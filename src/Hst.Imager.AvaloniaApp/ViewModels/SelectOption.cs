using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class SelectOption
{
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public override string ToString() => Title;
}

public static class MediaOptions
{
    public const string CustomPartPath = "custom";

    public const string ImageFile = "ImageFile";
    public const string PhysicalDisk = "PhysicalDisk";

    public static readonly List<SelectOption> SourceTypeOptions =
    [
        new SelectOption { Title = "Image file", Value = ImageFile },
        new SelectOption { Title = "Physical disk", Value = PhysicalDisk }
    ];

    public static string FormatBytes(long bytes, int decimals = 1)
    {
        if (bytes <= 0) return "0 B";

        const double k = 1024;
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];

        var unit = (int)Math.Floor(Math.Log(bytes) / Math.Log(k));
        var formattedSize = Math.Round(bytes / Math.Pow(k, unit), decimals);
        if (formattedSize >= 1000 && unit < units.Length - 1)
        {
            unit++;
            return $"{(formattedSize / 1000).ToString($"F{decimals}", CultureInfo.InvariantCulture)} {units[unit]}";
        }

        return $"{formattedSize.ToString(CultureInfo.InvariantCulture)} {units[unit]}";
    }

    /// <summary>
    /// Build part path options for media with disk, partition tables, partitions and custom.
    /// </summary>
    public static List<SelectOption> GetPartPathOptions(MediaInfo media, bool includePartitions = true)
    {
        var options = new List<SelectOption>
        {
            new() { Title = $"Disk ({FormatBytes(media.DiskSize)})", Value = media.Path }
        };

        if (media.Type is Media.MediaType.CompressedRaw or Media.MediaType.CompressedVhd)
        {
            return options;
        }

        var separator = media.Path.StartsWith('/') ? "/" : "\\";

        AddPartitionTableOptions(options, media.Path, separator, "gpt", "Guid Partition Table",
            media.DiskInfo?.GptPartitionTablePart, includePartitions);
        AddPartitionTableOptions(options, media.Path, separator, "mbr", "Master Boot Record",
            media.DiskInfo?.MbrPartitionTablePart, includePartitions);
        AddPartitionTableOptions(options, media.Path, separator, "rdb", "Rigid Disk Block",
            media.DiskInfo?.RdbPartitionTablePart, includePartitions);

        options.Add(new SelectOption { Title = "Custom", Value = CustomPartPath });

        return options;
    }

    private static void AddPartitionTableOptions(List<SelectOption> options, string path, string separator,
        string name, string title, PartitionTablePart? partitionTablePart, bool includePartitions)
    {
        if (partitionTablePart == null)
        {
            return;
        }

        options.Add(new SelectOption
        {
            Title = $"{title} ({FormatBytes(partitionTablePart.Size)})",
            Value = string.Concat(path, separator, name)
        });

        if (!includePartitions)
        {
            return;
        }

        foreach (var part in (partitionTablePart.Parts ?? []).Where(x => x.PartType == PartType.Partition))
        {
            options.Add(new SelectOption
            {
                Title = $"Partition #{part.PartitionNumber}: {FormatPartType(part)} ({FormatBytes(part.Size)})",
                Value = string.Concat(path, separator, name, separator, part.PartitionNumber)
            });
        }
    }

    public static string FormatPartType(PartInfo part) =>
        part.PartitionType == part.FileSystem
            ? part.PartitionType
            : $"{part.PartitionType}, {part.FileSystem}";
}
