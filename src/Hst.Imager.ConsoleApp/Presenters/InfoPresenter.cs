namespace Hst.Imager.ConsoleApp.Presenters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using HstWbInstaller.Imager.Core.Commands;
    using HstWbInstaller.Imager.Core.Extensions;

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
                    new Column { Name = "Model" },
                    new Column { Name = "Type" },
                    new Column { Name = "Size", Alignment = ColumnAlignment.Right }
                },
                Rows = mediaInfos.Select(x => new Row
                {
                    Columns = new []{ x.Path, x.Model, x.Type.ToString(),x.DiskSize.FormatBytes() }
                })
            };

            outputBuilder.AppendLine("Physical drives:");
            outputBuilder.AppendLine();
            outputBuilder.Append(TablePresenter.Present(listTable));
            return outputBuilder.ToString();
        }

        public static string PresentInfo(DiskInfo diskInfo)
        {
            var layoutWidth = 80;

            var rows = new List<Row>(new[]
            {
                new Row
                {
                    Columns = new[]
                    {
                        "Disk", string.Empty, diskInfo.Size.FormatBytes(),
                        diskInfo.StartOffset.ToString(),
                        diskInfo.EndOffset.ToString(),
                        BuildLayout(layoutWidth, diskInfo.Size, diskInfo.StartOffset, diskInfo.EndOffset)
                    }
                }
            });

            foreach (var partitionTable in diskInfo.PartitionTables)
            {
                rows.Add(new Row
                {
                    Columns = new[]
                    {
                        $"- {partitionTable.Type.ToString()}", string.Empty, string.Empty,
                        partitionTable.StartOffset.ToString(),
                        partitionTable.EndOffset.ToString(),
                        BuildLayout(layoutWidth, diskInfo.Size, partitionTable.StartOffset, partitionTable.EndOffset)
                    }
                });
                rows.AddRange(partitionTable.Partitions.Select(x => new Row
                {
                    Columns = new[]
                    {
                        $"  - {x.Type}", x.PartitionNumber.ToString(), x.Size.FormatBytes(), x.StartOffset.ToString(), x.EndOffset.ToString(),
                        BuildLayout(layoutWidth, diskInfo.Size, x.StartOffset, x.EndOffset)
                    }
                }));
            }

            var diskTable = new Table
            {
                Columns = new[]
                {
                    new Column { Name = "Type" },
                    new Column { Name = "#" },
                    new Column { Name = "Size", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Start", Alignment = ColumnAlignment.Right },
                    new Column { Name = "End", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Layout" }
                },
                Rows = rows
            };

            var outputBuilder = new StringBuilder();
            outputBuilder.AppendLine($"Info read from '{diskInfo.Path}':");
            outputBuilder.AppendLine();
            outputBuilder.Append(TablePresenter.Present(diskTable));

            return outputBuilder.ToString();
        }

        private static string BuildLayout(int maxWidth, long size, long startOffset, long endOffset)
        {
            var sizePerWidth = (double)maxWidth / size;
            var start = Convert.ToInt32(sizePerWidth * startOffset);
            var end = Convert.ToInt32(sizePerWidth * endOffset);
            var length = end - start;

            if (length <= 0)
            {
                length = 1;
                end = maxWidth - (start + length);
            }

            return string.Concat(new string(' ', start), new string('=', length), new string(' ', maxWidth - end));
        }
    }
}