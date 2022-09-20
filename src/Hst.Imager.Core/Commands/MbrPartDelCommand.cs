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
    using Hst.Core;
    using Microsoft.Extensions.Logging;

    public class MbrPartDelCommand : CommandBase
    {
        private readonly ILogger<MbrPartDelCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly int partitionNumber;

        public MbrPartDelCommand(ILogger<MbrPartDelCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.partitionNumber = partitionNumber;
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

            OnProgressMessage($"Deleting partition number '{partitionNumber}'");
            
            if (partitionNumber < 1 || partitionNumber > biosPartitionTable.Partitions.Count)
            {
                return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
            }
            
            biosPartitionTable.Delete(partitionNumber - 1);
            
            await disk.Content.DisposeAsync();
            disk.Dispose();
            
            return new Result();
        }
    }
}