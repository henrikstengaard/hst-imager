using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Validates Master Boot Record partition table including extended partitions.
/// </summary>
public static class MbrValidator
{
    private const int SectorSize = 512;
    private const int MaxLogicalPartitions = 128;

    private record MbrEntry(int Number, byte Status, byte Type, long FirstSector, long Sectors, bool Logical)
    {
        public long LastSector => FirstSector + Sectors - 1;
    }

    /// <summary>
    /// Detect if master boot record signature is present.
    /// </summary>
    public static async Task<bool> IsPresent(Stream stream, CancellationToken token)
    {
        var sector = await ReadSector(stream, 0, token);
        return sector != null && HasSignature(sector);
    }

    /// <summary>
    /// Validate master boot record and report issues found.
    /// </summary>
    /// <returns>Number of partitions found.</returns>
    public static async Task<int> Validate(Stream stream, long diskSize,
        Action<RecoveryIssueLevel, string, long?> addIssue, CancellationToken token)
    {
        var diskSectors = diskSize / SectorSize;
        var mbr = await ReadSector(stream, 0, token);
        if (mbr == null)
        {
            addIssue(RecoveryIssueLevel.Error, "Unable to read master boot record sector", 0);
            return 0;
        }

        if (!HasSignature(mbr))
        {
            addIssue(RecoveryIssueLevel.Error,
                $"Master boot record signature is invalid, expected 0x55AA, found 0x{mbr[510]:X2}{mbr[511]:X2}", 0);
            return 0;
        }

        var entries = new List<MbrEntry>();
        var activeCount = 0;

        for (var i = 0; i < 4; i++)
        {
            var entry = ReadEntry(mbr, i, i + 1, 0, false);
            if (entry.Type == 0)
            {
                continue;
            }

            if (entry.Status != 0x00 && entry.Status != 0x80)
            {
                addIssue(RecoveryIssueLevel.Warning,
                    $"Partition {entry.Number} has invalid status 0x{entry.Status:X2}, expected 0x00 or 0x80", 0);
            }

            if (entry.Status == 0x80)
            {
                activeCount++;
            }

            if (entry.Type == 0xEE)
            {
                addIssue(RecoveryIssueLevel.Info,
                    $"Partition {entry.Number} is a protective master boot record for a Guid Partition Table", 0);
                continue;
            }

            entries.Add(entry);

            if (IsExtended(entry.Type))
            {
                await ValidateExtended(stream, entry, diskSectors, entries, addIssue, token);
            }
        }

        if (activeCount > 1)
        {
            addIssue(RecoveryIssueLevel.Warning, $"{activeCount} partitions are marked active, only one is allowed", 0);
        }

        foreach (var entry in entries)
        {
            var name = entry.Logical ? $"Logical partition {entry.Number}" : $"Partition {entry.Number}";
            if (entry.Sectors == 0)
            {
                addIssue(RecoveryIssueLevel.Error, $"{name} has zero sectors", null);
                continue;
            }

            if (entry.FirstSector == 0)
            {
                addIssue(RecoveryIssueLevel.Error, $"{name} starts at sector 0 overlapping master boot record", 0);
            }

            if (diskSectors > 0 && entry.LastSector >= diskSectors)
            {
                addIssue(RecoveryIssueLevel.Error,
                    $"{name} ends at sector {entry.LastSector}, which is outside disk with {diskSectors} sectors",
                    entry.FirstSector);
            }
        }

        // check overlaps between primary partitions and between logical partitions
        CheckOverlaps(entries.Where(x => !x.Logical).ToList(), addIssue);
        CheckOverlaps(entries.Where(x => x.Logical).ToList(), addIssue);

        // logical partitions must be inside extended partition
        var extended = entries.FirstOrDefault(x => !x.Logical && IsExtended(x.Type));
        if (extended != null)
        {
            foreach (var logical in entries.Where(x => x.Logical && x.Sectors > 0))
            {
                if (logical.FirstSector < extended.FirstSector || logical.LastSector > extended.LastSector)
                {
                    addIssue(RecoveryIssueLevel.Error,
                        $"Logical partition {logical.Number} is outside extended partition {extended.Number}",
                        logical.FirstSector);
                }
            }
        }

        if (entries.Count(x => !x.Logical && IsExtended(x.Type)) > 1)
        {
            addIssue(RecoveryIssueLevel.Error, "More than one extended partition found", 0);
        }

        return entries.Count(x => !IsExtended(x.Type));
    }

