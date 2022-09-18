namespace Hst.Imager.ConsoleApp.Presenters
{
    using System;
    using System.Linq;
    using System.Text;
    using HstWbInstaller.Imager.Core.Commands;
    using HstWbInstaller.Imager.Core.Extensions;

    public static class MasterBootRecordPresenter
    {
        public static string Present(MbrInfo mbrInfo)
        {
            if (mbrInfo == null)
            {
                return "No Master Boot Record present";
            }
            
            var outputBuilder = new StringBuilder();
            
            outputBuilder.AppendLine("Master Boot Record:");
            var masterBootRecordTable = new Table
            {
                Columns = new[]
                {
                    new Column { Name = "Size", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Sectors", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Block Size", Alignment = ColumnAlignment.Right }
                },
                Rows = new []{ new Row
                    {
                        Columns = new[]
                        {
                            mbrInfo.DiskSize.FormatBytes(),
                            mbrInfo.Sectors.ToString(),
                            mbrInfo.BlockSize.ToString()
                        }
                    }}
                    .ToList()
            };
            outputBuilder.AppendLine();
            outputBuilder.Append(TablePresenter.Present(masterBootRecordTable));
            
            outputBuilder.AppendLine();
            outputBuilder.AppendLine("Partitions:");
            
            var partitionNumber = 0;
            var partitionTable = new Table
            {
                Columns = new[]
                {
                    new Column { Name = "#" },
                    new Column { Name = "Type" },
                    new Column { Name = "Size", Alignment = ColumnAlignment.Right },
                    new Column { Name = "First Sector", Alignment = ColumnAlignment.Right },
                    new Column { Name = "Last Sector", Alignment = ColumnAlignment.Right }
                },
                Rows = mbrInfo.Partitions.Select(x => new Row
                    {
                        Columns = new[]
                        {
                            (++partitionNumber).ToString(),
                            x.Type,
                            x.PartitionSize.FormatBytes(),
                            x.FirstSector.ToString(),
                            x.LastSector.ToString()
                        }
                    })
                    .ToList()
            };

            outputBuilder.AppendLine();
            outputBuilder.Append(TablePresenter.Present(partitionTable));

            return outputBuilder.ToString();
        }
    }
}