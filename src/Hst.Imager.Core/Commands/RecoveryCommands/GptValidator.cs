using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Guid Partition Table header read from disk.
/// </summary>
public class GptHeader
{
    public const int MinHeaderSize = 92;

    public long Lba { get; init; }
    public byte[] Raw { get; init; }
    public bool SignatureValid { get; init; }
    public uint HeaderSize { get; init; }
    public uint HeaderCrc { get; init; }
    public bool HeaderCrcValid { get; init; }
    public long MyLba { get; init; }
    public long AlternateLba { get; init; }
    public long FirstUsableLba { get; init; }
    public long LastUsableLba { get; init; }
    public Guid DiskGuid { get; init; }
    public long EntriesLba { get; init; }
    public uint EntryCount { get; init; }
    public uint EntrySize { get; init; }
    public uint EntriesCrc { get; init; }
    public byte[] Entries { get; set; }
    public bool EntriesCrcValid { get; set; }
    public List<string> Problems { get; } = [];

    /// <summary>
    /// Header and partition entry array are valid.
    /// </summary>
    public bool IsValid => Problems.Count == 0;

    public int EntriesSectors(int sectorSize) =>
        (int)(((long)EntryCount * EntrySize + sectorSize - 1) / sectorSize);
}

/// <summary>
/// Result of validating Guid Partition Table.
/// </summary>
public class GptValidationResult
{
    public int SectorSize { get; init; }
    public long DiskSectors { get; init; }
    public GptHeader Primary { get; init; }
    public GptHeader Backup { get; init; }

    /// <summary>
    /// Primary and backup header and partition entry arrays are equal (except for header locations).
    /// </summary>
    public bool CopiesMatch { get; init; }

    /// <summary>
    /// Problems with primary and backup headers pointing to each other.
    /// </summary>
    public IList<string> AlternateProblems { get; init; } = [];

    public int Partitions { get; init; }
}

/// <summary>
/// Validates and repairs Guid Partition Table by parsing raw sectors.
/// </summary>
public static class GptValidator
{
    private static readonly byte[] Signature = "EFI PART"u8.ToArray();
    private static readonly int[] SectorSizes = [512, 4096];
    private const int EntryMinSize = 128;
    private const uint MaxEntries = 1024;

