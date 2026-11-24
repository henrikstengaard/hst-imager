using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Imager.Core.Optimization.FileSystems;

namespace Hst.Imager.Core.Optimization;

/// <summary>
/// Examines partition tables and file systems on a disk to determine free and used sectors.
/// Supports Rigid Disk Block, Master Boot Record and Guid Partition Table partitions as well as
/// disks without partition table (e.g. partition hardfiles and floppy images).
/// Free sectors are read from FAT12/16/32, exFAT, OFS/FFS (DOS0 - DOS7) and PFS3 file systems.
/// Volumes with unknown file systems and areas outside partitions are considered used.
/// </summary>
public class DiskAllocationAnalyzer
{
    private const int SectorSize = 512;
    private const byte PiStormRdbBiosType = 0x76;
    private static readonly byte[] ExtendedBiosTypes = [0x05, 0x0f, 0x85];

    public event EventHandler<string> DebugMessage;

    public async Task<DiskAllocation> Analyze(Stream stream, long size)
    {
        var volumes = new List<VolumeAllocation>();

        if (!await TryAnalyzeRigidDiskBlock(stream, 0, size, string.Empty, volumes) &&
            !await TryAnalyzePartitionTable(stream, size, volumes))
        {
            volumes.Add(await AnalyzeVolume(stream, "Disk", 0, size, null));
        }

        var allocationMap = new AllocationMap();
        foreach (var volume in volumes.Where(x => x.IsKnownFileSystem))
        {
            allocationMap.AddFree(volume.FileSystemAllocation.FreeRanges);
        }

        return new DiskAllocation(allocationMap, volumes);
    }

    private async Task<bool> TryAnalyzeRigidDiskBlock(Stream stream, long offset, long size, string namePrefix,
        ICollection<VolumeAllocation> volumes)
    {
        RigidDiskBlock rigidDiskBlock;
        try
        {
            var rdbStream = offset == 0 ? stream : new SubStream(stream, Ownership.None, offset, size);
            rigidDiskBlock = await RigidDiskBlockReader.Read(rdbStream);
        }
        catch (Exception e)
        {
            OnDebugMessage($"Failed to read Rigid Disk Block: {e.Message}");
            return false;
        }

        if (rigidDiskBlock == null)
        {
            return false;
        }

        OnDebugMessage($"{namePrefix}Rigid Disk Block found");

        foreach (var partitionBlock in rigidDiskBlock.PartitionBlocks ?? [])
        {
            var name = $"{namePrefix}Rigid Disk Block partition {partitionBlock.DriveName}";
            var blockSize = (long)partitionBlock.BlockSize;
            var cylinderSize = (long)partitionBlock.Surfaces * partitionBlock.BlocksPerTrack * blockSize;
            var partitionOffset = offset + partitionBlock.LowCyl * cylinderSize;
            var partitionSize = ((long)partitionBlock.HighCyl - partitionBlock.LowCyl + 1) * cylinderSize;

            if (!TryClipVolume(name, partitionOffset, ref partitionSize, offset + size))
            {
                continue;
            }

            volumes.Add(await AnalyzeVolume(stream, name, partitionOffset, partitionSize,
                new AmigaVolumeParameters(partitionBlock.Reserved, (int)partitionBlock.FileSystemBlockSize,
                    (int)partitionBlock.BlockSize)));
        }

        return true;
    }

    private async Task<bool> TryAnalyzePartitionTable(Stream stream, long size, ICollection<VolumeAllocation> volumes)
    {
        BiosPartitionTable biosPartitionTable;
        try
        {
            stream.Position = 0;
            if (!BiosPartitionTable.IsValid(stream))
            {
                return false;
            }

            biosPartitionTable = new BiosPartitionTable(stream, Geometry.FromCapacity(size, SectorSize));
        }
        catch (Exception e)
        {
            OnDebugMessage($"Failed to read Master Boot Record: {e.Message}");
            return false;
        }

        var biosPartitions = biosPartitionTable.Partitions.OfType<BiosPartitionInfo>().ToList();

        if (biosPartitions.Any(x => x.BiosType == BiosPartitionTypes.GptProtective))
        {
            return await TryAnalyzeGuidPartitionTable(stream, size, volumes);
        }

        OnDebugMessage("Master Boot Record found");

        var partitionNumber = 0;
        foreach (var partition in biosPartitions)
        {
            partitionNumber++;
            if (ExtendedBiosTypes.Contains(partition.BiosType))
            {
                continue;
            }

            var name = $"Master Boot Record partition {partitionNumber}";
            var partitionOffset = partition.FirstSector * SectorSize;
            var partitionSize = partition.SectorCount * SectorSize;

            if (!TryClipVolume(name, partitionOffset, ref partitionSize, size))
            {
                continue;
            }

            if (partition.BiosType == PiStormRdbBiosType &&
                await TryAnalyzeRigidDiskBlock(stream, partitionOffset, partitionSize, $"{name}, ", volumes))
            {
                continue;
            }

            volumes.Add(await AnalyzeVolume(stream, name, partitionOffset, partitionSize, null));
        }

        return true;
    }

