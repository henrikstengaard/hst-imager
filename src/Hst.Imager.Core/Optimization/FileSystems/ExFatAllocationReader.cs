using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Hst.Imager.Core.Optimization.FileSystems;

/// <summary>
/// Reads free clusters from exFAT file systems using the allocation bitmap.
/// </summary>
public static class ExFatAllocationReader
{
    private const byte AllocationBitmapEntryType = 0x81;
    private const uint EndOfChain = 0xfffffff7;
    private const int MaxDirectoryClusters = 65536;

    /// <summary>
    /// Read free ranges of exFAT file system in volume.
    /// </summary>
    /// <returns>File system allocation or null, if volume is not a exFAT file system.</returns>
    public static async Task<FileSystemAllocation> Read(Stream stream, long volumeOffset, long volumeSize)
    {
        var bootSector = await StreamReadHelper.ReadBytesAt(stream, volumeOffset, 512);
        if (Encoding.ASCII.GetString(bootSector, 3, 8) != "EXFAT   " ||
            bootSector[510] != 0x55 || bootSector[511] != 0xaa)
        {
            return null;
        }

        var fatOffset = BitConverter.ToUInt32(bootSector, 80);
        var clusterHeapOffset = BitConverter.ToUInt32(bootSector, 88);
        var clusterCount = BitConverter.ToUInt32(bootSector, 92);
        var rootDirectoryCluster = BitConverter.ToUInt32(bootSector, 96);
        var volumeFlags = BitConverter.ToUInt16(bootSector, 106);
        var bytesPerSectorShift = bootSector[108];
        var sectorsPerClusterShift = bootSector[109];

        if (bytesPerSectorShift is < 9 or > 12 || sectorsPerClusterShift > 25 - bytesPerSectorShift ||
            clusterCount == 0 || rootDirectoryCluster < 2 || rootDirectoryCluster >= clusterCount + 2)
        {
            throw new IOException("Invalid exFAT boot sector");
        }

        var context = new ExFatContext(stream, volumeOffset,
            volumeOffset + ((long)fatOffset << bytesPerSectorShift),
            volumeOffset + ((long)clusterHeapOffset << bytesPerSectorShift),
            1L << (bytesPerSectorShift + sectorsPerClusterShift),
            clusterCount);

        if (context.HeapOffset + clusterCount * context.ClusterSize > volumeOffset + volumeSize)
        {
            throw new IOException("exFAT cluster heap exceeds volume size");
        }

        // find allocation bitmap in root directory. use second bitmap, if texfat second fat is active
        var activeBitmap = (volumeFlags & 1) == 0 ? 0 : 1;
        var rootDirectoryClusters = await ReadClusterChain(context, rootDirectoryCluster, 0);
        uint bitmapFirstCluster = 0;
        ulong bitmapLength = 0;
        foreach (var cluster in rootDirectoryClusters)
        {
            var clusterBytes = await StreamReadHelper.ReadBytesAt(stream, context.GetClusterOffset(cluster),
                (int)context.ClusterSize);
            for (var offset = 0; offset < clusterBytes.Length; offset += 32)
            {
                var entryType = clusterBytes[offset];
                if (entryType == 0)
                {
                    break;
                }

                if (entryType != AllocationBitmapEntryType || (clusterBytes[offset + 1] & 1) != activeBitmap)
                {
                    continue;
                }

                bitmapFirstCluster = BitConverter.ToUInt32(clusterBytes, offset + 20);
                bitmapLength = BitConverter.ToUInt64(clusterBytes, offset + 24);
                break;
            }

            if (bitmapFirstCluster != 0)
            {
                break;
            }
        }

        if (bitmapFirstCluster == 0 || bitmapLength < (clusterCount + 7) / 8)
        {
            throw new IOException("exFAT allocation bitmap not found");
        }

        var bitmapClusters = await ReadClusterChain(context, bitmapFirstCluster,
            (long)((bitmapLength + (ulong)context.ClusterSize - 1) / (ulong)context.ClusterSize));

        // read bitmap
        var bitmap = new byte[(clusterCount + 7) / 8];
        var bitmapPosition = 0;
        foreach (var cluster in bitmapClusters)
        {
            if (bitmapPosition >= bitmap.Length)
            {
                break;
            }

            var count = (int)Math.Min(context.ClusterSize, bitmap.Length - bitmapPosition);
            await StreamReadHelper.ReadAt(stream, context.GetClusterOffset(cluster), bitmap, bitmapPosition, count);
            bitmapPosition += count;
        }

        if (bitmapPosition < bitmap.Length)
        {
            throw new IOException("exFAT allocation bitmap is incomplete");
        }

        var builder = new FreeRangeBuilder();
        for (long index = 0; index < clusterCount; index++)
        {
            if ((bitmap[index >> 3] & (1 << (int)(index & 7))) == 0)
            {
                builder.Add(context.GetClusterOffset(index + 2), context.ClusterSize);
            }
        }

        return new FileSystemAllocation("exFAT", builder.Build());
    }

    /// <summary>
    /// Read cluster chain from fat. If fat entry of first cluster is zero, clusters are contiguous
    /// (no fat chain) and expected cluster count is used.
    /// </summary>
    private static async Task<IList<uint>> ReadClusterChain(ExFatContext context, uint firstCluster,
        long expectedClusters)
    {
        var clusters = new List<uint>();
        var cluster = firstCluster;
        var entryBytes = new byte[4];

        while (cluster >= 2 && cluster < context.ClusterCount + 2 && clusters.Count < MaxDirectoryClusters * 64)
        {
            clusters.Add(cluster);

            await StreamReadHelper.ReadAt(context.Stream, context.FatOffset + (long)cluster * 4, entryBytes, 0, 4);
            var next = BitConverter.ToUInt32(entryBytes, 0);

            if (next == 0 && clusters.Count == 1)
            {
                // no fat chain, clusters are contiguous
                for (var i = 1; i < expectedClusters; i++)
                {
                    clusters.Add(firstCluster + (uint)i);
                }

                break;
            }

            if (next >= EndOfChain || next == 0)
            {
                break;
            }

            cluster = next;
        }

        return clusters;
    }

    private record ExFatContext(
        Stream Stream,
        long VolumeOffset,
        long FatOffset,
        long HeapOffset,
        long ClusterSize,
        uint ClusterCount)
    {
        public long GetClusterOffset(long cluster) => HeapOffset + (cluster - 2) * ClusterSize;
    }
}
