using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hst.Imager.Core.Tests;

/// <summary>
/// Validates exFAT file system structures against rules described in Microsoft exFAT specification.
/// Used by tests to verify a formatted exFAT file system is valid independently of the exFAT formatter.
/// </summary>
public class ExFatValidator
{
    private readonly Stream stream;
    private readonly long partitionSize;
    private readonly List<string> errors = [];

    private int bytesPerSector;
    private int sectorsPerCluster;
    private uint fatOffset;
    private uint clusterHeapOffset;
    private uint clusterCount;

    public string VolumeLabel { get; private set; }
    public int BytesPerCluster => bytesPerSector * sectorsPerCluster;
    public uint ClusterCount => clusterCount;

    private ExFatValidator(Stream stream, long partitionSize)
    {
        this.stream = stream;
        this.partitionSize = partitionSize;
    }

    /// <summary>
    /// Validate exFAT file system in partition stream
    /// </summary>
    /// <param name="stream">Partition stream</param>
    /// <param name="partitionSize">Size of partition in bytes</param>
    /// <returns>Validator with errors found, empty if exFAT file system is valid</returns>
    public static ExFatValidator Validate(Stream stream, long partitionSize)
    {
        var validator = new ExFatValidator(stream, partitionSize);
        validator.Validate();
        return validator;
    }

    public IReadOnlyList<string> Errors => errors;

