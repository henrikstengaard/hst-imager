namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
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
            var mediaResult = await commandHelper.GetWritableMedia(physicalDrivesList, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            using var media = mediaResult.Value;

            OnDebugMessage($"Media size '{media.Size}'");
        
            var diskInfo = await commandHelper.ReadDiskInfo(media);
            if (diskInfo == null)
            {
                return new Result(new Error("Failed to read disk info"));
            }
            
            OnDebugMessage("Reading Master Boot Record");
            
            var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new Disk(media.Stream, Ownership.None);

            BiosPartitionTable biosPartitionTable;
            try
            {
                biosPartitionTable = new BiosPartitionTable(disk);
            }
            catch (Exception)
            {
                return new Result(new Error("Master Boot Record not found"));
            }
            
            OnDebugMessage($"Disk size: {disk.Capacity.FormatBytes()} ({disk.Capacity} bytes)");
            OnDebugMessage($"Sectors: {disk.Geometry.Value.TotalSectorsLong}");
            OnDebugMessage($"Sector size: {disk.SectorSize} bytes");

            // available size and default start offset
            var availableSize = disk.Capacity;
            long startOffset = 512;

            // get rdb partition table
            var rdbPartitionTable =
                diskInfo.PartitionTables.FirstOrDefault(x =>
                    x.Type == PartitionTableType.RigidDiskBlock);

            // reduce available size, if rdb is present and set start offset after rdb
            if (rdbPartitionTable != null)
            {
                OnDebugMessage($"Rigid Disk Block found");
                startOffset = rdbPartitionTable.Size.ToSectorSize();
                availableSize = diskInfo.Size - startOffset;
            }

            // calculate partition size and sectors
            var partitionSize = size.Value == 0 && size.Unit == Unit.Bytes
                ? 0
                : availableSize.ResolveSize(size).ToSectorSize();
            
            // find unallocated part for partition size with start offset equal or larger
            var unallocatedPart = diskInfo.DiskParts
                .OrderByDescending(x => x.Size)
                .FirstOrDefault(x => x.PartType == PartType.Unallocated && x.StartOffset >= startOffset && x.Size >= partitionSize);
            if (unallocatedPart == null)
            {
                return new Result(new Error($"Master Boot Record does not have unallocated disk space for partition size '{size}' ({partitionSize} bytes)"));
            }

            var firstSector = rdbPartitionTable == null
                ? 1
                : rdbPartitionTable.Size / 512;
            
            // add rdb mbr gap, if rdb is present and mbr doesn't have any partitions
            if (rdbPartitionTable != null && !biosPartitionTable.Partitions.Any())
            {
                firstSector += RdbMbrGap / 512;
            }

            if (startSector.HasValue && startSector.Value < firstSector)
            {
                return new Result(new Error($"Invalid start sector '{startSector}' is less that first sector '{firstSector}'"));
            }
            
            // calculate start and end sector
            var start = startSector ?? unallocatedPart.StartOffset / 512;
            if (start == 0)
            {
                start = 1;
            }

            // set partition start sector to first sector, if less than first sector
            if (start < firstSector)
            {
                start = firstSector;
            }

            // calculate partition sectors
            var partitionSectors = (partitionSize == 0 ? unallocatedPart.Size : partitionSize) / 512;

            if (partitionSectors <= 0)
            {
                return new Result(new Error($"Invalid sectors for partition size '{partitionSize}', start sector '{start}', total sectors '{disk.Geometry.Value.TotalSectorsLong}'"));
            }

            var end = start + partitionSectors - 1;
            partitionSize = partitionSectors * 512;

            if (endSector.HasValue && end > endSector)
            {
                end = endSector.Value;
                partitionSectors = end - start + 1;
                partitionSize = partitionSectors * 512;
            }

            // set end to last sector, if end is larger than last sector
            if (end > disk.Geometry.Value.TotalSectorsLong)
            {
                end = disk.Geometry.Value.TotalSectorsLong;
                partitionSectors = end - start + 1;
                partitionSize = partitionSectors * 512;
            }
            
            // return error, if start it's less than start offset
            if (start < startOffset / 512)
            {
                return new Result(new Error($"Start sector {startSector} is overlapping reversed partition space"));
            }
            
            OnInformationMessage($"- Partition number '{biosPartitionTable.Partitions.Count + 1}'");
            OnInformationMessage($"- Type '{type.ToString().ToUpper()}'");
            OnInformationMessage($"- Size '{partitionSize.FormatBytes()}' ({partitionSize} bytes)");
            OnInformationMessage($"- Start sector '{start}'");
            OnInformationMessage($"- End sector '{end}'");
            OnInformationMessage($"- Active '{active}'");

            // create mbr partition
            biosPartitionTable.CreatePrimaryBySector(start, end, biosPartitionTypeResult.Value, active);

            return new Result();
        }

        private static Regex partitionTypeAsNumberRegex = new Regex("0x[0-9a-f]{1,2}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private Result<byte> GetBiosPartitionType()
        {
            if (partitionTypeAsNumberRegex.IsMatch(type))
            {
                return new Result<byte>((byte)Convert.ToInt32(type, 16));
            }

            if (!Enum.TryParse<MbrPartType>(type, true, out var mbrPartType))
            {
                return new Result<byte>(new Error($"Unsupported partition type '{type}'"));
            }

            return mbrPartType switch
            {
                MbrPartType.Fat12 => new Result<byte>(BiosPartitionTypes.Fat12),
                MbrPartType.Fat16 => new Result<byte>(BiosPartitionTypes.Fat16),
                MbrPartType.Fat16Small => new Result<byte>(BiosPartitionTypes.Fat16Small),
                MbrPartType.Fat16Lba => new Result<byte>(BiosPartitionTypes.Fat16Lba),
                MbrPartType.Fat32 => new Result<byte>(BiosPartitionTypes.Fat32),
                MbrPartType.Fat32Lba => new Result<byte>(BiosPartitionTypes.Fat32Lba),
                MbrPartType.Ntfs => new Result<byte>(BiosPartitionTypes.Ntfs),
                MbrPartType.PiStormRdb => new Result<byte>(Core.Constants.BiosPartitionTypes.PiStormRdb),
                _ => new Result<byte>(new Error($"Unsupported partition type '{type}'"))
            };
        }
    }
}