    private static async Task ValidateExtended(Stream stream, MbrEntry extended, long diskSectors,
        List<MbrEntry> entries, Action<RecoveryIssueLevel, string, long?> addIssue, CancellationToken token)
    {
        var ebrSector = extended.FirstSector;
        var visited = new HashSet<long>();
        var number = 5;

        while (ebrSector > 0)
        {
            token.ThrowIfCancellationRequested();

            if (!visited.Add(ebrSector))
            {
                addIssue(RecoveryIssueLevel.Error, $"Extended boot record chain has a loop at sector {ebrSector}",
                    ebrSector);
                return;
            }

            if (visited.Count > MaxLogicalPartitions)
            {
                addIssue(RecoveryIssueLevel.Error,
                    $"Extended boot record chain has more than {MaxLogicalPartitions} logical partitions", ebrSector);
                return;
            }

            if (diskSectors > 0 && ebrSector >= diskSectors)
            {
                addIssue(RecoveryIssueLevel.Error,
                    $"Extended boot record at sector {ebrSector} is outside disk", ebrSector);
                return;
            }

            var ebr = await ReadSector(stream, ebrSector, token);
            if (ebr == null || !HasSignature(ebr))
            {
                addIssue(RecoveryIssueLevel.Error,
                    $"Extended boot record at sector {ebrSector} has invalid signature", ebrSector);
                return;
            }

            var logical = ReadEntry(ebr, 0, number, ebrSector, true);
            if (logical.Type != 0)
            {
                entries.Add(logical);
                number++;
            }

            var next = ReadEntry(ebr, 1, 0, extended.FirstSector, true);
            ebrSector = next.Type != 0 && IsExtended(next.Type) ? next.FirstSector : 0;
        }
    }

    private static void CheckOverlaps(IList<MbrEntry> entries, Action<RecoveryIssueLevel, string, long?> addIssue)
    {
        var sorted = entries.Where(x => x.Sectors > 0).OrderBy(x => x.FirstSector).ToList();
        for (var i = 1; i < sorted.Count; i++)
        {
            var previous = sorted[i - 1];
            var current = sorted[i];
            if (current.FirstSector <= previous.LastSector &&
                !(IsExtended(previous.Type) && current.Logical))
            {
                addIssue(RecoveryIssueLevel.Error,
                    $"Partition {current.Number} (sectors {current.FirstSector}-{current.LastSector}) overlaps partition {previous.Number} (sectors {previous.FirstSector}-{previous.LastSector})",
                    current.FirstSector);
            }
        }
    }

    private static MbrEntry ReadEntry(byte[] sector, int index, int number, long baseSector, bool logical)
    {
        var offset = 446 + index * 16;
        var status = sector[offset];
        var type = sector[offset + 4];
        var firstSector = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(offset + 8, 4));
        var sectors = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(offset + 12, 4));
        return new MbrEntry(number, status, type, baseSector + firstSector, sectors, logical);
    }

    private static bool IsExtended(byte type) => type is 0x05 or 0x0F or 0x85;

    private static bool HasSignature(byte[] sector) => sector[510] == 0x55 && sector[511] == 0xAA;

    private static async Task<byte[]> ReadSector(Stream stream, long sector, CancellationToken token)
    {
        var buffer = new byte[SectorSize];
        stream.Position = sector * SectorSize;
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
}
