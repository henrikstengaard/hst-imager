using System.Linq;
using System.Text;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Extensions;

namespace Hst.Imager.ConsoleApp.Presenters;

public static class GuidPartitionTablePresenter
{
    public static string Present(MediaInfo mediaInfo, bool showUnallocated)
    {
        if (mediaInfo == null || mediaInfo.DiskInfo == null || mediaInfo.DiskInfo.GptPartitionTablePart == null)
        {
            return "No Guid Partition Table present";
        }

        var gptPartitionTablePart = mediaInfo.DiskInfo.GptPartitionTablePart;
            
        var outputBuilder = new StringBuilder();
            
        outputBuilder.AppendLine($"Guid Partition Table info read from '{mediaInfo.Path}':");
        outputBuilder.AppendLine();
        outputBuilder.AppendLine("Guid Partition Table:");
        var guidPartitionTableTable = new Table
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
                        gptPartitionTablePart.Size.FormatBytes(),
                        gptPartitionTablePart.Sectors.ToString()
                    }
                }}
                .ToList()
        };
        outputBuilder.AppendLine();
        outputBuilder.Append(TablePresenter.Present(guidPartitionTableTable));
            
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
            Rows = gptPartitionTablePart.Parts.Select(x => new Row
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
        outputBuilder.Append(InfoPresenter.PresentInfo(gptPartitionTablePart, showUnallocated));
            
        return outputBuilder.ToString();
    }
}