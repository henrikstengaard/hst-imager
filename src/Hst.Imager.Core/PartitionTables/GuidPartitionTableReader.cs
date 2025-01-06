using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using PartitionInfo = Hst.Imager.Core.Commands.PartitionInfo;

namespace Hst.Imager.Core.PartitionTables;

public static class GuidPartitionTableReader
{
    private static readonly Lazy<GuidPartitionTypeRegister> GuidPartitionTypeRegister =
        new Lazy<GuidPartitionTypeRegister>(
            () =>
            {
                var register = new GuidPartitionTypeRegister();
                register.AddDefault();
                return register;
            }, LazyThreadSafetyMode.None);

    public static DiscUtils.Partitions.GuidPartitionTable Read(VirtualDisk disk)
    {
        try
        {
            disk.Content.Position = 0;
            return new DiscUtils.Partitions.GuidPartitionTable(disk);
        }
        catch (Exception)
        {
            // ignored, if read guid partition table fails
        }

        return null;
    }

    public static async Task<PartitionTableInfo> Read(VirtualDisk disk, DiscUtils.Partitions.GuidPartitionTable guidPartitionTable)
    {
        var totalSectors = disk.Capacity / disk.SectorSize;

        var guidPartitionNumber = 0;
        var gptPartitions = new List<PartitionInfo>();
        foreach (var partition in guidPartitionTable.Partitions)
        {
            gptPartitions.Add(await ReadGptPartitionInfo(++guidPartitionNumber, disk, partition));
        }

        var guidReservedSize = guidPartitionTable.FirstUsableSector * disk.BlockSize;
        return new PartitionTableInfo
        {
            Type = PartitionTableType.GuidPartitionTable,
            DiskGeometry = new DiskGeometryInfo
            {
                BytesPerSector = disk.Geometry.Value.BytesPerSector,
                Cylinders = disk.Geometry.Value.Cylinders,
                Capacity = disk.Geometry.Value.Capacity,
                HeadsPerCylinder = disk.Geometry.Value.HeadsPerCylinder,
                SectorsPerTrack = disk.Geometry.Value.SectorsPerTrack,
                TotalSectors = totalSectors,
            },
            Size = disk.Capacity,
            Sectors = guidPartitionTable.LastUsableSector + 1,
            Cylinders = 0,
            Partitions = gptPartitions,
            Reserved = new PartitionTableReservedInfo
            {
                StartOffset = 0,
                EndOffset = guidReservedSize - 1,
                StartSector = 0,
                EndSector = guidPartitionTable.FirstUsableSector > 0
                    ? guidPartitionTable.FirstUsableSector - 1
                    : 0,
                StartCylinder = 0,
                EndCylinder = 0,
                Size = guidReservedSize
            },
            StartOffset = 0,
            EndOffset = disk.Capacity - 1,
            StartSector = guidPartitionTable.FirstUsableSector,
            EndSector = guidPartitionTable.LastUsableSector,
            StartCylinder = 0,
            EndCylinder = 0
        };
    }

    private static async Task<PartitionInfo> ReadGptPartitionInfo(int guidPartitionNumber,
        VirtualDisk disk, DiscUtils.Partitions.PartitionInfo partitionInfo)
    {
        var partitionType = GuidPartitionTypeRegister.Value.TryGet(partitionInfo.GuidType, out var guidPartitionType)
            ? guidPartitionType.PartitionType
            : partitionInfo.TypeAsString;

        var fileSystemInfo = await FileSystemReader.ReadFileSystem(disk, partitionInfo);
        
        return new PartitionInfo
        {
            PartitionNumber = guidPartitionNumber,
            PartitionType = partitionType,
            FileSystem = fileSystemInfo?.FileSystemType,
            Size = partitionInfo.SectorCount * disk.BlockSize,
            StartOffset = partitionInfo.FirstSector * disk.BlockSize,
            EndOffset = ((partitionInfo.LastSector + 1) * disk.BlockSize) - 1,
            StartSector = partitionInfo.FirstSector,
            EndSector = partitionInfo.LastSector,
            StartCylinder = 0,
            EndCylinder = 0,
            VolumeSize = fileSystemInfo?.VolumeSize,
            VolumeFree = fileSystemInfo?.VolumeFree,
            GuidType = partitionInfo.GuidType,
        };
    }
}