using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Hst.Imager.Core.FileSystems.ExFat;

/// <summary>
/// Formats partitions with exFAT file system as described in Microsoft exFAT specification
/// https://learn.microsoft.com/en-us/windows/win32/fileio/exfat-specification.
/// Writes all exFAT structures (boot regions, FAT, allocation bitmap, up-case table and root directory) in full, so
/// existing data on a previously used disk doesn't affect the formatted file system.
/// </summary>
public static class ExFatFormatter
{
    /// <summary>
    /// Max cluster size of 32MB supported by exFAT
    /// </summary>
    public const int MaxClusterSize = 32 * 1024 * 1024;

    /// <summary>
    /// Max number of clusters in cluster heap, 2^32 - 11
    /// </summary>
    public const uint MaxClusterCount = 0xFFFFFFF5;

    /// <summary>
    /// Min size of exFAT volume, 1MB
    /// </summary>
    public const long MinVolumeSize = 1024 * 1024;

    /// <summary>
    /// Max length of volume label in characters
    /// </summary>
    public const int MaxVolumeLabelLength = 11;

    private const int BootRegionSectors = 12;
    private const int DirectoryEntrySize = 32;
    private const int FatEntrySize = 4;
    private const int FirstClusterIndex = 2;
    private const int WriteBufferSize = 1024 * 1024;
    private const uint FatEntryMediaType = 0xFFFFFFF8;
    private const uint FatEntryEndOfChain = 0xFFFFFFFF;
    private const byte AllocationBitmapEntryType = 0x81;
    private const byte UpCaseTableEntryType = 0x82;
    private const byte VolumeLabelEntryType = 0x83;

    /// <summary>
    /// Format partition with exFAT.
    /// Optimal cluster size is calculated, if cluster size is not provided.
    /// </summary>
    /// <param name="stream">Stream of disk to format partition on</param>
    /// <param name="partitionOffset">Offset in bytes of partition on disk</param>
    /// <param name="size">Size of partition in bytes</param>
    /// <param name="bytesPerSector">Bytes per sector</param>
    /// <param name="volumeLabel">Volume label, max 11 characters</param>
    /// <param name="clusterSize">Cluster size in bytes (0 = optimal cluster size)</param>
    public static async Task FormatPartition(Stream stream, long partitionOffset, long size, int bytesPerSector,
        string volumeLabel, int clusterSize = 0)
    {
        var layout = CreateLayout(size, bytesPerSector, clusterSize);

        var bitmapLength = (layout.ClusterCount + 7) / 8;
        var bitmapClusters = (uint)((bitmapLength + layout.BytesPerCluster - 1) / layout.BytesPerCluster);
        var upCaseTableLength = ExFatUpCaseTable.CompressedTable.Length * 2;
        var upCaseTableClusters = (uint)((upCaseTableLength + layout.BytesPerCluster - 1) / layout.BytesPerCluster);
        var bitmapFirstCluster = (uint)FirstClusterIndex;
        var upCaseTableFirstCluster = bitmapFirstCluster + bitmapClusters;
        var rootDirectoryFirstCluster = upCaseTableFirstCluster + upCaseTableClusters;
        var usedClusters = bitmapClusters + upCaseTableClusters + 1;

        if (usedClusters > layout.ClusterCount)
        {
            throw new ArgumentOutOfRangeException(nameof(size),
                $"Size {size} is too small for exFAT with cluster size {layout.BytesPerCluster}");
        }

        var volumeSerialNumber = (uint)Random.Shared.NextInt64(uint.MaxValue);
        var percentInUse = (byte)(usedClusters * 100L / layout.ClusterCount);

        // main and backup boot regions
        var bootRegion = CreateBootRegion(layout, partitionOffset / bytesPerSector, rootDirectoryFirstCluster,
            volumeSerialNumber, percentInUse);
        await WriteAsync(stream, partitionOffset, bootRegion);
        await WriteAsync(stream, partitionOffset + (long)BootRegionSectors * bytesPerSector, bootRegion);

        // fat with cluster chains for allocation bitmap, up-case table and root directory
        await WriteFat(stream, partitionOffset + (long)layout.FatOffset * bytesPerSector,
            (long)layout.FatLength * bytesPerSector, [
                (bitmapFirstCluster, bitmapClusters),
                (upCaseTableFirstCluster, upCaseTableClusters),
                (rootDirectoryFirstCluster, 1)
            ]);

        // allocation bitmap with used clusters marked as allocated
        await WriteAllocationBitmap(stream, layout.GetClusterOffset(partitionOffset, bitmapFirstCluster),
            (long)bitmapClusters * layout.BytesPerCluster, usedClusters);

        // up-case table
        var upCaseTable = new byte[(long)upCaseTableClusters * layout.BytesPerCluster];
        for (var i = 0; i < ExFatUpCaseTable.CompressedTable.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(upCaseTable.AsSpan(i * 2, 2), ExFatUpCaseTable.CompressedTable[i]);
        }
        await WriteAsync(stream, layout.GetClusterOffset(partitionOffset, upCaseTableFirstCluster), upCaseTable);

        // root directory with allocation bitmap, up-case table and volume label directory entries
        var rootDirectory = CreateRootDirectory(layout.BytesPerCluster, bitmapFirstCluster, bitmapLength,
            upCaseTableFirstCluster, upCaseTableLength, volumeLabel);
        await WriteAsync(stream, layout.GetClusterOffset(partitionOffset, rootDirectoryFirstCluster), rootDirectory);

        await stream.FlushAsync();
    }