    private async Task<bool> TryAnalyzeGuidPartitionTable(Stream stream, long size,
        ICollection<VolumeAllocation> volumes)
    {
        DiscUtils.Partitions.GuidPartitionTable guidPartitionTable;
        try
        {
            stream.Position = 0;
            guidPartitionTable = new DiscUtils.Partitions.GuidPartitionTable(stream,
                Geometry.FromCapacity(size, SectorSize));
        }
        catch (Exception e)
        {
            OnDebugMessage($"Failed to read Guid Partition Table: {e.Message}");
            return false;
        }

        OnDebugMessage("Guid Partition Table found");

        var partitionNumber = 0;
        foreach (var partition in guidPartitionTable.Partitions)
        {
            partitionNumber++;
            var name = $"Guid Partition Table partition {partitionNumber}";
            var partitionOffset = partition.FirstSector * SectorSize;
            var partitionSize = partition.SectorCount * SectorSize;

            if (!TryClipVolume(name, partitionOffset, ref partitionSize, size))
            {
                continue;
            }

            volumes.Add(await AnalyzeVolume(stream, name, partitionOffset, partitionSize, null));
        }

        return true;
    }

    private bool TryClipVolume(string name, long offset, ref long size, long diskSize)
    {
        if (offset >= diskSize || size <= 0)
        {
            OnDebugMessage($"Skipping {name} outside disk");
            return false;
        }

        if (offset + size > diskSize)
        {
            OnDebugMessage($"Clipping {name} to disk size");
            size = diskSize - offset;
        }

        return true;
    }

    private async Task<VolumeAllocation> AnalyzeVolume(Stream stream, string name, long offset, long size,
        AmigaVolumeParameters amigaVolumeParameters)
    {
        var parameters = amigaVolumeParameters ?? new AmigaVolumeParameters(2, SectorSize, SectorSize);

        var readers = new List<Func<Task<FileSystemAllocation>>>
        {
            () => FastFileSystemAllocationReader.Read(stream, offset, size, parameters.Reserved,
                parameters.FileSystemBlockSize),
            () => Pfs3AllocationReader.Read(stream, offset, size, parameters.BlockSize),
            () => SmartFileSystemAllocationReader.Read(stream, offset, size),
            () => ExFatAllocationReader.Read(stream, offset, size),
            () => FatAllocationReader.Read(stream, offset, size)
        };

        foreach (var reader in readers)
        {
            FileSystemAllocation fileSystemAllocation;
            try
            {
                fileSystemAllocation = await reader();
            }
            catch (Exception e)
            {
                OnDebugMessage($"Failed to read file system allocation for {name}: {e.Message}");
                return new VolumeAllocation
                {
                    Name = name,
                    Offset = offset,
                    Size = size,
                    Message = e.Message
                };
            }

            if (fileSystemAllocation == null)
            {
                continue;
            }

            // free ranges must be within volume
            var freeRanges = fileSystemAllocation.FreeRanges
                .Where(x => x.Offset >= offset && x.End <= offset + size)
                .ToList();
            if (freeRanges.Count != fileSystemAllocation.FreeRanges.Count)
            {
                OnDebugMessage($"Ignoring free ranges outside {name}");
                fileSystemAllocation = new FileSystemAllocation(fileSystemAllocation.FileSystem, freeRanges);
            }

            return new VolumeAllocation
            {
                Name = name,
                Offset = offset,
                Size = size,
                FileSystemAllocation = fileSystemAllocation
            };
        }

        return new VolumeAllocation
        {
            Name = name,
            Offset = offset,
            Size = size,
            Message = "Unknown file system"
        };
    }

    private void OnDebugMessage(string message) => DebugMessage?.Invoke(this, message);

    private record AmigaVolumeParameters(uint Reserved, int FileSystemBlockSize, int BlockSize);
}
