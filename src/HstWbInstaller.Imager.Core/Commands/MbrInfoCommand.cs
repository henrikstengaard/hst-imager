namespace HstWbInstaller.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DiscUtils.Partitions;
    using DiscUtils.Raw;
    using DiscUtils.Streams;
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
        
        public override async Task<Result> Execute(CancellationToken token)
        {
            OnProgressMessage($"Opening '{path}' for read");

            var physicalDrivesList = physicalDrives.ToList();
            var mediaResult = commandHelper.GetReadableMedia(physicalDrivesList, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }
            using var media = mediaResult.Value;
            await using var stream = media.Stream;
            
            using var disk = new Disk(stream, Ownership.None);
            
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
            
            OnMbrInfoRead(new MbrInfo
            {
                Path = path,
                DiskSize = disk.Capacity,
                Sectors = disk.Geometry.TotalSectorsLong,
                BlockSize = disk.BlockSize,
                Partitions = biosPartitionTable.Partitions.Select(x => CreateMbrPartition(x, disk.BlockSize)).ToList()
            });

            await disk.Content.DisposeAsync();
            disk.Dispose();

            return new Result();
        }

        private MbrPartition CreateMbrPartition(DiscUtils.Partitions.PartitionInfo partition, int blockSize)
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