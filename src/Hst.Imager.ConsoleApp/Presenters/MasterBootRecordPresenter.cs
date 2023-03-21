namespace Hst.Imager.ConsoleApp.Presenters
{
    using System.Linq;
    using System.Text;
    using Core.Commands;
    using Core.Extensions;

    public static class MasterBootRecordPresenter
    {
        public static string Present(MediaInfo mediaInfo, bool showUnallocated)
        {
            if (mediaInfo == null || mediaInfo.DiskInfo == null || mediaInfo.DiskInfo.MbrPartitionTablePart == null)
            {
                return "No Master Boot Record present";
            }

            var mbrPartitionTablePart = mediaInfo.DiskInfo.MbrPartitionTablePart;
            
            var outputBuilder = new StringBuilder();
            
            outputBuilder.AppendLine($"Master Boot Record info read from '{mediaInfo.Path}':");
            outputBuilder.AppendLine();
            outputBuilder.AppendLine("Master Boot Record:");
            var masterBootRecordTable = new Table
            {
                Columns = new[]
                {
                    new Column { Name = "Size", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Sectors", Alignment = ColumnAlignment.Right }
                },
                Rows = new []{ new Row
                    {
                        Columns = new[]
                        {
                            mbrPartitionTablePart.Size.FormatBytes(),
                            mbrPartitionTablePart.Sectors.ToString()
                        }
                    }}
                    .ToList()
            };
            outputBuilder.AppendLine();
            outputBuilder.Append(TablePresenter.Present(masterBootRecordTable));
            
            outputBuilder.AppendLine();
            outputBuilder.AppendLine("Partitions:");
            
            var partitionTable = new Table
            {
                Columns = new[]
                {
                    new Column { Name = "File System" },
                    new Column { Name = "Type" },
                    new Column { Name = "#" },
                    new Column { Name = "Size", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Start Sec", Alignment = ColumnAlignment.Right },
                    new Column { Name = "End Sec", Alignment = ColumnAlignment.Right }
                },
                Rows = mbrPartitionTablePart.Parts.Select(x => new Row
                    {
                        Columns = new[]
                        {
                            x.FileSystem,
                            x.PartType.ToString(),
                            x.PartitionNumber.HasValue ? x.PartitionNumber.ToString() : string.Empty,
                            x.Size.FormatBytes(),
                            x.StartSector.ToString(),
                            x.EndSector.ToString()
                        }
                    })
                    .ToList()
            };

            outputBuilder.AppendLine();
            outputBuilder.Append(TablePresenter.Present(partitionTable));

            outputBuilder.AppendLine();
            outputBuilder.Append(InfoPresenter.PresentInfo(mbrPartitionTablePart, showUnallocated));
            
            return outputBuilder.ToString();
        }
    }
}