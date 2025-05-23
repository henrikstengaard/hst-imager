﻿using Hst.Imager.Core.PartitionTables;

namespace Hst.Imager.ConsoleApp.Presenters
{
    using System.Linq;
    using System.Text;
    using Amiga.Extensions;
    using Core.Commands;
    using Core.Extensions;
    using Hst.Core.Extensions;

    public static class RigidDiskBlockPresenter
    {
        public static string Present(MediaInfo mediaInfo, bool showUnallocated)
        {
            if (mediaInfo == null || mediaInfo.DiskInfo == null || mediaInfo.DiskInfo.RigidDiskBlock == null)
            {
                return "No Rigid Disk Block present";
            }

            var rigidDiskBlock = mediaInfo.DiskInfo.RigidDiskBlock; 
            
            var outputBuilder = new StringBuilder();
            
            outputBuilder.AppendLine($"Rigid Disk Block info read from '{mediaInfo.Path}':");
            outputBuilder.AppendLine();
            outputBuilder.AppendLine("Rigid Disk Block:");
            var rigidDiskBlockTable = new Table
            {
                Columns = new[]
                {
                    new Column { Name = "Product" },
                    new Column { Name = "Vendor" },
                    new Column { Name = "Revision" },
                    new Column { Name = "Size", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Cylinders", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Heads", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Sectors", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Block Size", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Flags", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Host Id", Alignment = ColumnAlignment.Right },
                    new Column { Name = "RDB Block Lo", Alignment = ColumnAlignment.Right },
                    new Column { Name = "RDB Block Hi", Alignment = ColumnAlignment.Right }
                },
                Rows = new []{ new Row
                    {
                        Columns = new[]
                        {
                            rigidDiskBlock.DiskProduct,
                            rigidDiskBlock.DiskVendor,
                            rigidDiskBlock.DiskRevision,
                            rigidDiskBlock.DiskSize.FormatBytes(),
                            rigidDiskBlock.Cylinders.ToString(),
                            rigidDiskBlock.Heads.ToString(),
                            rigidDiskBlock.Sectors.ToString(),
                            rigidDiskBlock.BlockSize.ToString(),
                            $"{rigidDiskBlock.Flags.ToString()} *",
                            rigidDiskBlock.HostId.ToString(),
                            rigidDiskBlock.RdbBlockLo.ToString(),
                            rigidDiskBlock.RdbBlockHi.ToString()
                        }
                    }}
                    .ToList()
            };

            var flags = (RigidDiskBlockFlagsEnum)rigidDiskBlock.Flags;

            outputBuilder.AppendLine();
            outputBuilder.Append(TablePresenter.Present(rigidDiskBlockTable));
            outputBuilder.AppendLine();
            outputBuilder.AppendLine($"* Flags {rigidDiskBlock.Flags} = {flags}");
            
            // file systems

            outputBuilder.AppendLine();
            outputBuilder.AppendLine("File systems:");
            var fileSystemNumber = 0;
            var fileSystemTable = new Table
            {
                Columns = new[]
                {
                    new Column { Name = "#" },
                    new Column { Name = "DOS Type" },
                    new Column { Name = "Version" },
                    new Column { Name = "Name" },
                    new Column { Name = "Size", Alignment = ColumnAlignment.Right }
                },
                Rows = rigidDiskBlock.FileSystemHeaderBlocks.Select(x => new Row
                    {
                        Columns = new[]
                        {
                            (++fileSystemNumber).ToString(),
                            $"0x{x.DosType.FormatHex().ToUpper()} ({x.DosType.FormatDosType()})",
                            x.VersionFormatted,
                            x.FileSystemName ?? string.Empty,
                            ((long)x.FileSystemSize).FormatBytes()
                        }
                    })
                    .ToList()
            };

            outputBuilder.AppendLine();
            outputBuilder.Append(TablePresenter.Present(fileSystemTable));
            
            // partitions 
            
            outputBuilder.AppendLine();
            outputBuilder.AppendLine("Partitions:");
            
            var partitionNumber = 0;
            var partitionTable = new Table
            {
                Columns = new[]
                {
                    new Column { Name = "#" },
                    new Column { Name = "Name" },
                    new Column { Name = "Size", Alignment = ColumnAlignment.Right },
                    new Column { Name = "LowCyl", Alignment = ColumnAlignment.Right },
                    new Column { Name = "HighCyl", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Reserved", Alignment = ColumnAlignment.Right },
                    new Column { Name = "PreAlloc", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Block Size", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Buffers", Alignment = ColumnAlignment.Right },
                    new Column { Name = "DOS Type", Alignment = ColumnAlignment.Left },
                    new Column { Name = "Mask", Alignment = ColumnAlignment.Left },
                    new Column { Name = "Max Transfer", Alignment = ColumnAlignment.Left },
                    new Column { Name = "Bootable", Alignment = ColumnAlignment.Left },
                    new Column { Name = "No Mount", Alignment = ColumnAlignment.Left },
                    new Column { Name = "Priority", Alignment = ColumnAlignment.Right }
                },
                Rows = rigidDiskBlock.PartitionBlocks.Select(x => new Row
                    {
                        Columns = new[]
                        {
                            (++partitionNumber).ToString(),
                            x.DriveName, x.PartitionSize.FormatBytes(), x.LowCyl.ToString(), x.HighCyl.ToString(),
                            x.Reserved.ToString(), x.PreAlloc.ToString(), x.FileSystemBlockSize.ToString(),
                            x.NumBuffer.ToString(),
                            $"0x{x.DosType.FormatHex().ToUpper()} ({x.DosTypeFormatted})",
                            $"0x{x.Mask.FormatHex().ToUpper()} ({x.Mask})",
                            $"0x{x.MaxTransfer.FormatHex().ToUpper()} ({x.MaxTransfer})",
                            x.Bootable.ToString(),
                            x.NoMount.ToString(),
                            x.BootPriority.ToString()
                        }
                    })
                    .ToList()
            };

            outputBuilder.AppendLine();
            outputBuilder.Append(TablePresenter.Present(partitionTable));

            if (mediaInfo.DiskInfo.RdbPartitionTablePart != null)
            {
                outputBuilder.AppendLine();
                outputBuilder.Append(InfoPresenter.PresentInfo(mediaInfo.DiskInfo.RdbPartitionTablePart, showUnallocated));
            }
            
            return outputBuilder.ToString();
        }
    }
}