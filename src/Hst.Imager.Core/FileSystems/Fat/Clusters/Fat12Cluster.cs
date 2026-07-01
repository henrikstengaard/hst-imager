using System;
using System.Collections.Generic;

namespace Hst.Imager.Core.FileSystems.Fat.Clusters;

/// <summary>
/// FAT12 cluster reader for reading cluster chain in file allocation table.
/// </summary>
public static class Fat12Cluster
{
    private const int FatEntrySizeBits = 12;
    private const double FatEntrySizeBytes = FatEntrySizeBits / 8.0d;

    private static bool IsFreeCluster(ushort fatEntry) => fatEntry == 0;
    private static bool IsReservedCluster(ushort fatEntry) => fatEntry is 1 or >= 0xFF0 and <= 0xFF6;
    private static bool IsBadCluster(ushort fatEntry) => fatEntry == 0xFF7;
    private static bool IsEndOfChain(ushort fatEntry) => fatEntry is >= 0xFF8 and <= 0xFFF;

    public static IEnumerable<uint> ReadClusterChain(byte[] fatBytes, ushort firstCluster)
    {
        var clusters = new List<uint>();

        if (IsFreeCluster(firstCluster) || IsEndOfChain(firstCluster))
        {
            return clusters;
        }

        var nextCluster = firstCluster;

        do
        {
            clusters.Add(nextCluster);

            var fatEntryOffset = Convert.ToInt32(Math.Floor(nextCluster * FatEntrySizeBytes));
            
            // little endian representing 12 bit even cluster: 0, 2, 4...
            // byte:  00000000 11111111
            // bit:   76543210 76543210
            // value: 10000000 ----1000
            // hex:   0x80     0x08
            //
            // cluster = and low 4 bits of byte 1, shift value left 8 bits + byte 0
            //
            // byte:  11111111 00000000 
            // bit:   76543210 76543210
            // value: ----1000 10000000
            // hex:   0x08     0x80
            //
            // cluster = ((byte 1 & 0x0f << 8)) + byte 0 = 2176

            // little endian representing 12 bit odd cluster: 1, 3, 5...
            // byte:  00000000 11111111
            // bit:   76543210 76543210
            // value: 1000---- 00001000 
            // hex:   0x80     0x08
            //
            // cluster = byte 1 left shifted 4 bits + and high 4 bits of byte 0 right shifted 4 bits
            //
            // byte:  11111111 00000000 
            // bit:   76543210 76543210
            // value: 00001000 1000----
            // hex:   0x08     0x80
            //
            // cluster = (byte 1 << 4) + ((byte 0 & 0xf0) >> 4) = 136
            
            nextCluster = (ushort)(nextCluster % 2 == 0
                ? ((fatBytes[fatEntryOffset + 1] & 0x0f) << 8) + fatBytes[fatEntryOffset]
                : (fatBytes[fatEntryOffset + 1] << 4) + ((fatBytes[fatEntryOffset] & 0xf0) >> 4));
        } while (!IsEndOfChain(nextCluster));

        return clusters;
    }
}