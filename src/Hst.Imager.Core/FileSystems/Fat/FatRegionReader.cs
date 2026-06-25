using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.FileSystems.Fat.Clusters;
using Hst.Imager.Core.FileSystems.Fat.Regions;

namespace Hst.Imager.Core.FileSystems.Fat;

public static class FatRegionReader
{
    /// <summary>
    /// Read fat regions from fat file system.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="partitionOffset"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<IEnumerable<FatRegion>> ReadFatRegions(Stream stream, long partitionOffset = 0,
        CancellationToken cancellationToken = default)
    {
        var fatFileSystem = await FatReader.ReadFatFileSystem(stream, partitionOffset, cancellationToken);
        var bytesPerSector = fatFileSystem.BiosParameterBlock.BytesPerSector;
        
        var fatAreas = new List<FatRegion>(10)
        {
            new(0, fatFileSystem.BiosParameterBlock.ReservedSectorCount *
                   fatFileSystem.BiosParameterBlock.BytesPerSector, FatRegionType.Reserved, "Reserved")
        };

        var fatAreaOffset = fatFileSystem.BiosParameterBlock.ReservedSectorCount * bytesPerSector;
        var fatSizeBytes = fatFileSystem.FatSectors * bytesPerSector;
        for (var fat = 0; fat < fatFileSystem.BiosParameterBlock.NumberOfFats; fat++)
        {
            fatAreas.Add(new FatRegion(fatAreaOffset + (fat * fatSizeBytes), fatSizeBytes, FatRegionType.Fat, $"FAT #{fat + 1}"));
        }

        var rootDirectoryOffset = (fatFileSystem.FirstRootSector * bytesPerSector);
        fatAreas.Add(new FatRegion(rootDirectoryOffset, fatFileSystem.RootSectors * bytesPerSector, FatRegionType.Root,
            "Root directory"));

        var fatBytes = new byte[fatSizeBytes];
        stream.Seek(partitionOffset + fatAreaOffset, SeekOrigin.Begin);
        await stream.ReadExactlyAsync(fatBytes, 0, (int)fatSizeBytes, cancellationToken);
        
        var dataRegionOffset = rootDirectoryOffset + fatFileSystem.RootSectors * bytesPerSector;
        var clusterSize = fatFileSystem.BiosParameterBlock.SectorsPerCluster * bytesPerSector;

        var rootFatEntries = await FatEntryReader.ReadEntries(stream, 
            partitionOffset + rootDirectoryOffset, clusterSize, cancellationToken);
        var nextFatEntries = new Stack<FatEntry>(rootFatEntries);
        
        do
        {
            var fatEntry = nextFatEntries.Pop();
            
            if (fatEntry.Name.Equals(".          ") ||
                (fatEntry.LowFirstCluster == 0 && fatEntry.HighFirstCluster == 0))
            {
                continue;
            }

            var fatEntrySize = 0;
            var clusters = ReadClusterChain(fatFileSystem.FatType, fatBytes, fatEntry.LowFirstCluster,
                fatEntry.HighFirstCluster).ToList();
            
            foreach (var cluster in clusters)
            {
                var isDirectory = (fatEntry.Attribute & 0x10) == 0x10;
                
                var fatAreaSize = !isDirectory && (fatEntry.Size - fatEntrySize) < clusterSize
                    ? Convert.ToInt32(Math.Ceiling((double)(fatEntry.Size - fatEntrySize) / bytesPerSector) * bytesPerSector)
                    : clusterSize;
                var clusterSector = fatFileSystem.FirstDataSector + ((cluster - 2) * fatFileSystem.BiosParameterBlock.SectorsPerCluster);
                fatAreas.Add(new FatRegion(clusterSector * bytesPerSector, fatAreaSize,
                    isDirectory ? FatRegionType.Directory : FatRegionType.File, fatEntry.Name));
                
                if (fatEntry.Name.Equals("..         ") || !isDirectory)
                {
                    fatEntrySize += clusterSize;
                    continue;
                }
            
                var clusterOffset = partitionOffset + dataRegionOffset + ((cluster - 2) * clusterSize);
                var directoryFatEntries = await FatEntryReader.ReadEntries(stream, clusterOffset,
                    clusterSize, cancellationToken);

                foreach (var directoryFatEntry in directoryFatEntries)
                {
                    nextFatEntries.Push(directoryFatEntry);
                }
            }
        } while (nextFatEntries.Count != 0);
        
        return fatAreas;
    }
    
    private static IEnumerable<uint> ReadClusterChain(FatType fatType, byte[] fatBytes, ushort lowFirstCluster,
        ushort highFirstCluster) =>
        fatType switch
        {
            FatType.Fat12 => Fat12Cluster.ReadClusterChain(fatBytes, lowFirstCluster),
            FatType.Fat16 => Fat16Cluster.ReadClusterChain(fatBytes, lowFirstCluster),
            FatType.None => throw new NotSupportedException($"Unsupported FAT type {fatType}"),
            _ => throw new NotSupportedException($"Unsupported FAT type {fatType}")
        };
}