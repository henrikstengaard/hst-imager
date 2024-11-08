namespace Hst.Imager.Core.Commands.MbrCommands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Core;
    using Hst.Imager.Core;
    using Hst.Imager.Core.Commands;
    using Microsoft.Extensions.Logging;

    public class MbrPartImportCommand : CommandBase
    {
        private readonly ILogger<MbrPartImportCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string sourcePath;
        private readonly string destinationPath;
        private readonly int partitionNumber;
        private long statusBytesProcessed;
        private TimeSpan statusTimeElapsed;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;

        public MbrPartImportCommand(ILogger<MbrPartImportCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath, string destinationPath, int partitionNumber)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.sourcePath = sourcePath;
            this.destinationPath = destinationPath;
            this.partitionNumber = partitionNumber;
            this.statusBytesProcessed = 0;
            this.statusTimeElapsed = TimeSpan.Zero;
        }
        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Importing partition from '{sourcePath}' to '{destinationPath}'");

            OnDebugMessage($"Opening source path '{sourcePath}' as readable");

            var sourceMediaResult =
                await commandHelper.GetReadableMedia(physicalDrives, sourcePath);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            using var sourceMedia = sourceMediaResult.Value;
            var sourceStream = sourceMedia.Stream;

            OnDebugMessage($"Opening destination path '{destinationPath}' as writable");

            var destinationMediaResult =
                await commandHelper.GetWritableMedia(physicalDrives, destinationPath);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            using var destinationMedia = destinationMediaResult.Value;
            var destinationStream = destinationMedia.Stream;


            //OnInformationMessage(
            //    $"- Size '{partitionBlock.PartitionSize.FormatBytes()}' ({partitionBlock.PartitionSize} bytes)");
            //OnInformationMessage($"- Low Cyl '{partitionBlock.LowCyl}'");
            //OnInformationMessage($"- High Cyl '{partitionBlock.HighCyl}'");
            //OnInformationMessage($"- Reserved '{partitionBlock.Reserved}'");
            //OnInformationMessage($"- PreAlloc '{partitionBlock.PreAlloc}'");
            //OnInformationMessage($"- Buffers '{partitionBlock.NumBuffer}'");
            //OnInformationMessage($"- Max Transfer '{partitionBlock.MaxTransfer}'");
            //OnInformationMessage($"- File System Block Size '{partitionBlock.FileSystemBlockSize}'");

            //destinationRigidDiskBlock.PartitionBlocks =
            //    destinationRigidDiskBlock.PartitionBlocks.Concat(new[] { partitionBlock });

            //// calculate source cylinder size, offset and size
            //var destinationCylinderSize = destinationRigidDiskBlock.Heads * destinationRigidDiskBlock.Sectors *
            //                              destinationRigidDiskBlock.BlockSize;
            //var destinationOffset = (long)partitionBlock.LowCyl * destinationCylinderSize;


            //using var destinationMedia = sourceMediaResult.Value;
            //var sourceDisk = destinationMedia is DiskMedia diskMedia
            //    ? diskMedia.Disk
            //    : new DiscUtils.Raw.Disk(destinationMedia.Stream, Ownership.None);

            var sourceDiskInfo = await commandHelper.ReadDiskInfo(destinationMedia, PartitionTableType.MasterBootRecord);

            if (sourceDiskInfo.MbrPartitionTablePart == null)
            {
                return new Result(new Error("Master Boot Record not found"));
            }

            OnInformationMessage($"- Partition number '{partitionNumber}'");

            var partitionPartInfo = sourceDiskInfo.MbrPartitionTablePart.Parts
                .FirstOrDefault(x => x.PartType == PartType.Partition && x.PartitionNumber == partitionNumber);

            if (partitionPartInfo == null)
            {
                return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
            }

            if (sourceMedia.Size > partitionPartInfo.Size)
            {
                return new Result(new Error($"Source is '{partitionPartInfo.Size}' bytes and larger than partition number '{partitionNumber}' of '{partitionPartInfo.Size}' bytes"));
            }


            //OnDebugMessage($"Copying source partition number '{partitionNumber}'");

            //var destinationSizeSize = partitionPartInfo.Size;
            var sourceSize = sourceMedia.Size;
            const int sourceOffset = 0;
            var destinationOffset = partitionPartInfo.StartOffset;
            //var sourceStream = sourceDisk.Content;

            OnInformationMessage("Destination:");
            OnInformationMessage($"Partition number '{partitionNumber}'");
            OnInformationMessage($"Type '{partitionPartInfo.BiosType}'");
            OnInformationMessage($"Start offset '{partitionPartInfo.StartOffset}'");
            OnInformationMessage($"End offset '{partitionPartInfo.EndOffset}'");
            OnInformationMessage($"Start sector '{partitionPartInfo.StartSector}'");
            OnInformationMessage($"End sector '{partitionPartInfo.EndSector}'");
            OnInformationMessage($"Size '{sourceSize.FormatBytes()}' ({sourceSize} bytes)");


            OnDebugMessage($"Importing partition from source offset '{sourceOffset}' to destination offset '{destinationOffset}'");

            var isVhd = commandHelper.IsVhd(destinationPath);

            var streamCopier = new StreamCopier();
            streamCopier.DataProcessed += (_, e) =>
            {
                statusBytesProcessed = e.BytesProcessed;
                statusTimeElapsed = e.TimeElapsed;
                OnDataProcessed(e.Indeterminate, e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal, e.BytesPerSecond);
            };
            await streamCopier.Copy(token, sourceStream, destinationStream, sourceStream.Length, 0, destinationOffset,
                isVhd);

            OnInformationMessage($"Imported '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) in {statusTimeElapsed.FormatElapsed()}");

            return new Result();
        }

        private void OnDataProcessed(bool indeterminate, double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond)
        {
            DataProcessed?.Invoke(this,
                new DataProcessedEventArgs(indeterminate, percentComplete, bytesProcessed, bytesRemaining, bytesTotal, timeElapsed,
                    timeRemaining, timeTotal, bytesPerSecond));
        }
    }
}