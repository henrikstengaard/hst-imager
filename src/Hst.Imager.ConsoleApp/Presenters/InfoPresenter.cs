namespace Hst.Imager.ConsoleApp.Presenters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Core.Commands;
    using Core.Extensions;

    public static class InfoPresenter
    {
        public static string PresentInfo(IEnumerable<MediaInfo> mediaInfos)
        {
            var outputBuilder = new StringBuilder();

            var listTable = new Table
            {
                Columns = new[]
                {
                    new Column { Name = "Path" },
                    new Column { Name = "Name" },
                    new Column { Name = "Size", Alignment = ColumnAlignment.Right }
                },
                Rows = mediaInfos.Select(x => new Row
                {
                    Columns = new[] { x.Path, x.Name, x.DiskSize.FormatBytes() }
                })
            };

            outputBuilder.AppendLine("Physical drives:");
            outputBuilder.AppendLine();
            outputBuilder.Append(TablePresenter.Present(listTable));
            return outputBuilder.ToString();
        }

        public static string PresentInfo(PartitionTablePart partitionTablePart, bool includeUnallocated = false)
        {
            var partsList =
                (includeUnallocated
                    ? partitionTablePart.Parts
                    : partitionTablePart.Parts.Where(x => x.PartType != PartType.Unallocated)).ToList();

            var columns = new[]
            {
                new Column { Name = "Name" },
                new Column { Name = "Style" },
                new Column { Name = "Type" },
                new Column { Name = "#" },
                new Column { Name = "Size", Alignment = ColumnAlignment.Right },
                new Column { Name = "Start Off", Alignment = ColumnAlignment.Right },
                new Column { Name = "End Off", Alignment = ColumnAlignment.Right },
                new Column
                {
                    Name = partitionTablePart.PartitionTableType == PartitionTableType.RigidDiskBlock
                        ? "Start Cyl"
                        : "Start Sec",
                    Alignment = ColumnAlignment.Right
                },
                new Column
                {
                    Name = partitionTablePart.PartitionTableType == PartitionTableType.RigidDiskBlock
                        ? "End Cyl"
                        : "End Sec",
                    Alignment = ColumnAlignment.Right
                },
                new Column { Name = "Layout" }
            };

            var rows = new List<Row>();
            var columnLengths = columns.Select(x => x.Name.Length).ToArray();

            foreach (var part in partsList)
            {
                var row = new Row
                {
                    Columns = new[]
                    {
                        $"{part.FileSystem}",
                        FormatStyle(part.PartitionTableType),
                        part.PartType.ToString(),
                        part.PartitionNumber.HasValue ? part.PartitionNumber.Value.ToString() : string.Empty,
                        part.Size.FormatBytes(),
                        part.StartOffset.ToString(),
                        part.EndOffset.ToString(),
                        partitionTablePart.PartitionTableType == PartitionTableType.RigidDiskBlock
                            ? part.StartCylinder.ToString()
                            : part.StartSector.ToString(),
                        partitionTablePart.PartitionTableType == PartitionTableType.RigidDiskBlock
                            ? part.EndCylinder.ToString()
                            : part.EndSector.ToString(),
                        string.Empty
                    }
                };
                for (var i = 0; i < row.Columns.Length; i++)
                {
                    var columnLength = row.Columns[i] == null ? 0 : row.Columns[i].Length;
                    if (i >= columns.Length || columnLength < columnLengths[i])
                    {
                        continue;
                    }

                    columnLengths[i] = columnLength;
                }

                rows.Add(row);
            }

            var layoutWidth = Console.WindowWidth - columnLengths.Sum(x => x) - ((columns.Length - 1) * 3);

            for (var i = 0; i < partsList.Count; i++)
            {
                var part = partsList[i];
                rows[i].Columns[columns.Length - 1] = BuildLayout(layoutWidth, partitionTablePart.Size,
                    part.StartOffset, part.EndOffset);
            }

            var diskTable = new Table
            {
                Columns = columns,
                Rows = rows
            };

            var outputBuilder = new StringBuilder();
            outputBuilder.AppendLine("Partition table overview:");
            outputBuilder.AppendLine($"- Path: '{partitionTablePart.Path}'");
            outputBuilder.AppendLine(
                $"- Size: {partitionTablePart.Size.FormatBytes()} ({partitionTablePart.Size} bytes)");
            outputBuilder.AppendLine($"- Partition table: '{partitionTablePart.PartitionTableType}'");
            outputBuilder.AppendLine();
            outputBuilder.Append(TablePresenter.Present(diskTable));

            return outputBuilder.ToString();
        }

        public static string PresentInfo(DiskInfo diskInfo, bool includeUnallocated = false)
        {
            var partsList = (includeUnallocated
                    ? diskInfo.DiskParts
                    : diskInfo.DiskParts.Where(x => x.PartType != PartType.Unallocated))
                .ToList();

            var columns = new[]
            {
                new Column { Name = "File System" },
                new Column { Name = "Style" },
                new Column { Name = "Type" },
                new Column { Name = "#" },
                new Column { Name = "Size", Alignment = ColumnAlignment.Right },
                new Column { Name = "Start Off", Alignment = ColumnAlignment.Right },
                new Column { Name = "End Off", Alignment = ColumnAlignment.Right },
                new Column { Name = "Layout" }
            };

            var rows = new List<Row>();
            var columnLengths = columns.Select(x => x.Name.Length).ToArray();

            foreach (var part in partsList)
            {
                var row = new Row
                {
                    Columns = new[]
                    {
                        $"{part.FileSystem}",
                        FormatStyle(part.PartitionTableType),
                        part.PartType.ToString(),
                        part.PartitionNumber.HasValue ? part.PartitionNumber.Value.ToString() : string.Empty,
                        part.Size.FormatBytes(),
                        part.StartOffset.ToString(),
                        part.EndOffset.ToString(),
                        string.Empty
                    }
                };
                for (var i = 0; i < row.Columns.Length; i++)
                {
                    var columnLength = row.Columns[i] == null ? 0 : row.Columns[i].Length;
                    if (i >= columns.Length || columnLength < columnLengths[i])
                    {
                        continue;
                    }

                    columnLengths[i] = columnLength;
                }

                rows.Add(row);
            }

            var layoutWidth = Console.WindowWidth - columnLengths.Sum(x => x) - ((columns.Length - 1) * 3);

            for (var i = 0; i < partsList.Count; i++)
            {
                var part = partsList[i];
                rows[i].Columns[columns.Length - 1] =
                    BuildLayout(layoutWidth, diskInfo.Size, part.StartOffset, part.EndOffset);
            }

            var diskTable = new Table
            {
                Columns = columns,
                Rows = rows
            };

            var outputBuilder = new StringBuilder();
            outputBuilder.AppendLine($"Disk information read from '{diskInfo.Path}':");
            outputBuilder.AppendLine("Disk overview:");
            outputBuilder.AppendLine($"- Path: '{diskInfo.Path}'");
            outputBuilder.AppendLine(
                $"- Size: {diskInfo.Size.FormatBytes()} ({diskInfo.Size} bytes)");
            outputBuilder.AppendLine(
                $"- Partition tables: {diskInfo.PartitionTables.Count()}");
            outputBuilder.AppendLine();
            outputBuilder.Append(TablePresenter.Present(diskTable));

            return outputBuilder.ToString();
        }

        private static string FormatStyle(PartitionTableType type)
        {
            return type switch
            {
                PartitionTableType.GuidPartitionTable => "GPT",
                PartitionTableType.MasterBootRecord => "MBR",
                PartitionTableType.RigidDiskBlock => "RDB",
                _ => string.Empty
            };
        }

        private static string BuildLayout(int maxWidth, long size, long startOffset, long endOffset)
        {
            var sizePerWidth = (double)maxWidth / size;
            var start = Convert.ToInt32(sizePerWidth * startOffset);
            if (start == maxWidth)
            {
                start = maxWidth - 1;
            }

            if (endOffset > size)
            {
                endOffset = size;
            }

            var length = Convert.ToInt32(sizePerWidth * (endOffset - startOffset + 1));
            if (length <= 0)
            {
                length = 1;
            }

            var end = start + length;

            return string.Concat(new string(' ', start), new string('=', length), new string(' ', maxWidth - end));
        }
    }
}