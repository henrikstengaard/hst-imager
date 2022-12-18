namespace Hst.Imager.ConsoleApp.Presenters;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Commands;
using Core.Extensions;
using Core.Models.FileSystems;

public static class EntriesPresenter
{
    public static string PresentEntries(EntriesInfo entriesInfo)
    {
        var outputBuilder = new StringBuilder();
        
        var dirsCount = 0;
        var filesCount = 0;
        var rows = new List<Row>();
        foreach (var entry in (entriesInfo.Entries ?? new List<Entry>()).OrderBy(x => x.Type).ThenBy(x => x.Name))
        {
            switch (entry.Type)
            {
                case EntryType.Dir:
                    dirsCount++;
                    break;
                case EntryType.File:
                    filesCount++;
                    break;
            }

            rows.Add(new Row
            {
                Columns = new[]
                {
                    entry.Name,
                    entry.Type == EntryType.Dir ? "<DIR>" : entry.Size.FormatBytes(),
                    entry.Date == null ? string.Empty : entry.Date.Value.ToString("yyyy-MM-dd hh:mm:ss"),
                    entry.Attributes ?? string.Empty
                }
            });
        }

        var entriesTable = new Table
        {
            Columns = new[]
            {
                new Column { Name = "Name" },
                new Column { Name = "Size", Alignment = ColumnAlignment.Right },
                new Column { Name = "Date" },
                new Column { Name = "Attributes" }
            },
            Rows = rows
        };

        outputBuilder.AppendLine($"Disk path: {entriesInfo.DiskPath}");
        if (!string.IsNullOrWhiteSpace(entriesInfo.FileSystemPath))
        {
            outputBuilder.AppendLine($"File system path: {entriesInfo.FileSystemPath}");
        }
        outputBuilder.AppendLine();
        outputBuilder.AppendLine("Entries:");
        outputBuilder.AppendLine();
        outputBuilder.Append(TablePresenter.Present(entriesTable));
        outputBuilder.AppendLine();
        outputBuilder.AppendLine($"{dirsCount} {(dirsCount > 1 ? "directories" : "directory")}, {filesCount} {(filesCount > 1 ? "files" : "file")}");
        outputBuilder.AppendLine();
        return outputBuilder.ToString();
    }
}