    /// <summary>
    /// Detect if guid partition table is present by protective mbr or primary or backup header signature.
    /// </summary>
    public static async Task<bool> IsPresent(Stream stream, long diskSize, CancellationToken token)
    {
        var mbr = await ReadSectors(stream, 0, 512, 1, token);
        if (mbr != null && mbr[510] == 0x55 && mbr[511] == 0xAA &&
            Enumerable.Range(0, 4).Any(i => mbr[446 + i * 16 + 4] == 0xEE))
        {
            return true;
        }

        foreach (var sectorSize in SectorSizes)
        {
            var primary = await ReadSectors(stream, 1, sectorSize, 1, token);
            if (primary != null && HasSignature(primary))
            {
                return true;
            }

            var lastLba = diskSize / sectorSize - 1;
            var backup = lastLba > 1 ? await ReadSectors(stream, lastLba, sectorSize, 1, token) : null;
            if (backup != null && HasSignature(backup))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validate guid partition table and report issues found.
    /// </summary>
    public static async Task<GptValidationResult> Validate(Stream stream, long diskSize,
        Action<RecoveryIssueLevel, string, long?> addIssue, CancellationToken token)
    {
        var result = await Read(stream, diskSize, token);

        // protective mbr
        var mbr = await ReadSectors(stream, 0, 512, 1, token);
        if (mbr == null || mbr[510] != 0x55 || mbr[511] != 0xAA ||
            !Enumerable.Range(0, 4).Any(i => mbr[446 + i * 16 + 4] == 0xEE))
        {
            addIssue(RecoveryIssueLevel.Warning, "Protective master boot record with partition type 0xEE not found",
                0);
        }

        foreach (var header in new[] { result.Primary, result.Backup })
        {
            var name = header == result.Primary ? "Primary" : "Backup";
            if (header.IsValid)
            {
                addIssue(RecoveryIssueLevel.Info, $"{name} header at sector {header.Lba} is valid", header.Lba);
                continue;
            }

            foreach (var problem in header.Problems)
            {
                addIssue(RecoveryIssueLevel.Error, $"{name} header: {problem}", header.Lba);
            }
        }

        if (result.Primary.IsValid && result.Backup.IsValid && !result.CopiesMatch)
        {
            addIssue(RecoveryIssueLevel.Error,
                "Primary and backup headers or partition entry arrays don't match", null);
        }

        foreach (var problem in result.AlternateProblems)
        {
            addIssue(RecoveryIssueLevel.Error, problem, null);
        }

        var valid = result.Primary.IsValid ? result.Primary : result.Backup.IsValid ? result.Backup : null;
        if (valid == null)
        {
            addIssue(RecoveryIssueLevel.Error, "No valid header found, partition table can't be repaired", null);
            return result;
        }

        ValidateEntries(valid, result.DiskSectors, addIssue);

        return result;
    }

    /// <summary>
    /// Repair guid partition table by restoring invalid header and partition entry array from valid copy.
    /// </summary>
    /// <returns>True, if changes were written.</returns>
    public static async Task<bool> Repair(Stream stream, GptValidationResult result,
        Action<RecoveryIssueLevel, string, long?> addIssue, CancellationToken token)
    {
        var sectorSize = result.SectorSize;
        var lastLba = result.DiskSectors - 1;

        if (!result.Primary.IsValid && !result.Backup.IsValid)
        {
            addIssue(RecoveryIssueLevel.Error, "Both primary and backup headers are invalid, unable to repair", null);
            return false;
        }

        if (result.Primary.IsValid && result.Backup.IsValid && result.CopiesMatch &&
            result.AlternateProblems.Count == 0 && result.Primary.AlternateLba == lastLba)
        {
            addIssue(RecoveryIssueLevel.Info, "Guid partition table is valid, no repair needed", null);
            return false;
        }

        if (result.Primary.IsValid)
        {
            if (result.Backup.IsValid && !result.CopiesMatch)
            {
                addIssue(RecoveryIssueLevel.Warning,
                    "Primary and backup don't match, backup is restored from primary", null);
            }

            // restore backup from primary at last sector of disk
            var source = result.Primary;
            var entriesSectors = source.EntriesSectors(sectorSize);
            var backupEntriesLba = lastLba - entriesSectors;

            if (source.AlternateLba != lastLba)
            {
                var primaryHeader = CreateHeader(source, 1, lastLba, source.EntriesLba, sectorSize);
                await WriteSectors(stream, 1, sectorSize, primaryHeader, token);
                addIssue(RecoveryIssueLevel.Info,
                    $"Primary header alternate sector updated from {source.AlternateLba} to {lastLba}", 1);
            }

            await WriteSectors(stream, backupEntriesLba, sectorSize, Pad(source.Entries, entriesSectors * sectorSize), token);
            var backupHeader = CreateHeader(source, lastLba, 1, backupEntriesLba, sectorSize);
            await WriteSectors(stream, lastLba, sectorSize, backupHeader, token);
            addIssue(RecoveryIssueLevel.Info,
                $"Backup header written to sector {lastLba} and partition entries to sector {backupEntriesLba}",
                lastLba);
        }
        else
        {
            // restore primary from backup
            var source = result.Backup;
            var entriesSectors = source.EntriesSectors(sectorSize);
            const long primaryEntriesLba = 2;

            await WriteSectors(stream, primaryEntriesLba, sectorSize, Pad(source.Entries, entriesSectors * sectorSize), token);
            var primaryHeader = CreateHeader(source, 1, source.Lba, primaryEntriesLba, sectorSize);
            await WriteSectors(stream, 1, sectorSize, primaryHeader, token);
            addIssue(RecoveryIssueLevel.Info,
                $"Primary header written to sector 1 and partition entries to sector {primaryEntriesLba}", 1);
        }

        await stream.FlushAsync(token);

        return true;
    }

    private static void ValidateEntries(GptHeader header, long diskSectors,
        Action<RecoveryIssueLevel, string, long?> addIssue)
    {
        if (header.FirstUsableLba > header.LastUsableLba)
        {
            addIssue(RecoveryIssueLevel.Error,
                $"First usable sector {header.FirstUsableLba} is after last usable sector {header.LastUsableLba}",
                header.Lba);
        }

        if (header.LastUsableLba >= diskSectors)
        {
            addIssue(RecoveryIssueLevel.Error,
                $"Last usable sector {header.LastUsableLba} is outside disk with {diskSectors} sectors", header.Lba);
        }

        var partitions = GetPartitions(header);
        foreach (var (number, first, last) in partitions)
        {
            if (first > last)
            {
                addIssue(RecoveryIssueLevel.Error,
                    $"Partition {number} first sector {first} is after last sector {last}", first);
                continue;
            }

            if (first < header.FirstUsableLba || last > header.LastUsableLba)
            {
                addIssue(RecoveryIssueLevel.Error,
                    $"Partition {number} (sectors {first}-{last}) is outside usable sectors {header.FirstUsableLba}-{header.LastUsableLba}",
                    first);
            }
        }

        var sorted = partitions.Where(x => x.First <= x.Last).OrderBy(x => x.First).ToList();
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].First <= sorted[i - 1].Last)
            {
                addIssue(RecoveryIssueLevel.Error,
                    $"Partition {sorted[i].Number} (sectors {sorted[i].First}-{sorted[i].Last}) overlaps partition {sorted[i - 1].Number} (sectors {sorted[i - 1].First}-{sorted[i - 1].Last})",
                    sorted[i].First);
            }
        }

