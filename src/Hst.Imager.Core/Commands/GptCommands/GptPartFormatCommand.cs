using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using Hst.Core;
using Hst.Imager.Core.Apis;
using Hst.Imager.Core.FileSystems;
using Hst.Imager.Core.FileSystems.ExFat;
using Hst.Imager.Core.FileSystems.Fat32;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands.GptCommands;

public class GptPartFormatCommand : CommandBase
{
    private readonly ILogger<GptPartFormatCommand> logger;
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string path;
    private readonly int partitionNumber;
    private readonly GptPartType type;
    private readonly string name;
    private readonly int clusterSize;

    public GptPartFormatCommand(ILogger<GptPartFormatCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber, GptPartType type,
        string name, int clusterSize = 0)
    {
        this.logger = logger;
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
        this.partitionNumber = partitionNumber;
        this.type = type;
        this.name = name;
        this.clusterSize = clusterSize;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Formatting partition in Guid Partition Table at '{path}'");

        OnDebugMessage($"Opening '{path}' as writable");

        var physicalDrivesList = physicalDrives.ToList();
        var mediaResult = await commandHelper.GetWritableMedia(physicalDrivesList, path);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }
        using var media = mediaResult.Value;
            
        var disk = await MediaHelper.ResolveVirtualDisk(media);

        OnDebugMessage("Reading Guid Partition Table");
            
        GuidPartitionTable guidPartitionTable;
        try
        {
            guidPartitionTable = new GuidPartitionTable(disk);
        }
        catch (Exception)
        {
            return new Result(new Error("Guid Partition Table not found"));
        }

        OnInformationMessage($"- Partition number '{partitionNumber}'");

        if (partitionNumber < 1 || partitionNumber > guidPartitionTable.Partitions.Count)
        {
            return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
        }

        var partitionInfo = guidPartitionTable.Partitions[partitionNumber - 1];

        OnInformationMessage($"- Type '{partitionInfo.TypeAsString}'");
        OnInformationMessage($"- File system '{type}'");
        OnInformationMessage($"- Partition name '{name}'");
        OnInformationMessage($"- Cluster size '{ClusterSizeHelper.Format(clusterSize)}'");

        var clusterSizeResult = ClusterSizeHelper.Validate(type.ToString(), type == GptPartType.Fat32,
            type == GptPartType.ExFat, clusterSize, disk.SectorSize);
        if (clusterSizeResult.IsFaulted)
        {
            return clusterSizeResult;
        }

        var formatResult = await FormatPartition(disk, partitionInfo);
        if (formatResult.IsFaulted)
        {
            return formatResult;
        }
            
        // flush disk content
        await disk.Content.FlushAsync(token);

        if (media.IsPhysicalDrive)
        {
            await commandHelper.RescanPhysicalDrives();
        }

        return new Result();
    }

    private async Task<Result> FormatPartition(VirtualDisk disk, DiscUtils.Partitions.PartitionInfo partitionInfo)
    {
        try
        {
            switch (type)
            {
                case GptPartType.Fat32:
                    var partitionOffset = partitionInfo.FirstSector * disk.Geometry.Value.BytesPerSector;
                    await Fat32Formatter.FormatPartition(disk.Content, partitionOffset,
                        partitionInfo.SectorCount * disk.Geometry.Value.BytesPerSector,
                        disk.Geometry.Value.BytesPerSector, disk.Geometry.Value.SectorsPerTrack, disk.Geometry.Value.HeadsPerCylinder, 
                        name, clusterSize);
                    break;
                case GptPartType.Ntfs:
                    NtfsFileSystem.Format(partitionInfo.Open(), name, Geometry.FromCapacity(partitionInfo.SectorCount * 512),
                        partitionInfo.FirstSector, partitionInfo.SectorCount);
                    break;
                case GptPartType.ExFat:
                    await ExFatFormatter.FormatPartition(disk.Content,
                        partitionInfo.FirstSector * disk.Geometry.Value.BytesPerSector,
                        partitionInfo.SectorCount * disk.Geometry.Value.BytesPerSector,
                        disk.Geometry.Value.BytesPerSector, name, clusterSize);
                    break;
                default:
                    return new Result(new Error("Unsupported partition type"));
            }
        }
        catch (Exception e) when (e is ArgumentException or IOException and not Win32IoException)
        {
            return new Result(new Error($"Failed to format partition: {e.Message}"));
        }

        return new Result();
    }
}