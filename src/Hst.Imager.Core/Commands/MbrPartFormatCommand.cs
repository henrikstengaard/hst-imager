namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DiscUtils.Fat;
    using DiscUtils.Partitions;
    using DiscUtils.Raw;
    using DiscUtils.Streams;
    using Hst.Core;
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

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Deleting partition from Master Boot Record at '{path}'");

            OnDebugMessage($"Opening '{path}' as writable");

            var physicalDrivesList = physicalDrives.ToList();
            var mediaResult = commandHelper.GetWritableMedia(physicalDrivesList, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }
            using var media = mediaResult.Value;
            await using var stream = media.Stream;
            
            using var disk = new Disk(stream, Ownership.None);
            
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

            OnDebugMessage($"Formatting partition number '{partitionNumber}'");

            if (partitionNumber < 1 || partitionNumber > biosPartitionTable.Partitions.Count)
            {
                return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
            }

            var partitionInfo = biosPartitionTable.Partitions[partitionNumber - 1];
            
            if (partitionInfo.BiosType != BiosPartitionTypes.Fat32)
            {
                return new Result(new Error("Unsupported partition type"));
            }

            OnDebugMessage($"Partition name '{name}'");
            
            using var fatFileSystem = FatFileSystem.FormatPartition(disk, partitionNumber - 1, name);
            
            await disk.Content.DisposeAsync();
            disk.Dispose();
            
            return new Result();
        }
    }
}