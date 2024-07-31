using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Imager.Core.Commands;
using PartitionInfo = Hst.Imager.Core.Commands.PartitionInfo;

namespace Hst.Imager.Core.PartitionTables;

public static class RigidDiskBlockReader
{
    public static async Task<PartitionTableInfo> Read(VirtualDisk disk)
    {
        try
        {
            disk.Content.Position = 0;
            var rigidDiskBlock = await Amiga.RigidDiskBlocks.RigidDiskBlockReader.Read(disk.Content);

            return rigidDiskBlock == null ? null : Read(disk, rigidDiskBlock);
        }
        catch (Exception)
        {
            // ignored, if read rigid disk block fails
        }

        return null;
    }

    public static PartitionTableInfo Read(VirtualDisk disk, RigidDiskBlock rigidDiskBlock)
    {
        var cylinderSize = rigidDiskBlock.Heads * rigidDiskBlock.Sectors * rigidDiskBlock.BlockSize;
        var rdbPartitionNumber = 0;

        var rdbStartCyl = 0;
        var rdbEndCyl =
            Convert.ToInt32(Math.Ceiling((double)rigidDiskBlock.RdbBlockHi * rigidDiskBlock.BlockSize /
                                         cylinderSize)) - 1;

        var rdbStartOffset = rigidDiskBlock.RdbBlockLo * rigidDiskBlock.BlockSize;
        var rdbEndOffset = ((rigidDiskBlock.RdbBlockHi + 1) * rigidDiskBlock.BlockSize) - 1;
        return new PartitionTableInfo
        {
            Type = PartitionTableType.RigidDiskBlock,
            DiskGeometry = new DiskGeometryInfo
            {
                BytesPerSector = disk.Geometry.BytesPerSector,
                Cylinders = disk.Geometry.Cylinders,
                Capacity = disk.Geometry.Capacity,
                HeadsPerCylinder = disk.Geometry.HeadsPerCylinder,
                SectorsPerTrack = disk.Geometry.SectorsPerTrack,
                TotalSectors = disk.Geometry.TotalSectorsLong,
            },
            Size = rigidDiskBlock.DiskSize,
            Sectors = rigidDiskBlock.DiskSize / rigidDiskBlock.BlockSize,
            Cylinders = rigidDiskBlock.Cylinders,
            Partitions = rigidDiskBlock.PartitionBlocks.Select(x => new PartitionInfo
            {
                PartitionType = x.DosTypeFormatted,
                PartitionNumber = ++rdbPartitionNumber,
                FileSystem = x.DosTypeFormatted,
                Size = x.PartitionSize,
                StartOffset = (long)x.LowCyl * cylinderSize,
                EndOffset = ((long)x.HighCyl + 1) * cylinderSize - 1,
                StartSector = (long)x.LowCyl * rigidDiskBlock.Heads * rigidDiskBlock.Sectors,
                EndSector = (long)x.HighCyl * rigidDiskBlock.Heads * rigidDiskBlock.Sectors,
                StartCylinder = x.LowCyl,
                EndCylinder = x.HighCyl,
            }).ToList(),
            Reserved = new PartitionTableReservedInfo
            {
                StartOffset = rdbStartOffset,
                EndOffset = rdbEndOffset,
                StartSector = rigidDiskBlock.RdbBlockLo,
                EndSector = rigidDiskBlock.RdbBlockHi,
                StartCylinder = rdbStartCyl,
                EndCylinder = rdbEndCyl,
                Size = rdbEndOffset - rdbStartOffset + 1
            },
            StartOffset = rdbStartOffset,
            EndOffset = rdbStartOffset + rigidDiskBlock.DiskSize - 1,
            StartCylinder = 0,
            EndCylinder = rigidDiskBlock.HiCylinder,
            StartSector = rigidDiskBlock.RdbBlockLo,
            EndSector = rigidDiskBlock.DiskSize / rigidDiskBlock.BlockSize,
        };
    }

    private static string FormatFileSystem(RigidDiskBlock rigidDiskBlock, byte[] dosType)
    {
        var fileSystemHeaderBlock =
            rigidDiskBlock.FileSystemHeaderBlocks.FirstOrDefault(x => x.DosType.SequenceEqual(dosType));

        if (fileSystemHeaderBlock == null)
        {
            return string.Empty;
        }

        return $"{Path.GetFileName(fileSystemHeaderBlock.FileSystemName)} {fileSystemHeaderBlock.VersionFormatted}";
    }
}