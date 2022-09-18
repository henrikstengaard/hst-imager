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

    public class MbrInfoCommand : CommandBase
    {
        private readonly ILogger<MbrInfoCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;

        public MbrInfoCommand(ILogger<MbrInfoCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
        }

        public event EventHandler<MbrInfoReadEventArgs> MbrInfoRead;
        
        public override Task<Result> Execute(CancellationToken token)
        {
            var physicalDrive =
                physicalDrives.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            
            if (physicalDrive != null)
            {
                return Task.FromResult(new Result(new Error("MBR does not support physical drives")));
            }
            
            OnProgressMessage($"Opening '{path}' for read/write");

            if (!File.Exists(path))
            {
                return Task.FromResult(new Result(new Error($"Image file '{path}' not found")));
            }
            
            using var disk = VirtualDisk.OpenDisk(path, FileAccess.ReadWrite);

            OnProgressMessage("Reading Master Boot Record");

            BiosPartitionTable biosPartitionTable;
            try
            {
                biosPartitionTable = new BiosPartitionTable(disk);
            }
            catch (Exception)
            {
                return Task.FromResult(new Result(new Error("Master Boot Record not found")));
            }
            
            OnMbrInfoRead(new MbrInfo
            {
                Path = path,
                DiskSize = disk.Capacity,
                Sectors = disk.Geometry.TotalSectorsLong,
                BlockSize = disk.BlockSize,
                Partitions = biosPartitionTable.Partitions.Select(x => CreateMbrPartition(x, disk.BlockSize)).ToList()
            });
            
            return Task.FromResult(new Result());
        }

        private MbrPartition CreateMbrPartition(PartitionInfo partition, int blockSize)
        {
            var partitionSize = (partition.LastSector - partition.FirstSector) * blockSize;
            return new MbrPartition
            {
                Type = partition.TypeAsString,
                FirstSector = partition.FirstSector,
                LastSector = partition.LastSector,
                PartitionSize = partitionSize
            };
        }
        
        protected virtual void OnMbrInfoRead(MbrInfo rdbInfo)
        {
            MbrInfoRead?.Invoke(this, new MbrInfoReadEventArgs(rdbInfo));
        }        
    }
}