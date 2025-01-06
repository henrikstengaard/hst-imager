using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Partitions;
using DiscUtils.Raw;
using DiscUtils.Streams;
using Hst.Core;
using Hst.Core.Extensions;
using Hst.Imager.Core.Extensions;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands.GptCommands;

public class GptPartAddCommand : CommandBase
{
    private readonly ILogger<GptPartAddCommand> logger;
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string path;
    private readonly string type;
    private readonly string name;
    private readonly Size size;
    private readonly long? startSector;
    private readonly long? endSector;

    public GptPartAddCommand(ILogger<GptPartAddCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, string type, string name, Size size,
        long? startSector, long? endSector)
    {
        this.logger = logger;
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
        this.type = type;
        this.size = size;
        this.startSector = startSector;
        this.endSector = endSector;
        this.name = name;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        var gptPartitionTypeResult = GetGptPartitionType();
        if (gptPartitionTypeResult.IsFaulted)
        {
            return new Result(gptPartitionTypeResult.Error);
        }

        OnInformationMessage($"Adding partition to Guid Partition Table at '{path}'");
            
        OnDebugMessage($"Opening '{path}' for read/write");

        var physicalDrivesList = physicalDrives.ToList();
        var mediaResult = await commandHelper.GetWritableMedia(physicalDrivesList, path);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }

        using var media = mediaResult.Value;

        OnDebugMessage($"Media size '{media.Size}'");
        
        var diskInfo = await commandHelper.ReadDiskInfo(media, PartitionTableType.GuidPartitionTable);
        if (diskInfo == null)
        {
            return new Result(new Error("Failed to read disk info"));
        }
            
        OnDebugMessage("Reading Guid Partition Table");
            
        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new Disk(media.Stream, Ownership.None);

        GuidPartitionTable guidPartitionTable;
        try
        {
            guidPartitionTable = new GuidPartitionTable(disk);
        }
        catch (Exception)
        {
            return new Result(new Error("Guid Partition Table not found"));
        }

        var totalSectors = disk.Capacity / disk.SectorSize;
        var lastSector = totalSectors - 100;

        OnDebugMessage($"Disk size: {disk.Capacity.FormatBytes()} ({disk.Capacity} bytes)");
        OnDebugMessage($"Sectors: {totalSectors}");
        OnDebugMessage($"Sector size: {disk.SectorSize} bytes");
            
        // available size and default start offset
        var availableSize = disk.Capacity;
        var startOffset = guidPartitionTable.FirstUsableSector * disk.SectorSize;

        // calculate partition size and sectors
        var partitionSize = size.Value == 0 && size.Unit == Unit.Bytes
            ? 0
            : availableSize.ResolveSize(size).ToSectorSize();
        
        // find unallocated part for partition size with start offset equal or larger
        var unallocatedPart = diskInfo.DiskParts.FirstOrDefault(x =>
            x.PartType == PartType.Unallocated && x.StartOffset >= startOffset && x.Size >= partitionSize);
        if (unallocatedPart == null)
        {
            return new Result(new Error($"Guid Partition Table does not have unallocated disk space for partition size '{size}' ({partitionSize} bytes)"));
        }

        var firstSector = guidPartitionTable.FirstUsableSector;

        if (startSector.HasValue && startSector.Value < firstSector)
        {
            return new Result(new Error($"Invalid start sector '{startSector}' is less that first sector '{firstSector}'"));
        }

        // calculate start and end sector
        var start = startSector ?? unallocatedPart.StartOffset / disk.SectorSize;
        if (start <= 0)
        {
            start = guidPartitionTable.FirstUsableSector;
        }

        // set partition start sector to first sector, if less than first sector
        if (start < firstSector)
        {
            start = firstSector;
        }


        // calculate partition sectors
        var partitionSectors = (partitionSize == 0 ? unallocatedPart.Size : partitionSize) / disk.SectorSize;

        if (partitionSectors <= 0)
        {
            return new Result(new Error($"Invalid sectors for partition size '{partitionSize}', start sector '{start}', total sectors '{totalSectors}'"));
        }
            
        var end = start + partitionSectors - 1;
        partitionSize = partitionSectors * disk.SectorSize;

        if (endSector.HasValue && end > endSector)
        {
            end = endSector.Value;
            partitionSectors = end - start + 1;
            partitionSize = partitionSectors * 512;
        }

        // set end to last sector, if end is larger than last sector
        if (end > lastSector)
        {
            end = lastSector;
            partitionSectors = end - start + 1;
            partitionSize = partitionSectors * disk.SectorSize;
        }
            
        // return error, if start it's less than first usable sector
        if (start < guidPartitionTable.FirstUsableSector)
        {
            return new Result(new Error(
                $"Start sector {startSector} is overlapping reversed partition space. First usable sector is {guidPartitionTable.FirstUsableSector}"));
        }
            
        OnInformationMessage($"- Partition number '{guidPartitionTable.Partitions.Count + 1}'");
        OnInformationMessage($"- Type '{type.ToString().ToUpper()}'");
        OnInformationMessage($"- Guid Partition Type '{gptPartitionTypeResult.Value}'");
        OnInformationMessage($"- Size '{partitionSize.FormatBytes()}' ({partitionSize} bytes)");
        OnInformationMessage($"- Start sector '{start}'");
        OnInformationMessage($"- End sector '{end}'");
        OnInformationMessage($"- Name '{name}'");

        // create guid partition
        guidPartitionTable.Create(start, end, gptPartitionTypeResult.Value, 0, name);
            
        return new Result();
    }

    private Result<Guid> GetGptPartitionType()
    {
        if (Guid.TryParse(type, out var parsedGuid))
        {
            return new Result<Guid>(parsedGuid);
        }

        if (!Enum.TryParse<GptPartType>(type, true, out var gptPartType))
        {
            return new Result<Guid>(new Error($"Unsupported partition type '{type}'"));
        }

        return gptPartType switch
        {
            GptPartType.Fat32 => new Result<Guid>(GuidPartitionTypes.WindowsBasicData),
            GptPartType.Ntfs => new Result<Guid>(GuidPartitionTypes.WindowsBasicData),
            GptPartType.ExFat => new Result<Guid>(GuidPartitionTypes.WindowsBasicData),
            _ => new Result<Guid>(new Error($"Unsupported partition type '{type}'"))
        };
    }
}