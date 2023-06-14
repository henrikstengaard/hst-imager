﻿namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DiscUtils.Partitions;
    using DiscUtils.Raw;
    using DiscUtils.Streams;
    using Extensions;
    using Hst.Core;
    using Hst.Core.Extensions;
    using Microsoft.Extensions.Logging;
    using Models;
    using Size = Models.Size;

    public class MbrPartAddCommand : CommandBase
    {
        private readonly ILogger<MbrPartAddCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly string type;
        private readonly Size size;
        private readonly long? startSector;
        private readonly long? endSector;
        private readonly bool active;
        private const int RdbMbrGap = 512 * 1024;

        public MbrPartAddCommand(ILogger<MbrPartAddCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, string type, Size size,
            long? startSector, long? endSector, bool active = false)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.type = type;
            this.size = size;
            this.startSector = startSector;
            this.endSector = endSector;
            this.active = active;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            // get bios partition type
            var biosPartitionTypeResult = GetBiosPartitionType();
            if (biosPartitionTypeResult.IsFaulted)
            {
                return new Result(biosPartitionTypeResult.Error);
            }

            OnInformationMessage($"Adding partition to Master Boot Record at '{path}'");
            
            OnDebugMessage($"Opening '{path}' for read/write");

            var physicalDrivesList = physicalDrives.ToList();
            var mediaResult = commandHelper.GetWritableMedia(physicalDrivesList, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            using var media = mediaResult.Value;

            OnDebugMessage($"Media size '{media.Size}'");
        
            var diskInfo = await commandHelper.ReadDiskInfo(media, media.Stream);
            if (diskInfo == null)
            {
                return new Result(new Error("Failed to read disk info"));
            }
            
            OnDebugMessage("Reading Master Boot Record");
            
            // open stream as disk
            using var disk = new Disk(media.Stream, Ownership.None);
            
            BiosPartitionTable biosPartitionTable;
            try
            {
                biosPartitionTable = new BiosPartitionTable(disk);
            }
            catch (Exception e)
            {
                return new Result(new Error("Master Boot Record not found"));
            }
            
            // available size and default start offset
            var availableSize = disk.Geometry.Capacity;
            long startOffset = 512;
            
            // get rdb partition table
            var rdbPartitionTable =
                diskInfo.PartitionTables.FirstOrDefault(x =>
                    x.Type == PartitionTableType.RigidDiskBlock);

            // reduce available size, if rdb is present and set start offset after rdb
            if (rdbPartitionTable != null)
            {
                startOffset = rdbPartitionTable.Size.ToSectorSize();
                availableSize = diskInfo.Size - startOffset;
            }

            // calculate partition size and sectors
            var partitionSize = size.Value == 0 && size.Unit == Unit.Bytes
                ? 0
                : availableSize.ResolveSize(size).ToSectorSize();
            
            // find unallocated part for partition size
            var unallocatedPart = diskInfo.DiskParts.FirstOrDefault(x =>
                x.PartType == PartType.Unallocated && x.StartOffset >= startOffset && x.Size >= partitionSize);
            if (unallocatedPart == null)
            {
                return new Result(new Error($"Master Boot Record does not have unallocated disk space for partition size '{size}' ({partitionSize} bytes)"));
            }

            var firstSector = rdbPartitionTable == null
                ? 1
                : rdbPartitionTable.Size / 512;
            if (!biosPartitionTable.Partitions.Any())
            {
                firstSector += RdbMbrGap / 512;
            }

            if (startSector.HasValue && startOffset < firstSector)
            {
                return new Result(new Error($"Invalid start sector '{startSector}' is less that first sector '{firstSector}'"));
            }
            
            // calculate start and end sector
            var start = startSector ?? unallocatedPart.StartOffset / 512;
            if (start == 0)
            {
                start = 1;
            }

            var partitionSectors = partitionSize == 0
                ? disk.Geometry.TotalSectorsLong - start
                : partitionSize / 512;

            if (partitionSectors <= 0)
            {
                return new Result(new Error($"Invalid sectors for partition size '{partitionSize}', start sector '{start}', total sectors '{disk.Geometry.TotalSectorsLong}'"));
            }
            
            var end = start + partitionSectors - 1;
            partitionSize = partitionSectors * 512;

            // set end to last sector, if end is larger than last sector
            if (end > disk.Geometry.TotalSectorsLong)
            {
                end = disk.Geometry.TotalSectorsLong;
                partitionSectors = end - start + 1;
                partitionSize = partitionSectors * 512;
            }
            
            // return error, if start it's less than start offset
            if (start < startOffset / 512)
            {
                return new Result(new Error($"Start sector {startSector} is overlapping reversed partition space"));
            }
            
            OnInformationMessage($"- Partition number '{biosPartitionTable.Partitions.Count + 1}'");
            OnInformationMessage($"- Type '{type.ToUpper()}'");
            OnInformationMessage($"- Size '{partitionSize.FormatBytes()}' ({partitionSize} bytes)");
            OnInformationMessage($"- Start sector '{start}'");
            OnInformationMessage($"- End sector '{end}'");
            OnInformationMessage($"- Active '{active}'");

            // create mbr partition
            biosPartitionTable.CreatePrimaryBySector(start, end, biosPartitionTypeResult.Value, active);

            // dispose content and disk
            await disk.Content.DisposeAsync();
            disk.Dispose();
            
            return new Result();
        }

        private Result<byte> GetBiosPartitionType()
        {
            return type.ToLower() switch
            {
                "fat32" => new Result<byte>(BiosPartitionTypes.Fat32),
                _ => new Result<byte>(new Error($"Unsupported partition type '{type}'"))
            };
        }
    }
}