using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Partitions;
using Hst.Imager.Core.Commands;
using PartitionInfo = Hst.Imager.Core.Commands.PartitionInfo;

namespace Hst.Imager.Core.PartitionTables;

public static class MbrPartitionTableReader
{
    public static BiosPartitionTable Read(VirtualDisk disk)
    {
        try
        {
            disk.Content.Position = 0;
            return new BiosPartitionTable(disk);
        }
        catch (Exception)
        {
            // ignored, if read bios partition table fails
        }

        return null;
    }

    public static async Task<PartitionTableInfo> Read(VirtualDisk disk, BiosPartitionTable biosPartitionTable)
    {
        var mbrPartitionNumber = 0;
        var mbrPartitions = new List<PartitionInfo>();
        foreach (var partition in biosPartitionTable.Partitions.OfType<BiosPartitionInfo>())
        {
            mbrPartitions.Add(await ReadMbrPartitionInfo(++mbrPartitionNumber, partition, disk.BlockSize));
        }

        return new PartitionTableInfo
        {
            Type = PartitionTableType.MasterBootRecord,
            DiskGeometry = new DiskGeometryInfo
            {
                Capacity = disk.Geometry.Capacity,
                TotalSectors = biosPartitionTable.DiskGeometry.TotalSectorsLong,
                BytesPerSector = biosPartitionTable.DiskGeometry.BytesPerSector,
                HeadsPerCylinder = biosPartitionTable.DiskGeometry.HeadsPerCylinder,
                Cylinders = biosPartitionTable.DiskGeometry.Cylinders,
                SectorsPerTrack = biosPartitionTable.DiskGeometry.SectorsPerTrack
            },
            Size = disk.Geometry.Capacity,
            Sectors = disk.Geometry.TotalSectorsLong,
            Cylinders = 0,
            Partitions = mbrPartitions,
            Reserved = new PartitionTableReservedInfo
            {
                StartOffset = 0,
                EndOffset = 511,
                StartSector = 0,
                EndSector = 0,
                StartCylinder = 0,
                EndCylinder = 0,
                Size = 512
            },
            StartOffset = 0,
            EndOffset = disk.Geometry.Capacity - 1,
            StartSector = 0,
            EndSector = disk.Geometry.TotalSectorsLong - 1
        };
    }
    
    private static async Task<PartitionInfo> ReadMbrPartitionInfo(int mbrPartitionNumber,
        BiosPartitionInfo biosPartitionInfo, int blockSize)
    {
        var fileSystemInfo = await FileSystemReader.ReadFileSystem(biosPartitionInfo);

        var partitionType = await ReadPartitionType(biosPartitionInfo);
        
        return new PartitionInfo
        {
            IsActive = biosPartitionInfo.IsActive,
            IsPrimary = biosPartitionInfo.IsPrimary,
            PartitionNumber = mbrPartitionNumber,
            PartitionType = partitionType,
            FileSystem = fileSystemInfo?.FileSystemType,
            Size = biosPartitionInfo.SectorCount * blockSize,
            StartOffset = biosPartitionInfo.FirstSector * blockSize,
            EndOffset = ((biosPartitionInfo.LastSector + 1) * blockSize) - 1,
            StartSector = biosPartitionInfo.FirstSector,
            EndSector = biosPartitionInfo.LastSector,
            StartChs = new ChsAddressInfo
            {
                Cylinder = biosPartitionInfo.Start.Cylinder,
                Head = biosPartitionInfo.Start.Head,
                Sector = biosPartitionInfo.Start.Sector
            },
            EndChs = new ChsAddressInfo
            {
                Cylinder = biosPartitionInfo.End.Cylinder,
                Head = biosPartitionInfo.End.Head,
                Sector = biosPartitionInfo.End.Sector
            },
            VolumeSize = fileSystemInfo?.VolumeSize,
            VolumeFree = fileSystemInfo?.VolumeFree,
            BiosType = biosPartitionInfo.BiosType
        };
    }

    private static async Task<string> ReadPartitionType(DiscUtils.Partitions.PartitionInfo partitionInfo)
    {
        if (partitionInfo.BiosType != 0x76)
        {
            return partitionInfo.TypeAsString;
        }
        
        await using var stream = partitionInfo.Open();
        
        var rigidDiskBlock = await Amiga.RigidDiskBlocks.RigidDiskBlockReader.Read(stream);

        return rigidDiskBlock != null ? "PiStorm RDB" : partitionInfo.TypeAsString;
    }
}