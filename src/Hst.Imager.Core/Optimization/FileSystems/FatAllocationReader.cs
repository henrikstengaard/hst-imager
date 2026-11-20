using System;
using System.IO;
using System.Threading.Tasks;

namespace Hst.Imager.Core.Optimization.FileSystems;

/// <summary>
/// Reads free clusters from FAT12, FAT16 and FAT32 file systems using the first file allocation table.
/// </summary>
public static class FatAllocationReader
{
    private const int Fat32ChunkEntries = 256 * 1024;

    /// <summary>
    /// Read free ranges of FAT file system in volume.
    /// </summary>
    /// <returns>File system allocation or null, if volume is not a FAT file system.</returns>
    public static async Task<FileSystemAllocation> Read(Stream stream, long volumeOffset, long volumeSize)
    {
        var bootSector = await StreamReadHelper.ReadBytesAt(stream, volumeOffset, 512);

        if (!TryParseBootSector(bootSector, volumeSize, out var bpb))
        {
            return null;
        }

        var builder = new FreeRangeBuilder();
        var fatOffset = volumeOffset + (long)bpb.ReservedSectors * bpb.BytesPerSector;
        var clusterSize = (long)bpb.SectorsPerCluster * bpb.BytesPerSector;
        var dataOffset = volumeOffset + bpb.FirstDataSector * bpb.BytesPerSector;
        var volumeEnd = volumeOffset + volumeSize;

        void AddFreeCluster(long cluster)
        {
            var offset = dataOffset + (cluster - 2) * clusterSize;
            var length = Math.Min(clusterSize, volumeEnd - offset);
            builder.Add(offset, length);
        }

        switch (bpb.FatType)
        {
            case 12:
            {
                var fatBytes = await StreamReadHelper.ReadBytesAt(stream, fatOffset,
                    (int)((bpb.ClusterCount + 2) * 3 / 2 + 2));
                for (long cluster = 2; cluster < bpb.ClusterCount + 2; cluster++)
                {
                    var index = (int)(cluster * 3 / 2);
                    var value = fatBytes[index] | (fatBytes[index + 1] << 8);
                    var entry = (cluster & 1) == 0 ? value & 0xfff : value >> 4;
                    if (entry == 0)
                    {
                        AddFreeCluster(cluster);
                    }
                }

                break;
            }
            case 16:
            {
                var fatBytes = await StreamReadHelper.ReadBytesAt(stream, fatOffset,
                    (int)((bpb.ClusterCount + 2) * 2));
                for (long cluster = 2; cluster < bpb.ClusterCount + 2; cluster++)
                {
                    var index = (int)(cluster * 2);
                    if (fatBytes[index] == 0 && fatBytes[index + 1] == 0)
                    {
                        AddFreeCluster(cluster);
                    }
                }

                break;
            }
            default:
            {
                var buffer = new byte[Fat32ChunkEntries * 4];
                var totalEntries = bpb.ClusterCount + 2;
                for (long chunkStart = 0; chunkStart < totalEntries; chunkStart += Fat32ChunkEntries)
                {
                    var entries = (int)Math.Min(Fat32ChunkEntries, totalEntries - chunkStart);
                    Array.Clear(buffer);
                    await StreamReadHelper.ReadAt(stream, fatOffset + chunkStart * 4, buffer, 0, entries * 4);

                    for (var i = 0; i < entries; i++)
                    {
                        var cluster = chunkStart + i;
                        if (cluster < 2)
                        {
                            continue;
                        }

                        var entry = BitConverter.ToUInt32(buffer, i * 4) & 0x0fffffff;
                        if (entry == 0)
                        {
                            AddFreeCluster(cluster);
                        }
                    }
                }

                break;
            }
        }

        return new FileSystemAllocation($"FAT{bpb.FatType}", builder.Build());
    }

    private static bool TryParseBootSector(byte[] bootSector, long volumeSize, out BiosParameterBlock bpb)
    {
        bpb = null;

        if (bootSector[510] != 0x55 || bootSector[511] != 0xaa)
        {
            return false;
        }

        if (bootSector[0] != 0xeb && bootSector[0] != 0xe9)
        {
            return false;
        }

        var bytesPerSector = BitConverter.ToUInt16(bootSector, 11);
        var sectorsPerCluster = bootSector[13];
        var reservedSectors = BitConverter.ToUInt16(bootSector, 14);
        var numberOfFats = bootSector[16];
        var rootEntries = BitConverter.ToUInt16(bootSector, 17);
        var totalSectors16 = BitConverter.ToUInt16(bootSector, 19);
        var fatSize16 = BitConverter.ToUInt16(bootSector, 22);
        var totalSectors32 = BitConverter.ToUInt32(bootSector, 32);
        var fatSize32 = BitConverter.ToUInt32(bootSector, 36);

        if (bytesPerSector is not (512 or 1024 or 2048 or 4096) ||
            sectorsPerCluster == 0 || (sectorsPerCluster & (sectorsPerCluster - 1)) != 0 ||
            reservedSectors == 0 ||
            numberOfFats is 0 or > 4)
        {
            return false;
        }

        long totalSectors = totalSectors16 != 0 ? totalSectors16 : totalSectors32;
        long fatSize = fatSize16 != 0 ? fatSize16 : fatSize32;
        if (totalSectors == 0 || fatSize == 0 || totalSectors * bytesPerSector > volumeSize)
        {
            return false;
        }

        var rootDirSectors = ((long)rootEntries * 32 + bytesPerSector - 1) / bytesPerSector;
        var firstDataSector = reservedSectors + numberOfFats * fatSize + rootDirSectors;
        if (firstDataSector >= totalSectors)
        {
            return false;
        }

        var clusterCount = (totalSectors - firstDataSector) / sectorsPerCluster;
        var fatType = clusterCount < 4085 ? 12 : clusterCount < 65525 ? 16 : 32;

        // fat32 has no root directory entries and uses 32-bit fat size
        if (fatType == 32 && (rootEntries != 0 || fatSize16 != 0))
        {
            return false;
        }

        // limit cluster count to entries the fat can hold.
        // clusters beyond are not addressable and are treated as used
        var fatEntries = fatType switch
        {
            12 => fatSize * bytesPerSector * 2 / 3,
            16 => fatSize * bytesPerSector / 2,
            _ => fatSize * bytesPerSector / 4
        };
        clusterCount = Math.Min(clusterCount, fatEntries - 2);
        if (clusterCount <= 0)
        {
            return false;
        }

        bpb = new BiosParameterBlock(bytesPerSector, sectorsPerCluster, reservedSectors, firstDataSector,
            clusterCount, fatType);
        return true;
    }

    private record BiosParameterBlock(
        int BytesPerSector,
        int SectorsPerCluster,
        int ReservedSectors,
        long FirstDataSector,
        long ClusterCount,
        int FatType);
}