    /// <summary>
    /// Create layout of exFAT regions with FAT and cluster heap aligned to cluster size.
    /// </summary>
    /// <param name="size">Size of volume in bytes</param>
    /// <param name="bytesPerSector">Bytes per sector</param>
    /// <param name="clusterSize">Cluster size in bytes (0 = optimal cluster size)</param>
    /// <returns>Layout of exFAT regions</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static ExFatLayout CreateLayout(long size, int bytesPerSector, int clusterSize = 0)
    {
        if (bytesPerSector is < 512 or > 4096 || (bytesPerSector & (bytesPerSector - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesPerSector),
                $"Invalid bytes per sector {bytesPerSector}. Must be a power of 2 between 512 and 4096");
        }

        if (clusterSize != 0 && !IsValidClusterSize(clusterSize, bytesPerSector))
        {
            throw new ArgumentOutOfRangeException(nameof(clusterSize),
                $"Invalid exFAT cluster size {clusterSize}. Must be a power of 2 between {bytesPerSector} and {MaxClusterSize}");
        }

        if (size < MinVolumeSize)
        {
            throw new ArgumentOutOfRangeException(nameof(size),
                $"Size {size} is too small for exFAT, must be at least {MinVolumeSize}");
        }

        var bytesPerCluster = clusterSize > 0 ? clusterSize : Math.Max(CalculateClusterSize(size), bytesPerSector);
        var sectorsPerCluster = (uint)(bytesPerCluster / bytesPerSector);
        var volumeLength = (ulong)(size / bytesPerSector);

        // fat offset after main and backup boot regions aligned to cluster size
        var fatOffset = AlignUp(BootRegionSectors * 2, sectorsPerCluster);

        // fat length for max clusters, which can fit after fat offset, aligned to cluster size
        var maxClusterCount = fatOffset < volumeLength ? (volumeLength - fatOffset) / sectorsPerCluster : 0;
        var fatLength = AlignUp(((maxClusterCount + 2) * FatEntrySize + (ulong)bytesPerSector - 1) / (ulong)bytesPerSector,
            sectorsPerCluster);
        var clusterHeapOffset = fatOffset + fatLength;

        if (clusterHeapOffset >= volumeLength)
        {
            throw new ArgumentOutOfRangeException(nameof(size),
                $"Size {size} is too small for exFAT with cluster size {bytesPerCluster}");
        }

        var clusterCount = (volumeLength - clusterHeapOffset) / sectorsPerCluster;
        if (clusterCount > MaxClusterCount || clusterHeapOffset > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(clusterSize),
                $"Cluster size {bytesPerCluster} is too small for exFAT volume of size {size}, use a larger cluster size");
        }

