using System.Collections.Generic;
using Hst.Core.Converters;

namespace Hst.Imager.Core.FileSystems.Fat.Clusters;

/// <summary>
/// FAT32 cluster reader for reading cluster chain in file allocation table.
/// </summary>
public static class Fat32Cluster
{
    private const int FatEntrySizeBits = 32;
    private const int FatEntrySizeBytes = FatEntrySizeBits / 8;
    
    private static bool IsFreeCluster(uint fatEntry) => fatEntry == 0;
    private static bool IsReservedCluster(uint fatEntry) => fatEntry is 1 or >= 0x0FFFFFF0 and <= 0x0FFFFFF6;
    private static bool IsBadCluster(uint fatEntry) => fatEntry == 0x0FFFFFF7; 
    private static bool IsEndOfChain(uint fatEntry) => fatEntry is >= 0x0FFFFFF8 and <= 0x0FFFFFFF; 

    /// <summary>
    /// Read cluster chain from FAT32 file allocation table bytes starting from first cluster.
    /// </summary>
    /// <param name="fatBytes"></param>
    /// <param name="firstCluster"></param>
    /// <returns></returns>
    public static IEnumerable<uint> ReadClusterChain(byte[] fatBytes, uint firstCluster)
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

            nextCluster = LittleEndianConverter.ConvertBytesToUInt32(fatBytes, (int)fatEntryOffset);
        } while (!IsEndOfChain(nextCluster));

        return clusters;
    }
}