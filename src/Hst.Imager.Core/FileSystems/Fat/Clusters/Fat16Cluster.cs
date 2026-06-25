using System.Collections.Generic;
using Hst.Core.Converters;

namespace Hst.Imager.Core.FileSystems.Fat.Clusters;

/// <summary>
/// FAT16 iterator for reading cluster chain in file allocation table.
/// </summary>
public static class Fat16Cluster
{
    private const int FatEntrySizeBits = 16;
    private const int FatEntrySizeBytes = FatEntrySizeBits / 8;
    
    private static bool IsFreeCluster(ushort fatEntry) => fatEntry == 0;
    private static bool IsReservedCluster(ushort fatEntry) => fatEntry is 1 or >= 0xFFF0 and <= 0xFFF6;
    private static bool IsBadCluster(ushort fatEntry) => fatEntry == 0xFFF7; 
    private static bool IsEndOfChain(ushort fatEntry) => fatEntry is >= 0xFFF8 and <= 0xFFFF; 

    /// <summary>
    /// Read cluster chain from FAT16 file allocation table bytes starting from first cluster.
    /// </summary>
    /// <param name="fatBytes"></param>
    /// <param name="firstCluster"></param>
    /// <returns></returns>
    public static IEnumerable<uint> ReadClusterChain(byte[] fatBytes, ushort firstCluster)
    {
        if (IsFreeCluster(firstCluster) || IsEndOfChain(firstCluster))
        {
            return [];
        }

        var clusters = new List<uint>();
        var nextCluster = firstCluster;

        do
        {
            clusters.Add(nextCluster);

            var fatEntryOffset = nextCluster * FatEntrySizeBytes;

            nextCluster = LittleEndianConverter.ConvertBytesToUInt16(fatBytes, fatEntryOffset);
        } while (!IsEndOfChain(nextCluster));

        return clusters;
    }
}