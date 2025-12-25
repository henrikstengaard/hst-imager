namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Core;
    using Hst.Imager.Core.Helpers;
    using Microsoft.Extensions.Logging;

    public class RdbPartExportCommand : CommandBase
    {
        private readonly ILogger<RdbPartExportCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string sourcePath;
        private readonly int partitionNumber;
        private readonly string destinationPath;
        private long statusBytesProcessed;
        private TimeSpan statusTimeElapsed;

        public RdbPartExportCommand(ILogger<RdbPartExportCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath, int partitionNumber, string destinationPath)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.sourcePath = sourcePath;
            this.partitionNumber = partitionNumber;
            this.destinationPath = destinationPath;
            this.statusBytesProcessed = 0;
            this.statusTimeElapsed = TimeSpan.Zero;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Exporting partition from '{sourcePath}' to '{destinationPath}'");
            
            OnDebugMessage($"Opening source path '{sourcePath}' as readable");

            var sourceMediaResult =
                await commandHelper.GetReadableMedia(physicalDrives, sourcePath);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            using var sourceMedia = MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, sourceMediaResult.Value, sourcePath);
            var sourceStream = sourceMedia.Stream;
            OnDebugMessage("Reading source Rigid Disk Block");

            var sourceRigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(sourceMedia);

            var sourcePartitionBlocks = sourceRigidDiskBlock.PartitionBlocks.ToList();

            OnDebugMessage($"Copying source partition number '{partitionNumber}'");

            if (partitionNumber < 1 || partitionNumber > sourcePartitionBlocks.Count)
            {
                return new Result(new Error($"Invalid source partition number '{partitionNumber}'"));
            }

            // get partition block to copy
            var partitionBlock = sourcePartitionBlocks[partitionNumber - 1];

            OnInformationMessage("Source:");
            OnInformationMessage($"Partition number '{partitionNumber}'");
            OnInformationMessage($"Name '{partitionBlock.DriveName}'");
            OnInformationMessage($"Low Cyl '{partitionBlock.LowCyl}'");
            OnInformationMessage($"High Cyl '{partitionBlock.HighCyl}'");
            OnInformationMessage($"Size '{partitionBlock.PartitionSize.FormatBytes()}' ({partitionBlock.PartitionSize} bytes)");

            // calculate source cylinder size, offset and size
            var sourceCylinderSize = sourceRigidDiskBlock.Heads * sourceRigidDiskBlock.Sectors *
                                     sourceRigidDiskBlock.BlockSize;
            var sourceOffset = (long)partitionBlock.LowCyl * sourceCylinderSize;
            var sourceSize = ((long)partitionBlock.HighCyl - partitionBlock.LowCyl + 1) * sourceCylinderSize;

            OnDebugMessage($"Opening destination path '{destinationPath}' as writable");

            var destinationMediaResult =
                await commandHelper.GetWritableMedia(physicalDrives, destinationPath, create: true);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            using var destinationMedia = destinationMediaResult.Value;
            var destinationStream = destinationMedia.Stream;
            const int destinationOffset = 0;

            OnDebugMessage($"Exporting partition from source offset '{sourceOffset}' to destination offset '{destinationOffset}'");
            
            var streamCopier = new StreamCopier();
            streamCopier.DataProcessed += (_, e) =>
            {
                statusBytesProcessed = e.BytesProcessed;
                statusTimeElapsed = e.TimeElapsed;
                OnDataProcessed(e.Indeterminate, e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal, e.BytesPerSecond);
            };
            await streamCopier.Copy(token, sourceStream, destinationStream, sourceSize, sourceOffset, destinationOffset, false);
            
            OnInformationMessage($"Exported '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) in {statusTimeElapsed.FormatElapsed()}");
            
            return new Result();
        }
    }
}