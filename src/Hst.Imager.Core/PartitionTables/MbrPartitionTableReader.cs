using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Partitions;
using Hst.Imager.Core.Commands;
using PartitionInfo = Hst.Imager.Core.Commands.PartitionInfo;

namespace Hst.Imager.Core.PartitionTables;

public static class MbrPartitionTableReader
{
    public static async Task<PartitionTableInfo> Read(VirtualDisk disk)
    {
        try
        {
            var biosPartitionTable = new BiosPartitionTable(disk);

            var mbrPartitionNumber = 0;
            var mbrPartitions = new List<PartitionInfo>();
            foreach (var partition in biosPartitionTable.Partitions)
            {
                mbrPartitions.Add(await ReadMbrPartitionInfo(++mbrPartitionNumber, partition, disk.BlockSize));
            }

            return new PartitionTableInfo
            {
                Type = PartitionTableType.MasterBootRecord,
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
        catch (Exception)
        {
            // ignored, if read bios partition table fails
        }

        return null;
    }
    
    private static async Task<PartitionInfo> ReadMbrPartitionInfo(int mbrPartitionNumber,
        DiscUtils.Partitions.PartitionInfo partitionInfo, int blockSize)
    {
        return new PartitionInfo
        {
            PartitionNumber = ++mbrPartitionNumber,
            PartitionType = partitionInfo.TypeAsString,
            FileSystem = (await FileSystemReader.ReadFileSystem(partitionInfo)),
            BiosType = partitionInfo.BiosType.ToString(),
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