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
            if (mediaInfo?.DiskInfo?.MbrPartitionTablePart?.DiskGeometry == null)
            {
                return "No Master Boot Record present";
            }

            var outputBuilder = new StringBuilder();
            
            outputBuilder.AppendLine($"Master Boot Record info read from '{mediaInfo.Path}':");
            outputBuilder.AppendLine();
            outputBuilder.AppendLine("Master Boot Record:");
            var masterBootRecordTable = new Table
            {
                Columns = new[]
                {
                    new Column { Name = "Size" },
                    new Column { Name = "Sector size", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Total sectors", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Cylinders", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Heads per cylinder", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Sectors per track", Alignment = ColumnAlignment.Right }
                },
                Rows = new []{ new Row
                    {
                        Columns = new[]
                        {
                            mediaInfo.DiskInfo.MbrPartitionTablePart.DiskGeometry.Capacity.FormatBytes(),
                            mediaInfo.DiskInfo.MbrPartitionTablePart.DiskGeometry.BytesPerSector.ToString(),
                            mediaInfo.DiskInfo.MbrPartitionTablePart.DiskGeometry.TotalSectors.ToString(),
                            mediaInfo.DiskInfo.MbrPartitionTablePart.DiskGeometry.Cylinders.ToString(),
                            mediaInfo.DiskInfo.MbrPartitionTablePart.DiskGeometry.HeadsPerCylinder.ToString(),
                            mediaInfo.DiskInfo.MbrPartitionTablePart.DiskGeometry.SectorsPerTrack.ToString()
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
                    new Column { Name = "#" },
                    new Column { Name = "Id" },
                    new Column { Name = "Type" },
                    new Column { Name = "File System" },
                    new Column { Name = "Size", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Start Sec", Alignment = ColumnAlignment.Right },
                    new Column { Name = "End Sec", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Active" },
                    new Column { Name = "Primary" }
                },
                Rows = mediaInfo.DiskInfo.MbrPartitionTablePart.Parts.Where(x => x.PartType == PartType.Partition).Select(x => new Row
                    {
                        Columns = new[]
                        {
                            x.PartitionNumber.HasValue ? x.PartitionNumber.ToString() : string.Empty,
                            x.BiosType.ToString(),
                            x.PartitionType,
                            x.FileSystem,
                            x.Size.FormatBytes(),
                            x.StartSector.ToString(),
                            x.EndSector.ToString(),
                            x.IsActive ? "Yes" : "No",
                            x.IsPrimary ? "Yes" : "No"
                        }
                    })
                    .ToList()
            };

            outputBuilder.AppendLine();
            outputBuilder.Append(TablePresenter.Present(partitionTable));

            var mbrPartitionTablePart = mediaInfo.DiskInfo.MbrPartitionTablePart;
            
            outputBuilder.AppendLine();
            outputBuilder.Append(InfoPresenter.PresentInfo(mbrPartitionTablePart, showUnallocated));
            
            return outputBuilder.ToString();
        }
    }
}