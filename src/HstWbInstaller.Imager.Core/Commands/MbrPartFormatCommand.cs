namespace HstWbInstaller.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DiscUtils;
    using DiscUtils.Fat;
    using DiscUtils.Partitions;
    using HstWbInstaller.Core;
    using Microsoft.Extensions.Logging;

    public class MbrPartFormatCommand : CommandBase
    {
        private readonly ILogger<RdbInitCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly int partitionNumber;
        private readonly string name;

        public MbrPartFormatCommand(ILogger<RdbInitCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber, string name)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.partitionNumber = partitionNumber;
            this.name = name;
        }

        public override Task<Result> Execute(CancellationToken token)
        {
            logger.LogDebug($"Opening '{path}' for read/write");
            
            var physicalDrive =
                physicalDrives.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            
            if (physicalDrive != null)
            {
                return Task.FromResult(new Result(new Error("MBR does not support physical drives")));
            }
            
            using var disk = VirtualDisk.OpenDisk(path, FileAccess.ReadWrite);
            var biosPartitionTable = new BiosPartitionTable(disk);

            logger.LogDebug($"Formatting partition number '{partitionNumber}'");

            if (partitionNumber < 1 || partitionNumber > biosPartitionTable.Partitions.Count)
            {
                return Task.FromResult(new Result(new Error($"Invalid partition number '{partitionNumber}'")));
            }

            var partitionInfo = biosPartitionTable.Partitions[partitionNumber - 1];
            
            if (partitionInfo.BiosType != BiosPartitionTypes.Fat32)
            {
                return Task.FromResult(new Result(new Error("Unsupported partition type")));
            }

            logger.LogDebug($"Partition name '{name}'");
            
            using var fatFileSystem = FatFileSystem.FormatPartition(disk, partitionNumber - 1, name);
            
            disk.Content.Dispose();
            disk.Dispose();
            
            return Task.FromResult(new Result());
        }
    }
}