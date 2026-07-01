using System;
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

        var rootDirSectors = (biosParameterBlock.RootEntriesCount * Constants.FatEntrySize +
            biosParameterBlock.BytesPerSector - 1) / biosParameterBlock.BytesPerSector;

        var extendedBiosParameterBlock = biosParameterBlock.FatSectors != 0
            ? ExtendedBiosParameterBlockReader.Read(sectorData)
            : ExtendedBiosParameterBlockFat32Reader.Read(sectorData);

        var fatSectors = biosParameterBlock.FatSectors != 0
            ? biosParameterBlock.FatSectors
            : extendedBiosParameterBlock.FatSectorsFat32;

        long firstDataSector = biosParameterBlock.ReservedSectorCount + (biosParameterBlock.NumberOfFats * fatSectors);

        var totalSectors = biosParameterBlock.TotalSectors != 0
            ? (long)biosParameterBlock.TotalSectors
            : biosParameterBlock.TotalSectorsFat32;

        var dataSectors = totalSectors - firstDataSector + rootDirSectors;

        var clusterCount = Convert.ToInt64(Math.Floor((double)dataSectors / biosParameterBlock.SectorsPerCluster));

        var firstRootSector = firstDataSector;
        
        // first data sector starts after root directory sectors for fat12/fat16.
        firstDataSector += rootDirSectors;

        var fatType = GetFatType(clusterCount);

        if (biosParameterBlock.RootEntriesCount == 0)
        {
            firstRootSector += (extendedBiosParameterBlock.RootCluster - Constants.ReservedClusters) *
                               biosParameterBlock.SectorsPerCluster;
        }
        
        return new FatFileSystem(biosParameterBlock, extendedBiosParameterBlock, totalSectors, fatSectors,
            rootDirSectors, firstRootSector, firstDataSector, dataSectors, clusterCount, fatType);
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