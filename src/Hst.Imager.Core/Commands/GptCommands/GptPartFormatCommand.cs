using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Fat;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using DiscUtils.Raw;
using DiscUtils.Streams;
using Hst.Core;
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

    public GptPartFormatCommand(ILogger<GptPartFormatCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber, GptPartType type, string name)
    {
        this.logger = logger;
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
        this.partitionNumber = partitionNumber;
        this.type = type;
        this.name = name;
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
            
        var disk = media is DiskMedia diskMedia
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

        OnInformationMessage($"- Partition number '{partitionNumber}'");

        if (partitionNumber < 1 || partitionNumber > guidPartitionTable.Partitions.Count)
        {
            return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
        }

        var partitionInfo = guidPartitionTable.Partitions[partitionNumber - 1];
            
        OnInformationMessage($"- Type '{partitionInfo.TypeAsString}'");
        OnInformationMessage($"- Partition name '{name}'");

        // format mbr partition
        switch (type)
        {
            case GptPartType.Fat32:
                FatFileSystem.FormatPartition(disk, partitionNumber - 1, name);
                break;
            case GptPartType.Ntfs:
                var partition = disk.Partitions.Partitions[partitionNumber - 1];
                NtfsFileSystem.Format(partition.Open(), name, Geometry.FromCapacity(partition.SectorCount * 512), 
                    partition.FirstSector, partition.SectorCount);
                break;
            default:
                return new Result(new Error("Unsupported partition type"));
        }
            
        // flush disk content
        await disk.Content.FlushAsync(token);
            
        return new Result();
    }
}