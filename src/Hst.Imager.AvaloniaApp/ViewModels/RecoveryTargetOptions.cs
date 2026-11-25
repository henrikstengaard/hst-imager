using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Imager.Core.Commands;

namespace Hst.Imager.AvaloniaApp.ViewModels;

/// <summary>
/// Targets for recovery operations with path to disk, partition table or partition as value.
/// </summary>
public static class RecoveryTargetOptions
{
    private static readonly long[] AdfSizes = [901120, 1802240];

    private static readonly string[] SupportedDosTypes =
    [
        "DOS\\0", "DOS\\1", "DOS\\2", "DOS\\3", "DOS\\4", "DOS\\5", "DOS\\6", "DOS\\7",
        "PFS\\3", "PDS\\3", "SFS\\0"
    ];

    /// <summary>
    /// Get partition tables, which can be scanned and repaired: disk with auto detected partition tables, partition
    /// tables present and PiStorm Rigid Disk Blocks in partitions. Rigid Disk Block is always included, so a damaged
    /// Rigid Disk Block, which isn't detected, can be scanned and repaired.
    /// </summary>
    public static List<SelectOption> GetPartitionTableOptions(MediaInfo media)
    {
        if (IsAdf(media))
        {
            return [];
        }

        var separator = GetSeparator(media.Path);
        var diskInfo = media.DiskInfo;
        var options = new List<SelectOption>
        {
            new() { Title = $"Disk, detect partition tables ({MediaOptions.FormatBytes(media.DiskSize)})", Value = media.Path }
        };

        if (diskInfo?.GptPartitionTablePart != null)
        {
            options.Add(new SelectOption
            {
                Title = $"Guid Partition Table ({diskInfo.GptPartitionTablePart.Parts?.Count(x => x.PartType == PartType.Partition) ?? 0} partitions)",
                Value = string.Concat(media.Path, separator, "gpt")
            });
        }

        if (diskInfo?.MbrPartitionTablePart != null)
        {
            options.Add(new SelectOption
            {
                Title = $"Master Boot Record ({diskInfo.MbrPartitionTablePart.Parts?.Count(x => x.PartType == PartType.Partition) ?? 0} partitions)",
                Value = string.Concat(media.Path, separator, "mbr")
            });
        }

        options.Add(new SelectOption
        {
            Title = diskInfo?.RdbPartitionTablePart != null
                ? $"Rigid Disk Block ({diskInfo.RigidDiskBlock?.PartitionBlocks.Count() ?? 0} partitions)"
                : "Rigid Disk Block (not detected)",
            Value = string.Concat(media.Path, separator, "rdb")
        });

        AddPiStormRdbOptions(options, media.Path, separator, "gpt", "Guid Partition Table",
            diskInfo?.GptPartitionTablePart);
        AddPiStormRdbOptions(options, media.Path, separator, "mbr", "Master Boot Record",
            diskInfo?.MbrPartitionTablePart);

        return options;
    }

    /// <summary>
    /// Get partitions with Amiga file systems FFS, PFS3 and SFS in Rigid Disk Block, which can be scanned, repaired
    /// and salvaged, or adf image file.
    /// </summary>
    public static List<SelectOption> GetFileSystemOptions(MediaInfo media)
    {
        if (IsAdf(media))
        {
            return
            [
                new SelectOption { Title = "Adf image file (Fast File System)", Value = media.Path }
            ];
        }

        var rigidDiskBlock = media.DiskInfo?.RigidDiskBlock;
        if (rigidDiskBlock == null)
        {
            return [];
        }

        var separator = GetSeparator(media.Path);
        return rigidDiskBlock.PartitionBlocks
            .Select((partitionBlock, index) => (PartitionBlock: partitionBlock, Number: index + 1))
            .Where(x => SupportedDosTypes.Contains(x.PartitionBlock.DosTypeFormatted))
            .Select(x => new SelectOption
            {
                Title = $"Partition #{x.Number}: {x.PartitionBlock.DriveName}, {x.PartitionBlock.DosTypeFormatted} ({MediaOptions.FormatBytes(GetPartitionSize(x.PartitionBlock))})",
                Value = string.Concat(media.Path, separator, "rdb", separator, x.Number)
            })
            .ToList();
    }

    private static void AddPiStormRdbOptions(List<SelectOption> options, string path, string separator,
        string name, string title, PartitionTablePart? partitionTablePart)
    {
        foreach (var part in (partitionTablePart?.Parts ?? []).Where(IsPiStormRdb))
        {
            options.Add(new SelectOption
            {
                Title = $"PiStorm Rigid Disk Block in {title} partition #{part.PartitionNumber} ({MediaOptions.FormatBytes(part.Size)})",
                Value = string.Concat(path, separator, name, separator, part.PartitionNumber, separator, "rdb")
            });
        }
    }

    private static bool IsPiStormRdb(PartInfo part) =>
        part.PartType == PartType.Partition &&
        (string.Equals(part.PartitionType, Core.Constants.FileSystemNames.PiStormRdb, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(part.GuidType, Core.Constants.GuidPartitionTypes.PiStormRdb.ToString(),
             StringComparison.OrdinalIgnoreCase));

    private static long GetPartitionSize(PartitionBlock partitionBlock) =>
        (long)(partitionBlock.HighCyl - partitionBlock.LowCyl + 1) * partitionBlock.Surfaces *
        partitionBlock.BlocksPerTrack * 512;

    private static bool IsAdf(MediaInfo media) =>
        Path.GetExtension(media.Path).Equals(".adf", StringComparison.OrdinalIgnoreCase) ||
        (AdfSizes.Contains(media.DiskSize) && media.DiskInfo?.RdbPartitionTablePart == null &&
         media.DiskInfo?.MbrPartitionTablePart == null && media.DiskInfo?.GptPartitionTablePart == null);

    private static string GetSeparator(string path) => path.StartsWith('/') ? "/" : "\\";
}
