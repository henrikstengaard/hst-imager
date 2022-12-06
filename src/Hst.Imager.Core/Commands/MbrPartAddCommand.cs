namespace Hst.Imager.Core.Commands
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
    using Size = Models.Size;

    public class MbrPartAddCommand : CommandBase
    {
        private readonly ILogger<RdbInitCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly string type;
        private readonly Size size;
        private readonly long? startSector;
        private readonly long? endSector;
        private readonly bool active;
        private const int RdbMbrGap = 512 * 1024;

        public MbrPartAddCommand(ILogger<RdbInitCommand> logger, ICommandHelper commandHelper,
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
            await using var stream = media.Stream;

            // read disk info
            var diskInfo = await commandHelper.ReadDiskInfo(media, stream);
            
            // open stream as disk
            using var disk = new Disk(stream, Ownership.None);
            
            // available size and default start offset
            var availableSize = disk.Geometry.Capacity;
            long startOffset = 512;
            
            // get rdb partition table
            var rdbPartitionTable =
                diskInfo.PartitionTables.FirstOrDefault(x =>
                    x.Type == PartitionTableInfo.PartitionTableType.RigidDiskBlock);

            // reduce available size, if rdb is present and set start offset after rdb
            if (rdbPartitionTable != null)
            {
                startOffset = (rdbPartitionTable.Size + RdbMbrGap).ToSectorSize();
                availableSize = diskInfo.Size - startOffset;
            }

            // return error, if start sector is defined and it's less than start offset
            if (startSector.HasValue && startSector < startOffset / 512)
            {
                return new Result(new Error($"Start sector {startSector} is overlapping Rigid Disk Block"));
            }
            
            // calculate partition size and sectors
            var partitionSize = availableSize.ResolveSize(size).ToSectorSize();
            var partitionSectors = partitionSize / 512;
            
            OnDebugMessage("Reading Master Boot Record");
            
            BiosPartitionTable biosPartitionTable;
            try
            {
                biosPartitionTable = new BiosPartitionTable(disk);
            }
            catch (Exception)
            {
                return new Result(new Error("Master Boot Record not found"));
            }
            
            // calculate start and end sector
            var start = startSector ?? startOffset / 512;
            var end = start + partitionSectors - 1;

            // set end to last sector, if end is larger than last sector
            if (end > disk.Geometry.TotalSectorsLong)
            {
                end = disk.Geometry.TotalSectorsLong;
                partitionSectors = end - start + 1;
                partitionSize = partitionSectors * 512;
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