        return new ExFatLayout(bytesPerSector, (int)sectorsPerCluster, volumeLength, (uint)fatOffset,
            (uint)fatLength, (uint)clusterHeapOffset, (uint)clusterCount);
    }

    /// <summary>
    /// Cluster size is valid for exFAT, if it's a power of 2 between bytes per sector and 32MB
    /// </summary>
    /// <param name="clusterSize">Cluster size in bytes</param>
    /// <param name="bytesPerSector">Bytes per sector</param>
    /// <returns></returns>
    public static bool IsValidClusterSize(int clusterSize, int bytesPerSector)
    {
        return clusterSize >= bytesPerSector && clusterSize <= MaxClusterSize &&
               (clusterSize & (clusterSize - 1)) == 0;
    }

    /// <summary>
    /// Calculate optimal cluster size in bytes for volume size using Microsoft's default exFAT cluster sizes
    /// </summary>
    /// <param name="size">Size of volume in bytes</param>
    /// <returns></returns>
    public static int CalculateClusterSize(long size)
    {
        if (size <= 256L * 1024 * 1024)
        {
            return 4 * 1024; // 4KB clusters up to 256MB
        }

        if (size <= 32L * 1024 * 1024 * 1024)
        {
            return 32 * 1024; // 32KB clusters up to 32GB
        }

        return 128 * 1024; // 128KB clusters above 32GB
    }

    /// <summary>
    /// Calculate boot checksum of the first 11 sectors of boot region excluding volume flags and percent in use
    /// fields in boot sector
    /// </summary>
    /// <param name="bootRegion">Boot region</param>
    /// <param name="bytesPerSector">Bytes per sector</param>
    /// <returns>Boot checksum</returns>
    public static uint CalculateBootChecksum(ReadOnlySpan<byte> bootRegion, int bytesPerSector)
    {
        uint checksum = 0;
        for (var i = 0; i < bytesPerSector * 11; i++)
        {
            if (i is 106 or 107 or 112)
            {
                continue;
            }

            checksum = ((checksum & 1) == 1 ? 0x80000000 : 0) + (checksum >> 1) + bootRegion[i];
        }

        return checksum;
    }

    private static byte[] CreateBootRegion(ExFatLayout layout, long partitionOffsetSectors,
        uint rootDirectoryFirstCluster, uint volumeSerialNumber, byte percentInUse)
    {
        var bytesPerSector = layout.BytesPerSector;
        var bootRegion = new byte[BootRegionSectors * bytesPerSector];
        var bootSector = bootRegion.AsSpan(0, bytesPerSector);

        // jump boot, file system name and must be zero bytes 11-63 (new array)
        bootSector[0] = 0xEB;
        bootSector[1] = 0x76;
        bootSector[2] = 0x90;
        Encoding.ASCII.GetBytes("EXFAT   ").CopyTo(bootSector[3..]);

        BinaryPrimitives.WriteUInt64LittleEndian(bootSector[64..], (ulong)partitionOffsetSectors);
        BinaryPrimitives.WriteUInt64LittleEndian(bootSector[72..], layout.VolumeLength);
        BinaryPrimitives.WriteUInt32LittleEndian(bootSector[80..], layout.FatOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(bootSector[84..], layout.FatLength);
        BinaryPrimitives.WriteUInt32LittleEndian(bootSector[88..], layout.ClusterHeapOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(bootSector[92..], layout.ClusterCount);
        BinaryPrimitives.WriteUInt32LittleEndian(bootSector[96..], rootDirectoryFirstCluster);
        BinaryPrimitives.WriteUInt32LittleEndian(bootSector[100..], volumeSerialNumber);
        BinaryPrimitives.WriteUInt16LittleEndian(bootSector[104..], 0x0100); // file system revision 1.00
        BinaryPrimitives.WriteUInt16LittleEndian(bootSector[106..], 0); // volume flags
        bootSector[108] = (byte)Log2(bytesPerSector);
        bootSector[109] = (byte)Log2(layout.SectorsPerCluster);
        bootSector[110] = 1; // number of fats
        bootSector[111] = 0x80; // drive select
        bootSector[112] = percentInUse;

        // boot code with halt instructions as no boot-strapping instructions are provided
        bootSector[120..510].Fill(0xF4);
        bootSector[510] = 0x55;
        bootSector[511] = 0xAA;

        // main extended boot sectors 1-8 with extended boot signature at end of sector
        for (var sector = 1; sector <= 8; sector++)
        {
            var offset = sector * bytesPerSector;
            BinaryPrimitives.WriteUInt32LittleEndian(bootRegion.AsSpan(offset + bytesPerSector - 4, 4), 0xAA550000);
        }

        // oem parameters sector 9 and reserved sector 10 are zero
        // boot checksum sector 11 with repeating boot checksum
        var checksum = CalculateBootChecksum(bootRegion, bytesPerSector);
        for (var offset = 11 * bytesPerSector; offset < 12 * bytesPerSector; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bootRegion.AsSpan(offset, 4), checksum);
        }

        return bootRegion;
    }

    private static async Task WriteFat(Stream stream, long offset, long length,
        (uint FirstCluster, uint Clusters)[] clusterChains)
    {
        var buffer = new byte[WriteBufferSize];
        for (long position = 0; position < length; position += buffer.Length)
        {
            var count = (int)Math.Min(buffer.Length, length - position);
            Array.Clear(buffer, 0, count);

            // fat entries 0 and 1 for media type and end of chain
            if (position == 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), FatEntryMediaType);
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4, 4), FatEntryEndOfChain);
            }

            // fat entries for cluster chains within current buffer
            foreach (var (firstCluster, clusters) in clusterChains)
            {
                for (var cluster = firstCluster; cluster < firstCluster + clusters; cluster++)
                {
                    var entryOffset = (long)cluster * FatEntrySize - position;
                    if (entryOffset < 0 || entryOffset >= count)
                    {
                        continue;
                    }

                    var nextCluster = cluster + 1 < firstCluster + clusters ? cluster + 1 : FatEntryEndOfChain;
                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan((int)entryOffset, 4), nextCluster);
                }
            }

            await WriteAsync(stream, offset + position, buffer, count);
        }
    }

    private static async Task WriteAllocationBitmap(Stream stream, long offset, long length, uint usedClusters)
    {
        var buffer = new byte[WriteBufferSize];
        for (long position = 0; position < length; position += buffer.Length)
        {
            var count = (int)Math.Min(buffer.Length, length - position);
            Array.Clear(buffer, 0, count);

            // set bits for used clusters within current buffer, bit 0 of byte 0 is first cluster
            var firstBit = position * 8;
            var lastBit = Math.Min(usedClusters, (position + count) * 8);
            for (var bit = firstBit; bit < lastBit; bit++)
            {
                buffer[(bit - firstBit) / 8] |= (byte)(1 << (int)(bit % 8));
            }

            await WriteAsync(stream, offset + position, buffer, count);
        }
    }

    private static byte[] CreateRootDirectory(int bytesPerCluster, uint bitmapFirstCluster, long bitmapLength,
        uint upCaseTableFirstCluster, long upCaseTableLength, string volumeLabel)
    {
        var rootDirectory = new byte[bytesPerCluster];

        // allocation bitmap directory entry
        var bitmapEntry = rootDirectory.AsSpan(0, DirectoryEntrySize);
        bitmapEntry[0] = AllocationBitmapEntryType;
        bitmapEntry[1] = 0; // bitmap flags, first allocation bitmap
        BinaryPrimitives.WriteUInt32LittleEndian(bitmapEntry[20..], bitmapFirstCluster);
        BinaryPrimitives.WriteUInt64LittleEndian(bitmapEntry[24..], (ulong)bitmapLength);

        // up-case table directory entry
        var upCaseTableEntry = rootDirectory.AsSpan(DirectoryEntrySize, DirectoryEntrySize);
        upCaseTableEntry[0] = UpCaseTableEntryType;
        BinaryPrimitives.WriteUInt32LittleEndian(upCaseTableEntry[4..], ExFatUpCaseTable.TableChecksum);
        BinaryPrimitives.WriteUInt32LittleEndian(upCaseTableEntry[20..], upCaseTableFirstCluster);
        BinaryPrimitives.WriteUInt64LittleEndian(upCaseTableEntry[24..], (ulong)upCaseTableLength);

        // volume label directory entry, if volume label is set
        var label = (volumeLabel ?? string.Empty).Trim();
        if (label.Length > MaxVolumeLabelLength)
        {
            label = label[..MaxVolumeLabelLength];
        }

        if (label.Length > 0)
        {
            var volumeLabelEntry = rootDirectory.AsSpan(DirectoryEntrySize * 2, DirectoryEntrySize);
            volumeLabelEntry[0] = VolumeLabelEntryType;
            volumeLabelEntry[1] = (byte)label.Length;
            Encoding.Unicode.GetBytes(label).CopyTo(volumeLabelEntry[2..]);
        }

        return rootDirectory;
    }

    private static Task WriteAsync(Stream stream, long offset, byte[] buffer) =>
        WriteAsync(stream, offset, buffer, buffer.Length);

    private static async Task WriteAsync(Stream stream, long offset, byte[] buffer, int count)
    {
        stream.Seek(offset, SeekOrigin.Begin);
        await stream.WriteAsync(buffer.AsMemory(0, count));
    }

    private static ulong AlignUp(ulong value, ulong alignment) => (value + alignment - 1) / alignment * alignment;

    private static int Log2(int value) => System.Numerics.BitOperations.Log2((uint)value);
}

