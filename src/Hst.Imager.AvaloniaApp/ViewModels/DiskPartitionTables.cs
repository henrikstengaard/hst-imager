using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.Core.Commands;

namespace Hst.Imager.AvaloniaApp.ViewModels;

/// <summary>
/// Reads partition tables of a disk and builds segments shown in partition layout bar, shared by partition and info
/// views.
/// </summary>
public static class DiskPartitionTables
{
    /// <summary>
    /// Read partition tables of disk and PiStorm disks in master boot record partitions with bios type 0x76 (118).
    /// PiStorm disks are read from their master boot record partition. Errors reading PiStorm disks are passed to
    /// error handler and disk is still read without them.
    /// </summary>
    public static async Task<List<DiskPartitionTable>> ReadAsync(IMediaService mediaService, MediaInfo? media,
        bool byteswap, Action<string>? onError = null)
    {
        if (media == null)
            return [];

        var diskInfo = media.DiskInfo;
        var tableTypes = PartitionLayout.GetPartitionTableTypes(diskInfo);

        // disk without partition tables is uninitialized, if first sectors only contain zeroes
        if (tableTypes.Count == 0)
        {
            var isBlank = false;
            try
            {
                isBlank = await mediaService.IsBlankAsync(media.Path, byteswap);
            }
            catch (Exception)
            {
                // shown as no partition table, if first sectors can't be read
            }

            return [new DiskPartitionTable(PartitionLayout.CreateEmpty(media.DiskSize, isBlank), media.Path)];
        }

        var tables = tableTypes
            .Select(x => new DiskPartitionTable(PartitionLayout.FromMediaInfo(media, x), media.Path))
            .ToList();

        var mbrLayout = tables.FirstOrDefault(x => x.Layout.TableType == PartitionTableType.MasterBootRecord)?.Layout;
        if (mbrLayout == null)
            return tables;

        var separator = media.Path.StartsWith('/') ? "/" : "\\";
        foreach (var part in (diskInfo?.MbrPartitionTablePart?.Parts ?? [])
                 .Where(x => x.PartType == PartType.Partition && x.BiosType == "118"))
        {
            var container = mbrLayout.Partitions.FirstOrDefault(x => x.Number == part.PartitionNumber);
            if (container == null)
                continue;

            var path = string.Concat(media.Path, separator, "mbr", separator, part.PartitionNumber);
            MediaInfo? piStormMedia;
            try
            {
                piStormMedia = await mediaService.GetMediaInfoAsync(path, byteswap);
            }
            catch (Exception ex)
            {
                // disk is still partitioned without PiStorm disk, which can't be read
                onError?.Invoke(
                    $"Failed to read PiStorm Rigid Disk Block in Master Boot Record partition #{part.PartitionNumber}: {ex.Message}");
                continue;
            }

            if (piStormMedia == null)
                continue;

            tables.Add(new DiskPartitionTable(
                PartitionLayout.FromMediaInfo(piStormMedia, PartitionTableType.RigidDiskBlock), path)
            {
                Container = container,
                ContainerLayout = mbrLayout
            });
        }

        return tables;
    }

    /// <summary>
    /// Build segments of partition tables ordered by their position on disk. Areas reserved by other partition
    /// tables are left out, as they are shown by the other partition table. Partition tables in partitions are nested
    /// in the partition containing them.
    /// </summary>
    public static List<PartitionSegmentViewModel> BuildSegments(IEnumerable<DiskPartitionTable> tables)
    {
        var tableList = tables.ToList();
        var segments = new List<PartitionSegmentViewModel>();

        foreach (var table in tableList.Where(x => x.IsDiskPath))
            segments.AddRange(table.Layout.BuildSegments().Where(x => !x.IsReserved));

        foreach (var table in tableList.Where(x => !x.IsDiskPath))
        {
            var container = segments.FirstOrDefault(x => ReferenceEquals(x.Partition, table.Container));
            if (container == null)
                continue;
            container.NestedLayout = table.Layout;
            segments.AddRange(table.Layout.BuildSegments(table.Offset, 1).Where(x => !x.IsReserved));
        }

        return segments.OrderBy(x => x.DiskStart).ThenBy(x => x.Depth).ToList();
    }

    /// <summary>
    /// Describe start and end shown for partitions in sectors and cylinders for rigid disk block partitions.
    /// </summary>
    public static string FormatSectorsOrCylinders(IEnumerable<PartitionLayout> layouts)
    {
        const string sectors = "Start and end in sectors of 512 bytes from start of disk.";
        var rdbLayouts = layouts.Where(x => x.HasPartitionTable && x.IsRdb).ToList();
        if (rdbLayouts.Count == 0)
            return sectors;

        var cylinders = rdbLayouts.Select(x => x.Alignment).Distinct().Count() == 1
            ? $"cylinders of {MediaOptions.FormatBytes(rdbLayouts[0].Alignment)}"
            : "cylinders";
        return $"{sectors} Start and end cylinder of Rigid Disk Block partitions in {cylinders} from start of Rigid Disk Block.";
    }
}
