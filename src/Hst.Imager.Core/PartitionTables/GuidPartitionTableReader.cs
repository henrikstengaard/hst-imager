using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Partitions;
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

    public static async Task<PartitionTableInfo> Read(VirtualDisk disk)
    {
        try
        {
            var guidPartitionTable = new GuidPartitionTable(disk);

            var guidPartitionNumber = 0;
            var gptPartitions = new List<PartitionInfo>();
            foreach (var partition in guidPartitionTable.Partitions)
            {
                gptPartitions.Add(await ReadGptPartitionInfo(++guidPartitionNumber, partition, disk.BlockSize));
            }

            var guidReservedSize = guidPartitionTable.FirstUsableSector * disk.BlockSize;
            return new PartitionTableInfo
            {
                Type = PartitionTableType.GuidPartitionTable,
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
        catch (Exception)
        {
            // ignored, if read guid partition table fails
        }

        return null;
    }

    private static async Task<PartitionInfo> ReadGptPartitionInfo(int guidPartitionNumber,
        DiscUtils.Partitions.PartitionInfo partitionInfo, int blockSize)
    {
        var partitionType = GuidPartitionTypeRegister.Value.TryGet(partitionInfo.GuidType, out var guidPartitionType)
            ? guidPartitionType.PartitionType
            : partitionInfo.TypeAsString;

        return new PartitionInfo
        {
            PartitionNumber = guidPartitionNumber,
            PartitionType = partitionType,
            FileSystem = (await FileSystemReader.ReadFileSystem(partitionInfo)),
            BiosType = partitionInfo.BiosType.ToString(),
            GuidType = partitionInfo.GuidType.ToString(),
            Size = partitionInfo.SectorCount * blockSize,
            StartOffset = partitionInfo.FirstSector * blockSize,
            EndOffset = ((partitionInfo.LastSector + 1) * blockSize) - 1,
            StartSector = partitionInfo.FirstSector,
            EndSector = partitionInfo.LastSector,
            StartCylinder = 0,
            EndCylinder = 0
        };
    }
}