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
    using HstWbInstaller.Core;
    using Microsoft.Extensions.Logging;

    public class MbrPartDelCommand : CommandBase
    {
        private readonly ILogger<MbrPartDelCommand> logger;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly int partitionNumber;

        public MbrPartDelCommand(ILogger<MbrPartDelCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber)
        {
            this.logger = logger;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.partitionNumber = partitionNumber;
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

            logger.LogDebug("Reading Master Boot Record");
            
            var biosPartitionTable = new BiosPartitionTable(disk);

            OnProgressMessage($"Deleting partition number '{partitionNumber}'");
            
            if (partitionNumber < 1 || partitionNumber > biosPartitionTable.Partitions.Count)
            {
                return Task.FromResult(new Result(new Error($"Invalid partition number '{partitionNumber}'")));
            }
            
            biosPartitionTable.Delete(partitionNumber - 1);
            
            return Task.FromResult(new Result());
        }
    }
}