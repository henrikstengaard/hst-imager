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
    private readonly GptPartType type;
    private readonly string name;
    private readonly Size size;
    private readonly long? startSector;
    private readonly long? endSector;

    public GptPartAddCommand(ILogger<GptPartAddCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, GptPartType type, string name, Size size,
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
        // get well known partition type
        var wellKnownPartitionTypeResult = GetWellKnownPartitionType();
        if (wellKnownPartitionTypeResult.IsFaulted)
        {
            return new Result(wellKnownPartitionTypeResult.Error);
        }
        
        // get partition type guid
        var partitionTypeGuidResult = GetPartitionTypeGuid(wellKnownPartitionTypeResult.Value);

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
        
        OnDebugMessage($"Disk size: {disk.Capacity.FormatBytes()} ({disk.Capacity} bytes)");
        OnDebugMessage($"Sectors: {disk.Geometry.Value.TotalSectorsLong}");
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

        var diskSectors = disk.Geometry.Value.TotalSectorsLong;

        // calculate partition sectors
        var partitionSectors = (partitionSize == 0 ? unallocatedPart.Size : partitionSize) / disk.SectorSize;

        if (partitionSectors <= 0)
        {
            return new Result(new Error($"Invalid sectors for partition size '{partitionSize}', start sector '{start}', total sectors '{disk.Geometry.Value.TotalSectorsLong}'"));
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
        if (end > diskSectors)
        {
            end = diskSectors;
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
        OnInformationMessage($"- Size '{partitionSize.FormatBytes()}' ({partitionSize} bytes)");
        OnInformationMessage($"- Start sector '{start}'");
        OnInformationMessage($"- End sector '{end}'");
        OnInformationMessage($"- Name '{name}'");

        // create guid partition
        guidPartitionTable.Create(start, end, partitionTypeGuidResult.Value, 0, name);
            
        return new Result();
    }
    
    private Result<WellKnownPartitionType> GetWellKnownPartitionType()
    {
        return type switch
        {
            GptPartType.Fat32 => new Result<WellKnownPartitionType>(WellKnownPartitionType.WindowsFat),
            GptPartType.Ntfs => new Result<WellKnownPartitionType>(WellKnownPartitionType.WindowsNtfs),
            _ => new Result<WellKnownPartitionType>(new Error($"Unsupported partition type '{type}'"))
        };
    }
    
    private static Result<Guid> GetPartitionTypeGuid(WellKnownPartitionType wellKnownPartitionType)
    {
        return wellKnownPartitionType switch
        {
            WellKnownPartitionType.WindowsFat => new Result<Guid>(GuidPartitionTypes.WindowsBasicData),
            WellKnownPartitionType.WindowsNtfs => new Result<Guid>(GuidPartitionTypes.WindowsBasicData),
            WellKnownPartitionType.Linux => new Result<Guid>(GuidPartitionTypes.WindowsBasicData),
            WellKnownPartitionType.LinuxSwap => new Result<Guid>(GuidPartitionTypes.LinuxSwap),
            WellKnownPartitionType.LinuxLvm => new Result<Guid>(GuidPartitionTypes.LinuxLvm),
            _ => new Result<Guid>(new Error($"Unknown partition type '{wellKnownPartitionType}'"))
        };
    }
}