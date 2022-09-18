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
    using Extensions;
    using HstWbInstaller.Core;
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

        public override Task<Result> Execute(CancellationToken token)
        {
            var physicalDrive =
                physicalDrives.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            
            if (physicalDrive != null)
            {
                return Task.FromResult(new Result(new Error("MBR does not support physical drives")));
            }
            
            using var disk = VirtualDisk.OpenDisk(path, FileAccess.ReadWrite);

            var diskSize = disk.Geometry.Capacity;
            var partitionSize = diskSize.ResolveSize(size);
            var partitionSectors = partitionSize / 512;
            
            logger.LogDebug("Reading Master Boot Record");
            
            var biosPartitionTable = new BiosPartitionTable(disk);
            
            var start = startSector ?? 1;
            var end = start + partitionSectors - 1;

            if (end > disk.Geometry.TotalSectorsLong)
            {
                end = disk.Geometry.TotalSectorsLong;
                partitionSectors = end - start + 1;
                partitionSize = partitionSectors * 512;
            }
            
            logger.LogDebug($"Adding partition number '{biosPartitionTable.Partitions.Count + 1}'");
            logger.LogDebug($"Type '{type.ToUpper()}'");

            var biosPartitionTypeResult = GetBiosPartitionType();
            if (biosPartitionTypeResult.IsFaulted)
            {
                return Task.FromResult(new Result(biosPartitionTypeResult.Error));
            }
            
            logger.LogDebug($"Size '{partitionSize.FormatBytes()}' ({partitionSize} bytes)");
            logger.LogDebug($"Start sector '{start}'");
            logger.LogDebug($"End sector '{end}'");
            logger.LogDebug($"Active '{active}'");

            biosPartitionTable.CreatePrimaryBySector(start, end, biosPartitionTypeResult.Value, active);

            disk.Content.Dispose();
            disk.Dispose();
            
            return Task.FromResult(new Result());
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