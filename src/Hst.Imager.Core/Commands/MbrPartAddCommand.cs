namespace HstWbInstaller.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DiscUtils;
    using DiscUtils.Partitions;
    using DiscUtils.Raw;
    using DiscUtils.Streams;
    using Extensions;
    using Hst.Core;
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
            OnProgressMessage($"Opening '{path}' for read/write");

            var physicalDrivesList = physicalDrives.ToList();
            var mediaResult = commandHelper.GetWritableMedia(physicalDrivesList, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }
            using var media = mediaResult.Value;
            await using var stream = media.Stream;
            
            using var disk = new Disk(stream, Ownership.None);

            var diskSize = disk.Geometry.Capacity;
            var partitionSize = diskSize.ResolveSize(size);
            var partitionSectors = partitionSize / 512;
            
            OnProgressMessage("Reading Master Boot Record");
            
            BiosPartitionTable biosPartitionTable;
            try
            {
                biosPartitionTable = new BiosPartitionTable(disk);
            }
            catch (Exception)
            {
                return new Result(new Error("Master Boot Record not found"));
            }
            
            var start = startSector ?? 1;
            var end = start + partitionSectors - 1;

            if (end > disk.Geometry.TotalSectorsLong)
            {
                end = disk.Geometry.TotalSectorsLong;
                partitionSectors = end - start + 1;
                partitionSize = partitionSectors * 512;
            }
            
            OnProgressMessage($"Adding partition number '{biosPartitionTable.Partitions.Count + 1}'");
            OnProgressMessage($"Type '{type.ToUpper()}'");

            var biosPartitionTypeResult = GetBiosPartitionType();
            if (biosPartitionTypeResult.IsFaulted)
            {
                return new Result(biosPartitionTypeResult.Error);
            }
            
            OnProgressMessage($"Size '{partitionSize.FormatBytes()}' ({partitionSize} bytes)");
            OnProgressMessage($"Start sector '{start}'");
            OnProgressMessage($"End sector '{end}'");
            OnProgressMessage($"Active '{active}'");

            biosPartitionTable.CreatePrimaryBySector(start, end, biosPartitionTypeResult.Value, active);

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