using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hst.Imager.Core.FileSystems.Fat;

public static class FatReader
{
    public static async Task<FatFileSystem> ReadFatFileSystem(Stream stream, long partitionOffset = 0,
        CancellationToken cancellationToken = default)
    {
        if (stream.Seek(partitionOffset, SeekOrigin.Begin) != partitionOffset)
        {
            throw new IOException($"Failed to seek to partition offset {partitionOffset}");
        }

        var sectorData = new byte[512];
        await stream.ReadExactlyAsync(sectorData, cancellationToken);

        var biosParameterBlock = BiosParameterBlockReader.Read(sectorData);

        var totalSectors = biosParameterBlock.TotalSectors != 0
            ? (long)biosParameterBlock.TotalSectors
            : biosParameterBlock.TotalSectorsFat32;

        var extendedBiosParameterBlock = biosParameterBlock.FatSectors != 0
            ? ExtendedBiosParameterBlockReader.Read(sectorData)
            : ExtendedBiosParameterBlockFat32Reader.Read(sectorData);

        var fatSectors = biosParameterBlock.FatSectors != 0
            ? biosParameterBlock.FatSectors
            : extendedBiosParameterBlock.FatSectorsFat32;

        var firstRootSector = biosParameterBlock.ReservedSectorCount + (biosParameterBlock.NumberOfFats * fatSectors);

        var rootSectors = (biosParameterBlock.RootEntriesCount * Constants.FatEntrySize +
                              biosParameterBlock.BytesPerSector - 1) /
                          biosParameterBlock.BytesPerSector;

        var firstDataSector = firstRootSector + rootSectors;

        var dataSectors = totalSectors - firstDataSector;

        var clusterCount = dataSectors / biosParameterBlock.SectorsPerCluster;

        var fatType = GetFatType(clusterCount);

        return new FatFileSystem(biosParameterBlock, extendedBiosParameterBlock, totalSectors, fatSectors,
            firstRootSector, rootSectors, firstDataSector, dataSectors, clusterCount, fatType);
    }

    /// <summary>
    /// Get fat type from number of clusters.
    /// </summary>
    /// <param name="clusterCount">Cluster count.</param>
    /// <returns>Fat type.</returns>
    private static FatType GetFatType(long clusterCount)
    {
        return clusterCount switch
        {
            <= 4085 => FatType.Fat12,
            <= 65525 => FatType.Fat16,
            _ => FatType.Fat32
        };
    }
}