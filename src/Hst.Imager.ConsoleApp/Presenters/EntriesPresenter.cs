namespace Hst.Imager.ConsoleApp.Presenters;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Core.Commands;
using Core.Extensions;
using Core.Models.FileSystems;

public static class EntriesPresenter
{
    public static string PresentEntries(EntriesInfo entriesInfo, FormatEnum format)
    {
        switch (format)
        {
            case FormatEnum.Table:
                return FormatTable(entriesInfo);
            case FormatEnum.Json:
                return FormatJson(entriesInfo);
            default:
                throw new ArgumentException($"Unsupported format '{format}'", nameof(format));
        }
    }

    private static string FormatJson(EntriesInfo entriesInfo)
    {
        return JsonSerializer.Serialize(entriesInfo, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
    
    private static string FormatTable(EntriesInfo entriesInfo)
    {
        var outputBuilder = new StringBuilder();
        
        var dirsCount = 0;
        var filesCount = 0;
        var rows = new List<Row>();

        var entries = (entriesInfo.Entries ?? new List<Entry>()).ToList();

        var propertiesIndex = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            foreach (var property in entry.Properties)
            {
                if (propertiesIndex.Contains(property.Key))
                {
                    continue;
                }

                propertiesIndex.Add(property.Key);
            }
        }

        var propertiesOrdered = propertiesIndex.OrderBy(x => x).ToList();
        
        var orderedEntries = entriesInfo.Recursive
            ? entries.OrderBy(x => x.RawPath)
            : entries.OrderBy(x => x.Type).ThenBy(x => x.Name);
        
        foreach (var entry in orderedEntries)
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

            var columns = new List<string>(new[]
            {
                entry.FormattedName,
                entry.Type == EntryType.Dir ? "<DIR>" : entry.Size.FormatBytes(),
                entry.Date == null ? string.Empty : entry.Date.Value.ToString(CultureInfo.CurrentCulture),
                entry.Attributes ?? string.Empty
            });

            foreach (var property in propertiesOrdered)
            {
                columns.Add(entry.Properties.ContainsKey(property) ? entry.Properties[property] : string.Empty);
            }
            
            rows.Add(new Row
            {
                Columns = columns.ToArray()
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
            }.Concat(propertiesOrdered.Select(x => new Column{ Name = x})).ToArray(),
            Rows = rows
        };

        outputBuilder.AppendLine();
        outputBuilder.AppendLine($"Path: {entriesInfo.Path}");
        outputBuilder.AppendLine("Entries:");
        outputBuilder.AppendLine();
        outputBuilder.Append(TablePresenter.Present(entriesTable));
        outputBuilder.AppendLine();
        outputBuilder.AppendLine($"{dirsCount} {(dirsCount > 1 ? "directories" : "directory")}, {filesCount} {(filesCount > 1 ? "files" : "file")}");
        outputBuilder.AppendLine();
        return outputBuilder.ToString();
    }
}