        addIssue(RecoveryIssueLevel.Info, $"{partitions.Count} partitions found", null);
    }

    private static List<(int Number, long First, long Last)> GetPartitions(GptHeader header)
    {
        var partitions = new List<(int, long, long)>();
        if (header.Entries == null)
        {
            return partitions;
        }

        for (var i = 0; i < header.EntryCount; i++)
        {
            var offset = (int)(i * header.EntrySize);
            if (offset + EntryMinSize > header.Entries.Length)
            {
                break;
            }

            var typeGuid = header.Entries.AsSpan(offset, 16);
            if (typeGuid.IndexOfAnyExcept((byte)0) < 0)
            {
                continue;
            }

            var first = (long)BinaryPrimitives.ReadUInt64LittleEndian(header.Entries.AsSpan(offset + 32, 8));
            var last = (long)BinaryPrimitives.ReadUInt64LittleEndian(header.Entries.AsSpan(offset + 40, 8));
            partitions.Add((i + 1, first, last));
        }

        return partitions;
    }

    /// <summary>
    /// Read primary and backup headers and partition entry arrays.
    /// </summary>
    public static async Task<GptValidationResult> Read(Stream stream, long diskSize, CancellationToken token)
    {
        // detect sector size by primary header signature, fallback to 512
        var sectorSize = 512;
        foreach (var size in SectorSizes)
        {
            var sector = await ReadSectors(stream, 1, size, 1, token);
            if (sector != null && HasSignature(sector))
            {
                sectorSize = size;
                break;
            }

            var last = await ReadSectors(stream, diskSize / size - 1, size, 1, token);
            if (last != null && HasSignature(last))
            {
                sectorSize = size;
                break;
            }
        }

        var diskSectors = diskSize / sectorSize;
        var lastLba = diskSectors - 1;

        var primary = await ReadHeader(stream, 1, sectorSize, diskSectors, token);

        // backup is expected at last sector, but use alternate lba from primary if backup isn't found there
        var backup = await ReadHeader(stream, lastLba, sectorSize, diskSectors, token);
        if (!backup.SignatureValid && primary.IsValid && primary.AlternateLba != lastLba &&
            primary.AlternateLba > 1 && primary.AlternateLba < diskSectors)
        {
            var alternate = await ReadHeader(stream, primary.AlternateLba, sectorSize, diskSectors, token);
            if (alternate.SignatureValid)
            {
                backup = alternate;
            }
        }

        var alternateProblems = new List<string>();
        if (primary.IsValid && backup.IsValid && primary.AlternateLba != backup.Lba)
        {
            alternateProblems.Add($"Primary header alternate sector {primary.AlternateLba} doesn't point to backup header at sector {backup.Lba}");
        }

        if (backup.IsValid && backup.AlternateLba != 1)
        {
            alternateProblems.Add($"Backup header alternate sector {backup.AlternateLba} doesn't point to primary header at sector 1");
        }

        if (backup.IsValid && backup.Lba != lastLba)
        {
            alternateProblems.Add($"Backup header is at sector {backup.Lba}, expected last sector {lastLba}");
        }

        var copiesMatch = primary.SignatureValid && backup.SignatureValid &&
                          primary.DiskGuid == backup.DiskGuid &&
                          primary.FirstUsableLba == backup.FirstUsableLba &&
                          primary.LastUsableLba == backup.LastUsableLba &&
                          primary.EntryCount == backup.EntryCount &&
                          primary.EntrySize == backup.EntrySize &&
                          primary.Entries != null && backup.Entries != null &&
                          primary.Entries.AsSpan().SequenceEqual(backup.Entries);

        var valid = primary.IsValid ? primary : backup.IsValid ? backup : null;

        return new GptValidationResult
        {
            SectorSize = sectorSize,
            DiskSectors = diskSectors,
            Primary = primary,
            Backup = backup,
            CopiesMatch = copiesMatch,
            AlternateProblems = alternateProblems,
            Partitions = valid == null ? 0 : GetPartitions(valid).Count
        };
    }

    private static async Task<GptHeader> ReadHeader(Stream stream, long lba, int sectorSize, long diskSectors,
        CancellationToken token)
    {
        var raw = lba >= 0 && lba < diskSectors ? await ReadSectors(stream, lba, sectorSize, 1, token) : null;
        if (raw == null)
        {
            var missing = new GptHeader { Lba = lba, Raw = new byte[sectorSize] };
            missing.Problems.Add($"Unable to read sector {lba}");
            return missing;
        }

        if (!HasSignature(raw))
        {
            var invalid = new GptHeader { Lba = lba, Raw = raw };
            invalid.Problems.Add("Signature 'EFI PART' not found");
            return invalid;
        }

        var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(12, 4));
        var headerCrc = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(16, 4));
        var headerSizeValid = headerSize >= GptHeader.MinHeaderSize && headerSize <= sectorSize;
        var headerCrcValid = headerSizeValid && ComputeHeaderCrc(raw, (int)headerSize) == headerCrc;

        var header = new GptHeader
        {
            Lba = lba,
            Raw = raw,
            SignatureValid = true,
            HeaderSize = headerSize,
            HeaderCrc = headerCrc,
            HeaderCrcValid = headerCrcValid,
            MyLba = (long)BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(24, 8)),
            AlternateLba = (long)BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(32, 8)),
            FirstUsableLba = (long)BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(40, 8)),
            LastUsableLba = (long)BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(48, 8)),
            DiskGuid = new Guid(raw.AsSpan(56, 16)),
            EntriesLba = (long)BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(72, 8)),
            EntryCount = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(80, 4)),
            EntrySize = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(84, 4)),
            EntriesCrc = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(88, 4))
        };

        if (!headerSizeValid)
        {
            header.Problems.Add($"Header size {headerSize} is invalid");
            return header;
        }

        if (!headerCrcValid)
        {
            header.Problems.Add($"Header CRC32 0x{headerCrc:X8} is invalid, expected 0x{ComputeHeaderCrc(raw, (int)headerSize):X8}");
        }

        if (header.MyLba != lba)
        {
            header.Problems.Add($"Header sector {header.MyLba} doesn't match actual sector {lba}");
        }

        if (header.EntrySize < EntryMinSize || header.EntrySize % 8 != 0 || header.EntryCount == 0 ||
            header.EntryCount > MaxEntries)
        {
            header.Problems.Add($"Partition entry array with {header.EntryCount} entries of {header.EntrySize} bytes is invalid");
            return header;
        }

        var entriesSectors = header.EntriesSectors(sectorSize);
        if (header.EntriesLba < 2 || header.EntriesLba + entriesSectors > diskSectors)
        {
            header.Problems.Add($"Partition entry array at sector {header.EntriesLba} is outside disk");
            return header;
        }

        var entries = await ReadSectors(stream, header.EntriesLba, sectorSize, entriesSectors, token);
        if (entries == null)
        {
            header.Problems.Add($"Unable to read partition entry array at sector {header.EntriesLba}");
            return header;
        }

        header.Entries = entries.AsSpan(0, (int)(header.EntryCount * header.EntrySize)).ToArray();
        var entriesCrc = Crc32.Compute(header.Entries);
        header.EntriesCrcValid = entriesCrc == header.EntriesCrc;
        if (!header.EntriesCrcValid)
        {
            header.Problems.Add($"Partition entry array CRC32 0x{header.EntriesCrc:X8} is invalid, expected 0x{entriesCrc:X8}");
        }

        return header;
    }

    private static byte[] CreateHeader(GptHeader source, long myLba, long alternateLba, long entriesLba,
        int sectorSize)
    {
        var header = new byte[sectorSize];
        Array.Copy(source.Raw, header, (int)source.HeaderSize);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(24, 8), (ulong)myLba);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(32, 8), (ulong)alternateLba);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(72, 8), (ulong)entriesLba);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(88, 4), Crc32.Compute(source.Entries));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16, 4),
            ComputeHeaderCrc(header, (int)source.HeaderSize));
        return header;
    }

    private static uint ComputeHeaderCrc(byte[] header, int headerSize)
    {
        var copy = header.AsSpan(0, headerSize).ToArray();
        copy[16] = copy[17] = copy[18] = copy[19] = 0;
        return Crc32.Compute(copy);
    }

    private static byte[] Pad(byte[] data, int size)
    {
        if (data.Length >= size)
        {
            return data;
        }

        var padded = new byte[size];
        Array.Copy(data, padded, data.Length);
        return padded;
    }

    private static bool HasSignature(byte[] sector) => sector.AsSpan(0, Signature.Length).SequenceEqual(Signature);

    private static async Task<byte[]> ReadSectors(Stream stream, long lba, int sectorSize, int count,
        CancellationToken token)
    {
        if (lba < 0)
        {
            return null;
        }

        var buffer = new byte[sectorSize * count];
        var offset = lba * sectorSize;
        if (stream.CanSeek && offset + buffer.Length > stream.Length)
        {
            return null;
        }

        stream.Position = offset;
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), token);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total == buffer.Length ? buffer : null;
    }

    private static async Task WriteSectors(Stream stream, long lba, int sectorSize, byte[] data,
        CancellationToken token)
    {
        stream.Position = lba * sectorSize;
        await stream.WriteAsync(data, token);
    }
}
