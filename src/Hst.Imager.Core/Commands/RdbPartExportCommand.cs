namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Core;
    using Microsoft.Extensions.Logging;

    public class RdbPartExportCommand : CommandBase
    {
        private readonly ILogger<RdbPartExportCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string sourcePath;
        private readonly int partitionNumber;
        private readonly string destinationPath;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;

        public RdbPartExportCommand(ILogger<RdbPartExportCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath, int partitionNumber, string destinationPath)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.sourcePath = sourcePath;
            this.partitionNumber = partitionNumber;
            this.destinationPath = destinationPath;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnProgressMessage($"Opening source path '{sourcePath}' as readable");

            var sourceMediaResult =
                commandHelper.GetReadableMedia(physicalDrives, sourcePath);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            using var sourceMedia = sourceMediaResult.Value;
            await using var sourceStream = sourceMedia.Stream;

            OnProgressMessage("Reading source Rigid Disk Block");

            var sourceRigidDiskBlock = await commandHelper.GetRigidDiskBlock(sourceStream);

            var sourcePartitionBlocks = sourceRigidDiskBlock.PartitionBlocks.ToList();

            OnProgressMessage($"Copying source partition number '{partitionNumber}'");

            if (partitionNumber < 1 || partitionNumber > sourcePartitionBlocks.Count)
            {
                return new Result(new Error($"Invalid source partition number '{partitionNumber}'"));
            }

            // get partition block to copy
            var partitionBlock = sourcePartitionBlocks[partitionNumber - 1];

            OnProgressMessage("Source:");
            OnProgressMessage($"Partition number '{partitionNumber}'");
            OnProgressMessage($"Name '{partitionBlock.DriveName}'");
            OnProgressMessage($"LowCyl '{partitionBlock.LowCyl}'");
            OnProgressMessage($"HighCyl '{partitionBlock.HighCyl}'");
            OnProgressMessage($"Size '{partitionBlock.PartitionSize.FormatBytes()}' ({partitionBlock.PartitionSize} bytes)");

            // calculate source cylinder size, offset and size
            var sourceCylinderSize = sourceRigidDiskBlock.Heads * sourceRigidDiskBlock.Sectors *
                                     sourceRigidDiskBlock.BlockSize;
            var sourceOffset = partitionBlock.LowCyl * sourceCylinderSize;
            var sourceSize = (partitionBlock.HighCyl - partitionBlock.LowCyl + 1) * sourceCylinderSize;

            OnProgressMessage($"Opening destination path '{destinationPath}' as writable");

            var destinationMediaResult =
                commandHelper.GetWritableMedia(physicalDrives, destinationPath, allowPhysicalDrive: false);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            using var destinationMedia = destinationMediaResult.Value;
            await using var destinationStream = destinationMedia.Stream;

            OnProgressMessage($"Exporting partition from '{sourcePath}' to '{destinationPath}'");
            
            var streamCopier = new StreamCopier();
            streamCopier.DataProcessed += (_, e) =>
            {
                OnDataProcessed(e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal);
            };
            await streamCopier.Copy(token, sourceStream, destinationStream, sourceSize, sourceOffset, 0, false);
            
            return new Result();
        }
        
        private void OnDataProcessed(double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal)
        {
            DataProcessed?.Invoke(this,
                new DataProcessedEventArgs(percentComplete, bytesProcessed, bytesRemaining, bytesTotal, timeElapsed,
                    timeRemaining, timeTotal));
        }
    }
}