    private void Validate()
    {
        var bootSector = Read(0, 512);
        var bytesPerSectorShift = bootSector[108];
        if (bytesPerSectorShift is < 9 or > 12)
        {
            errors.Add($"Invalid BytesPerSectorShift {bytesPerSectorShift}");
            return;
        }

        bytesPerSector = 1 << bytesPerSectorShift;
        var mainBootRegion = Read(0, 12 * bytesPerSector);
        var backupBootRegion = Read(12L * bytesPerSector, 12 * bytesPerSector);
        ValidateBootRegion("Main", mainBootRegion);
        ValidateBootRegion("Backup", backupBootRegion);
        if (!mainBootRegion.SequenceEqual(backupBootRegion))
        {
            errors.Add("Main and backup boot regions are not equal");
        }

        if (errors.Count > 0)
        {
            return;
        }

        var sectorsPerClusterShift = bootSector[109];
        sectorsPerCluster = 1 << sectorsPerClusterShift;
        var volumeLength = BinaryPrimitives.ReadUInt64LittleEndian(bootSector.AsSpan(72));
        fatOffset = BinaryPrimitives.ReadUInt32LittleEndian(bootSector.AsSpan(80));
        var fatLength = BinaryPrimitives.ReadUInt32LittleEndian(bootSector.AsSpan(84));
        clusterHeapOffset = BinaryPrimitives.ReadUInt32LittleEndian(bootSector.AsSpan(88));
        clusterCount = BinaryPrimitives.ReadUInt32LittleEndian(bootSector.AsSpan(92));
        var rootDirectoryCluster = BinaryPrimitives.ReadUInt32LittleEndian(bootSector.AsSpan(96));
        var fileSystemRevision = BinaryPrimitives.ReadUInt16LittleEndian(bootSector.AsSpan(104));
        var numberOfFats = bootSector[110];
        var percentInUse = bootSector[112];

        // boot sector field ranges
        Check(sectorsPerClusterShift <= 25 - bytesPerSectorShift,
            $"Invalid SectorsPerClusterShift {sectorsPerClusterShift}");
        Check(volumeLength >= (1UL << 20) / (ulong)bytesPerSector, $"VolumeLength {volumeLength} is less than 1MB");
        Check(volumeLength * (ulong)bytesPerSector <= (ulong)partitionSize,
            $"VolumeLength {volumeLength} exceeds partition size {partitionSize}");
        Check(fatOffset >= 24, $"FatOffset {fatOffset} is less than 24");
        Check(fatLength >= ((ulong)clusterCount + 2) * 4 / (ulong)bytesPerSector +
            (((ulong)clusterCount + 2) * 4 % (ulong)bytesPerSector == 0 ? 0UL : 1UL),
            $"FatLength {fatLength} is too small for ClusterCount {clusterCount}");
        Check(clusterHeapOffset >= fatOffset + (ulong)fatLength * numberOfFats,
            $"ClusterHeapOffset {clusterHeapOffset} overlaps FAT");
        Check(clusterCount == Math.Min((volumeLength - clusterHeapOffset) >> sectorsPerClusterShift, 0xFFFFFFF5UL),
            $"ClusterCount {clusterCount} doesn't match volume length and cluster heap offset");
        Check(numberOfFats is 1 or 2, $"Invalid NumberOfFats {numberOfFats}");
        Check(fileSystemRevision == 0x0100, $"Unexpected FileSystemRevision {fileSystemRevision:x4}");
        Check(percentInUse <= 100 || percentInUse == 0xFF, $"Invalid PercentInUse {percentInUse}");
        Check(IsValidCluster(rootDirectoryCluster), $"Invalid FirstClusterOfRootDirectory {rootDirectoryCluster}");

        // fat entries 0 and 1
        Check(GetFatEntry(0) == 0xFFFFFFF8, $"Invalid FAT entry 0 {GetFatEntry(0):x8}");
        Check(GetFatEntry(1) == 0xFFFFFFFF, $"Invalid FAT entry 1 {GetFatEntry(1):x8}");

        if (errors.Count > 0)
        {
            return;
        }

        // root directory
        var rootDirectoryClusters = GetClusterChain("Root directory", rootDirectoryCluster, null);
        if (rootDirectoryClusters == null)
        {
            return;
        }

        var rootDirectory = ReadClusters(rootDirectoryClusters);
        var allocatedClusters = new HashSet<uint>(rootDirectoryClusters);
        int bitmapEntries = 0, upCaseTableEntries = 0, volumeLabelEntries = 0;
        for (var offset = 0; offset < rootDirectory.Length; offset += 32)
        {
            var entry = rootDirectory.AsSpan(offset, 32);
            var entryType = entry[0];
            if (entryType == 0)
            {
                break;
            }

            switch (entryType)
            {
                case 0x81:
                    bitmapEntries++;
                    ValidateAllocationBitmapEntry(entry, allocatedClusters);
                    break;
                case 0x82:
                    upCaseTableEntries++;
                    ValidateUpCaseTableEntry(entry, allocatedClusters);
                    break;
                case 0x83:
                    volumeLabelEntries++;
                    var characterCount = entry[1];
                    Check(characterCount <= 11, $"Invalid volume label character count {characterCount}");
                    VolumeLabel = Encoding.Unicode.GetString(entry.Slice(2, Math.Min((int)characterCount, 11) * 2));
                    break;
                default:
                    if ((entryType & 0x80) != 0)
                    {
                        errors.Add($"Unexpected in use directory entry type {entryType:x2} at offset {offset}");
                    }
                    break;
            }
        }

        Check(bitmapEntries == 1, $"Root directory has {bitmapEntries} allocation bitmap entries, expected 1");
        Check(upCaseTableEntries == 1, $"Root directory has {upCaseTableEntries} up-case table entries, expected 1");
        Check(volumeLabelEntries <= 1, $"Root directory has {volumeLabelEntries} volume label entries");

        if (errors.Count == 0)
        {
            ValidateAllocatedClusters(rootDirectory, allocatedClusters);
        }
    }

