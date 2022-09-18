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

    public class MbrInitCommand : CommandBase
    {
        private readonly ILogger<MbrInitCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;

        public MbrInitCommand(ILogger<MbrInitCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
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

            logger.LogDebug("Initializing Master Boot Record");
            //logger.LogDebug($"Size '{mbrSize.FormatBytes()}' ({mbrSize} bytes)");
            using var disk = VirtualDisk.OpenDisk(path, FileAccess.ReadWrite);
            // var disk = Disk.Initialize(stream, Ownership.None, diskSize);
            //var disk = VirtualDisk.OpenDisk(path, FileAccess.ReadWrite);
            BiosPartitionTable.Initialize(disk);
            // var i = biosPartitionTable.CreatePrimaryBySector(20, 1000000, BiosPartitionTypes.Fat32, true);
            //
            // var p = biosPartitionTable.Partitions[i];
            // logger.LogDebug($"Sectors '{p.SectorCount}'");
            // logger.LogDebug($"First '{p.FirstSector}'");
            // logger.LogDebug($"Last '{p.LastSector}'");
            // //
            // //
            // using FatFileSystem fs = FatFileSystem.FormatPartition(disk, i, "AmigaMBRTest");

            // BiosPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsFat);

            disk.Content.Dispose();
            disk.Dispose();
            
            return Task.FromResult(new Result());
        }
    }
}