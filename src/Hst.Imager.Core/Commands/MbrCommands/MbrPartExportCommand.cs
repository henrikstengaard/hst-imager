namespace Hst.Imager.Core.Commands.MbrCommands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DiscUtils.Partitions;
    using DiscUtils.Streams;
    using Extensions;
    using Hst.Core;
    using Hst.Imager.Core;
    using Hst.Imager.Core.Commands;
    using Microsoft.Extensions.Logging;
    using Models;

    public class MbrPartExportCommand : CommandBase
    {
        private readonly ILogger<MbrPartExportCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string sourcePath;
        private readonly string partition;
        private readonly string destinationPath;
        private long statusBytesProcessed;
        private TimeSpan statusTimeElapsed;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;

        public MbrPartExportCommand(ILogger<MbrPartExportCommand> logger, ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath, string partition, string destinationPath)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.sourcePath = sourcePath;
            this.partition = partition;
            this.destinationPath = destinationPath;
            statusBytesProcessed = 0;
            statusTimeElapsed = TimeSpan.Zero;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Exporting partition from '{sourcePath}' to '{destinationPath}'");

            OnInformationMessage("Source:");
            OnInformationMessage($"- Path '{sourcePath}'");

            OnDebugMessage($"Opening source path '{sourcePath}' as readable");

            var sourceMediaResult =
                await commandHelper.GetReadableMedia(physicalDrives, sourcePath);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            using var sourceMedia = sourceMediaResult.Value;
            var sourceDisk = sourceMedia is DiskMedia diskMedia
                ? diskMedia.Disk
                : new DiscUtils.Raw.Disk(sourceMedia.Stream, Ownership.None);

            var sourceDiskInfo = await commandHelper.ReadDiskInfo(sourceMedia, PartitionTableType.MasterBootRecord);

            if (sourceDiskInfo.MbrPartitionTablePart == null)
            {
                return new Result(new Error("Master Boot Record not found"));
            }

            OnInformationMessage($"- Partition '{partition}'");

            var partitionPartInfo = GetPartitionPartInfo(sourceDiskInfo.MbrPartitionTablePart, partition);

            if (partitionPartInfo == null)
            {
                return new Result(new Error($"Invalid partition '{partition}'"));
            }

            var sourceSize = partitionPartInfo.Size;
            var sourceOffset = partitionPartInfo.StartOffset;
            var sourceStream = sourceDisk.Content;

            OnInformationMessage($"- Type '{partitionPartInfo.BiosType}'");
            OnInformationMessage($"- Start offset '{partitionPartInfo.StartOffset}'");
            OnInformationMessage($"- End offset '{partitionPartInfo.EndOffset}'");
            OnInformationMessage($"- Start sector '{partitionPartInfo.StartSector}'");
            OnInformationMessage($"- End sector '{partitionPartInfo.EndSector}'");
            OnInformationMessage($"- Size '{sourceSize.FormatBytes()}' ({sourceSize} bytes)");

            OnDebugMessage($"Opening destination path '{destinationPath}' as writable");

            OnInformationMessage("Destination:");
            OnInformationMessage($"- Path '{destinationPath}'");

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

        private static PartInfo GetPartitionPartInfo(PartitionTablePart mbrPartitionTablePart, string partition)
        {
            if (int.TryParse(partition, out var partitionNumber))
            {
                return mbrPartitionTablePart.Parts
                    .FirstOrDefault(x => x.PartType == PartType.Partition && x.PartitionNumber == partitionNumber);
            }

            if (Enum.TryParse<MbrPartType>(partition, true, out var partitionType))
            {
                return mbrPartitionTablePart.Parts
                    .FirstOrDefault(x => x.PartType == PartType.Partition && x.BiosType == ((int)partitionType).ToString());
            }

            return null;
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