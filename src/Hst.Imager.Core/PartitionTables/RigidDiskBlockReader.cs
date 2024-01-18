using System;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils;
using Hst.Imager.Core.Commands;

namespace Hst.Imager.Core.PartitionTables;

public static class RigidDiskBlockReader
{
    public static async Task<PartitionTableInfo> Read(VirtualDisk disk)
    {
        try
        {
            var rigidDiskBlock = await Amiga.RigidDiskBlocks.RigidDiskBlockReader.Read(disk.Content);

            if (rigidDiskBlock == null)
            {
                return null;
            }

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
                Size = rigidDiskBlock.DiskSize,
                Sectors = rigidDiskBlock.DiskSize / rigidDiskBlock.BlockSize,
                Cylinders = rigidDiskBlock.Cylinders,
                Partitions = rigidDiskBlock.PartitionBlocks.Select(x => new PartitionInfo
                {
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
        catch (Exception)
        {
            // ignored, if read rigid disk block fails
        }

        return null;
    }
}