/// <summary>
/// Layout of exFAT regions with offsets and lengths in sectors
/// </summary>
/// <param name="BytesPerSector">Bytes per sector</param>
/// <param name="SectorsPerCluster">Sectors per cluster</param>
/// <param name="VolumeLength">Length of volume in sectors</param>
/// <param name="FatOffset">Volume relative sector offset of FAT</param>
/// <param name="FatLength">Length of FAT in sectors</param>
/// <param name="ClusterHeapOffset">Volume relative sector offset of cluster heap</param>
/// <param name="ClusterCount">Number of clusters in cluster heap</param>
public record ExFatLayout(int BytesPerSector, int SectorsPerCluster, ulong VolumeLength, uint FatOffset,
    uint FatLength, uint ClusterHeapOffset, uint ClusterCount)
{
    public int BytesPerCluster => BytesPerSector * SectorsPerCluster;

    /// <summary>
    /// Get offset in bytes of cluster on disk
    /// </summary>
    /// <param name="partitionOffset">Offset in bytes of partition on disk</param>
    /// <param name="cluster">Cluster index, first cluster is 2</param>
    /// <returns>Offset in bytes</returns>
    public long GetClusterOffset(long partitionOffset, uint cluster) =>
        partitionOffset + ((long)ClusterHeapOffset + (long)(cluster - 2) * SectorsPerCluster) * BytesPerSector;
}