    private void ValidateBootRegion(string name, byte[] bootRegion)
    {
        Check(bootRegion[0] == 0xEB && bootRegion[1] == 0x76 && bootRegion[2] == 0x90, $"{name} invalid JumpBoot");
        Check(Encoding.ASCII.GetString(bootRegion, 3, 8) == "EXFAT   ", $"{name} invalid FileSystemName");
        Check(bootRegion.AsSpan(11, 53).IndexOfAnyExcept((byte)0) < 0, $"{name} MustBeZero is not zero");
        Check(bootRegion[510] == 0x55 && bootRegion[511] == 0xAA, $"{name} invalid BootSignature");

        for (var sector = 1; sector <= 8; sector++)
        {
            var signature = BinaryPrimitives.ReadUInt32LittleEndian(
                bootRegion.AsSpan(sector * bytesPerSector + bytesPerSector - 4));
            Check(signature == 0xAA550000, $"{name} extended boot sector {sector} invalid ExtendedBootSignature");
        }

        uint checksum = 0;
        for (var i = 0; i < bytesPerSector * 11; i++)
        {
            if (i is 106 or 107 or 112)
            {
                continue;
            }

            checksum = ((checksum & 1) != 0 ? 0x80000000 : 0) + (checksum >> 1) + bootRegion[i];
        }

        for (var offset = 11 * bytesPerSector; offset < 12 * bytesPerSector; offset += 4)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(bootRegion.AsSpan(offset)) != checksum)
            {
                errors.Add($"{name} boot checksum doesn't match");
                break;
            }
        }
    }

    private void ValidateAllocationBitmapEntry(ReadOnlySpan<byte> entry, HashSet<uint> allocatedClusters)
    {
        var bitmapFlags = entry[1];
        var firstCluster = BinaryPrimitives.ReadUInt32LittleEndian(entry[20..]);
        var dataLength = BinaryPrimitives.ReadUInt64LittleEndian(entry[24..]);

        Check(bitmapFlags == 0, $"Allocation bitmap has unexpected BitmapFlags {bitmapFlags}");
        Check(dataLength == (clusterCount + 7UL) / 8,
            $"Allocation bitmap DataLength {dataLength} doesn't match cluster count {clusterCount}");

        var clusters = GetClusterChain("Allocation bitmap", firstCluster, dataLength);
        if (clusters != null)
        {
            allocatedClusters.UnionWith(clusters);
        }
    }

    private void ValidateUpCaseTableEntry(ReadOnlySpan<byte> entry, HashSet<uint> allocatedClusters)
    {
        var tableChecksum = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
        var firstCluster = BinaryPrimitives.ReadUInt32LittleEndian(entry[20..]);
        var dataLength = BinaryPrimitives.ReadUInt64LittleEndian(entry[24..]);

        Check(tableChecksum == 0xE619D30D, $"Up-case table checksum {tableChecksum:x8} is not recommended table");

        var clusters = GetClusterChain("Up-case table", firstCluster, dataLength);
        if (clusters == null)
        {
            return;
        }

        allocatedClusters.UnionWith(clusters);
        var upCaseTable = ReadClusters(clusters).AsSpan(0, (int)dataLength);
        uint checksum = 0;
        foreach (var b in upCaseTable)
        {
            checksum = ((checksum & 1) != 0 ? 0x80000000 : 0) + (checksum >> 1) + b;
        }

        Check(checksum == tableChecksum, $"Up-case table data checksum {checksum:x8} doesn't match {tableChecksum:x8}");

        // decompress up-case table, where ffffh followed by a count maps count characters to themselves
        var mappings = new List<int>();
        for (var i = 0; i + 1 < upCaseTable.Length; i += 2)
        {
            var value = BinaryPrimitives.ReadUInt16LittleEndian(upCaseTable[i..]);
            if (value == 0xFFFF && i + 3 < upCaseTable.Length)
            {
                var count = BinaryPrimitives.ReadUInt16LittleEndian(upCaseTable[(i + 2)..]);
                for (var c = 0; c < count; c++)
                {
                    mappings.Add(mappings.Count);
                }
                i += 2;
                continue;
            }

            mappings.Add(value);
        }

        Check(mappings.Count == 65536,
            $"Up-case table covers {mappings.Count} characters, expected complete range of 65536 characters");

        // mandatory first 128 entries, a-z mapped to A-Z and others mapped to themselves
        for (var c = 0; c < Math.Min(128, mappings.Count); c++)
        {
            var expected = c is >= 'a' and <= 'z' ? c - 32 : c;
            var actual = mappings[c];
            if (actual != expected)
            {
                errors.Add($"Up-case table maps mandatory character {c:x4} to {actual:x4}");
                break;
            }
        }
    }

    private void ValidateAllocatedClusters(byte[] rootDirectory, HashSet<uint> allocatedClusters)
    {
        var bitmapEntryOffset = Enumerable.Range(0, rootDirectory.Length / 32).First(i => rootDirectory[i * 32] == 0x81) * 32;
        var firstCluster = BinaryPrimitives.ReadUInt32LittleEndian(rootDirectory.AsSpan(bitmapEntryOffset + 20));
        var dataLength = BinaryPrimitives.ReadUInt64LittleEndian(rootDirectory.AsSpan(bitmapEntryOffset + 24));
        var bitmap = ReadClusters(GetClusterChain("Allocation bitmap", firstCluster, dataLength));

        // clusters used by allocation bitmap, up-case table and root directory are allocated and no other clusters
        for (uint cluster = 2; cluster < clusterCount + 2; cluster++)
        {
            var index = cluster - 2;
            var isAllocated = (bitmap[index / 8] & (1 << (int)(index % 8))) != 0;
            if (isAllocated != allocatedClusters.Contains(cluster))
            {
                errors.Add($"Cluster {cluster} is {(isAllocated ? "" : "not ")}allocated in allocation bitmap");
                return;
            }
        }
    }

    private List<uint> GetClusterChain(string name, uint firstCluster, ulong? dataLength)
    {
        if (!IsValidCluster(firstCluster))
        {
            errors.Add($"{name} has invalid first cluster {firstCluster}");
            return null;
        }

        var expectedClusters = dataLength.HasValue
            ? (long)((dataLength.Value + (ulong)BytesPerCluster - 1) / (ulong)BytesPerCluster)
            : (long?)null;
        var clusters = new List<uint>();
        var cluster = firstCluster;
        while (true)
        {
            clusters.Add(cluster);
            var next = GetFatEntry(cluster);
            if (next == 0xFFFFFFFF)
            {
                break;
            }

            if (!IsValidCluster(next) || next <= cluster || clusters.Count > clusterCount)
            {
                errors.Add($"{name} has invalid FAT entry {next:x8} for cluster {cluster}");
                return null;
            }

            cluster = next;
        }

        if (expectedClusters.HasValue && clusters.Count != expectedClusters.Value)
        {
            errors.Add($"{name} cluster chain has {clusters.Count} clusters, expected {expectedClusters.Value}");
            return null;
        }

        return clusters;
    }

    private bool IsValidCluster(uint cluster) => cluster >= 2 && cluster <= clusterCount + 1;

    private uint GetFatEntry(uint cluster) =>
        BinaryPrimitives.ReadUInt32LittleEndian(Read((long)fatOffset * bytesPerSector + cluster * 4L, 4));

    private byte[] ReadClusters(IReadOnlyList<uint> clusters)
    {
        var data = new byte[clusters.Count * (long)BytesPerCluster];
        for (var i = 0; i < clusters.Count; i++)
        {
            var offset = ((long)clusterHeapOffset + (long)(clusters[i] - 2) * sectorsPerCluster) * bytesPerSector;
            Read(offset, BytesPerCluster).CopyTo(data, i * (long)BytesPerCluster);
        }

        return data;
    }

    private byte[] Read(long offset, int count)
    {
        var buffer = new byte[count];
        stream.Position = offset;
        stream.ReadExactly(buffer);
        return buffer;
    }

    private void Check(bool condition, string error)
    {
        if (!condition)
        {
            errors.Add(error);
        }
    }
}
