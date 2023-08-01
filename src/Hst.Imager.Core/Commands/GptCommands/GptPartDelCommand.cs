using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Partitions;
using DiscUtils.Raw;
using DiscUtils.Streams;
using Hst.Core;
using Hst.Imager.Core.Extensions;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands.GptCommands;

public class GptPartDelCommand : CommandBase
{
    private readonly ILogger<GptPartDelCommand> logger;
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string path;
    private readonly int partitionNumber;

    public GptPartDelCommand(ILogger<GptPartDelCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber)
    {
        this.logger = logger;
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
        this.partitionNumber = partitionNumber;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Deleting partition from Guid Partition Table at '{path}'");
            
        OnDebugMessage($"Opening '{path}' as writable");

        var physicalDrivesList = physicalDrives.ToList();
        var mediaResult = commandHelper.GetWritableMedia(physicalDrivesList, path);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }
        using var media = mediaResult.Value;
            
        using var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new Disk(media.Stream, Ownership.None);
            
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

        OnDebugMessage($"Disk size: {disk.Capacity.FormatBytes()} ({disk.Capacity} bytes)");
        OnDebugMessage($"Sectors: {disk.Geometry.TotalSectorsLong}");
        OnDebugMessage($"Sector size: {disk.SectorSize} bytes");

        OnDebugMessage($"Deleting partition number '{partitionNumber}'");
            
        if (partitionNumber < 1 || partitionNumber > guidPartitionTable.Partitions.Count)
        {
            return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
        }
            
        // delete gpt partition
        guidPartitionTable.Delete(partitionNumber - 1);
            
        // flush disk content
        await disk.Content.FlushAsync(token);
            
        return new Result();
    }
}