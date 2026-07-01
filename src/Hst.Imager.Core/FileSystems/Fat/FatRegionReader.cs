using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    /// <param name="stream">Stream to read fat file sytstem from.</param>
    /// <param name="partitionOffset">Partition offset where fat partition starts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public static async Task<IEnumerable<FatRegion>> ReadFatRegions(Stream stream, long partitionOffset = 0,
        CancellationToken cancellationToken = default)
    {
        var fatFileSystem = await FatReader.ReadFatFileSystem(stream, partitionOffset, cancellationToken);
        var bytesPerSector = fatFileSystem.BiosParameterBlock.BytesPerSector;
        
        var fatRegions = new List<FatRegion>(10)
        {
            new(0, fatFileSystem.BiosParameterBlock.ReservedSectorCount *
                   fatFileSystem.BiosParameterBlock.BytesPerSector, FatRegionType.Reserved, "Reserved")
        };

        var fatRegionOffset = fatFileSystem.BiosParameterBlock.ReservedSectorCount * bytesPerSector;
        var fatSizeBytes = fatFileSystem.FatSectors * bytesPerSector;
        for (var fat = 0; fat < fatFileSystem.BiosParameterBlock.NumberOfFats; fat++)
        {
            fatRegions.Add(new FatRegion(fatRegionOffset + (fat * fatSizeBytes), fatSizeBytes, FatRegionType.Fat, $"FAT #{fat + 1}"));
        }

        var fatBytes = new byte[fatSizeBytes];
        stream.Seek(partitionOffset + fatRegionOffset, SeekOrigin.Begin);
        await stream.ReadExactlyAsync(fatBytes, 0, (int)fatSizeBytes, cancellationToken);

        fatRegions.AddRange(GetRootDirectoryRegions(fatFileSystem, fatBytes));
        
        var clusterSize = fatFileSystem.BiosParameterBlock.SectorsPerCluster * bytesPerSector;

        var rootFatEntries = await FatEntryReader.ReadRootEntries(stream, partitionOffset, fatFileSystem, fatBytes, cancellationToken)
            .ToListAsync(cancellationToken);
        var nextFatEntries = new Stack<IFatEntry>(rootFatEntries.SelectMany(x => x));
        
        do
        {
            var fatEntry = nextFatEntries.Pop();

            var fatShortEntry = fatEntry as FatEntry;
            if (fatShortEntry == null)
            {
                continue;
            }
            
            var name = Encoding.ASCII.GetString(fatShortEntry.Name, 0, 11);
            
            if (name.Equals(Constants.CurrentDirectory) ||
                (fatShortEntry.LowFirstCluster == 0 && fatShortEntry.HighFirstCluster == 0))
            {
                continue;
            }

            var isDirectory = (fatShortEntry.Attribute & 0x10) == 0x10;
            
            long fatEntryOffset = 0;
            long fatEntrySize = 0;
            
            var clusters = FatCluster.ReadClusterChain(fatFileSystem.FatType, fatBytes,
                ((uint)fatShortEntry.HighFirstCluster << 16) | fatShortEntry.LowFirstCluster).ToList();
            
            foreach (var cluster in clusters)
            {
                var clusterSector = fatFileSystem.FirstDataSector + ((cluster - Constants.ReservedClusters) *
                                                                     fatFileSystem.BiosParameterBlock.SectorsPerCluster);
                var clusterOffset =  clusterSector * bytesPerSector;
                
                if (fatEntryOffset == 0)
                {
                    fatEntryOffset = clusterOffset;
                }

                var fatEntryClusterSize = !isDirectory && fatShortEntry.Size - fatEntrySize < clusterSize
                    ? Convert.ToInt32(Math.Ceiling((double)(fatShortEntry.Size - fatEntrySize) / bytesPerSector) * bytesPerSector)
                    : clusterSize;

                if (fatShortEntry.Name.Equals(Constants.ParentDirectory) || !isDirectory)
                {
                    if (fatEntryOffset != clusterOffset && fatEntryOffset + fatEntrySize != clusterOffset)
                    {
                        fatRegions.Add(new FatRegion(fatEntryOffset, fatEntrySize, FatRegionType.File,
                            name));
                        fatEntryOffset = clusterOffset;
                        fatEntrySize = 0;
                    }

                    fatEntrySize += fatEntryClusterSize;
                    
                    continue;
                }

                var directoryFatEntries = await FatEntryReader.ReadEntries(stream,
                    partitionOffset + clusterOffset, clusterSize, cancellationToken);

                foreach (var directoryFatEntry in directoryFatEntries)
                {
                    nextFatEntries.Push(directoryFatEntry);
                }
            }
            
            fatRegions.Add(new FatRegion(fatEntryOffset, fatEntrySize,
                isDirectory ? FatRegionType.Directory : FatRegionType.File, name));
        } while (nextFatEntries.Count != 0);
        
        return fatRegions;
    }

    private static IEnumerable<FatRegion> GetRootDirectoryRegions(FatFileSystem fatFileSystem, byte[] fatBytes)
    {
        var bytesPerSector = fatFileSystem.BiosParameterBlock.BytesPerSector;

        if (fatFileSystem.RootDirSectors != 0)
        {
            long rootDirectoryOffset = fatFileSystem.FirstRootSector * bytesPerSector;

            yield return new FatRegion(rootDirectoryOffset, fatFileSystem.RootDirSectors * bytesPerSector,
                FatRegionType.Root, "Root directory");

            yield break;
        }

        var clusterSize = fatFileSystem.BiosParameterBlock.SectorsPerCluster * bytesPerSector;
        var clusters = FatCluster.ReadClusterChain(fatFileSystem.FatType, fatBytes,
            fatFileSystem.ExtendedBiosParameterBlock.RootCluster).ToList();
        
        foreach (var cluster in clusters)
        {
            var clusterSector = fatFileSystem.FirstDataSector + ((cluster - Constants.ReservedClusters) * 
                                                                 fatFileSystem.BiosParameterBlock .SectorsPerCluster);
            var clusterOffset = clusterSector * bytesPerSector;
            
            yield return new FatRegion(clusterOffset, clusterSize, FatRegionType.Root, "Root directory");
        }
    }
}