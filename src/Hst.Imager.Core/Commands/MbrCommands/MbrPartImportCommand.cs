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
    using Hst.Imager.Core.Models;
    using Microsoft.Extensions.Logging;

    public class MbrPartImportCommand : CommandBase
    {
        private readonly ILogger<MbrPartImportCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string sourcePath;
        private readonly string destinationPath;
        private readonly string partition;
        private long statusBytesProcessed;
        private TimeSpan statusTimeElapsed;

        public MbrPartImportCommand(ILogger<MbrPartImportCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath, string destinationPath, string partition)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.sourcePath = sourcePath;
            this.destinationPath = destinationPath;
            this.partition = partition;
            this.statusBytesProcessed = 0;
            this.statusTimeElapsed = TimeSpan.Zero;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Importing partition from '{sourcePath}' to '{destinationPath}'");

            OnDebugMessage($"Opening source path '{sourcePath}' as readable");

            OnInformationMessage("Source:");
            OnInformationMessage($"- Path '{sourcePath}'");

            var sourceMediaResult =
                await commandHelper.GetReadableMedia(physicalDrives, sourcePath);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            using var sourceMedia = sourceMediaResult.Value;
            var sourceStream = sourceMedia.Stream;
            var sourceSize = sourceMedia.Size;

            OnInformationMessage($"- Size '{sourceSize.FormatBytes()}' ({sourceSize} bytes)");

            OnDebugMessage($"Opening destination path '{destinationPath}' as writable");

            OnInformationMessage("Destination:");
            OnInformationMessage($"- Path '{destinationPath}'");

            var destinationMediaResult =
                await commandHelper.GetWritableMedia(physicalDrives, destinationPath);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            using var destinationMedia = destinationMediaResult.Value;
            var destinationStream = destinationMedia.Stream;

            OnDebugMessage($"Reading disk info from destination path '{destinationPath}'");

            var destinationDiskInfo =
                await commandHelper.ReadDiskInfo(destinationMedia, PartitionTableType.MasterBootRecord);

            if (destinationDiskInfo.MbrPartitionTablePart == null)
            {
                return new Result(new Error("Master Boot Record not found"));
            }

            OnInformationMessage($"- Partition '{partition}'");

            var partitionPartInfo = GetPartitionPartInfo(destinationDiskInfo.MbrPartitionTablePart, partition);

            if (partitionPartInfo == null)
            {
                return new Result(new Error($"Invalid partition '{partition}'"));
            }

            if (sourceMedia.Size > partitionPartInfo.Size)
            {
                return new Result(new Error(
                    $"Source is '{partitionPartInfo.Size}' bytes and larger than partition size of '{partitionPartInfo.Size}' bytes"));
            }

            const int sourceOffset = 0;
            var destinationOffset = partitionPartInfo.StartOffset;

            OnInformationMessage($"- Type '{partitionPartInfo.BiosType}'");
            OnInformationMessage($"- Start offset '{partitionPartInfo.StartOffset}'");
            OnInformationMessage($"- End offset '{partitionPartInfo.EndOffset}'");
            OnInformationMessage($"- Start sector '{partitionPartInfo.StartSector}'");
            OnInformationMessage($"- End sector '{partitionPartInfo.EndSector}'");
            OnInformationMessage($"- Size '{partitionPartInfo.Size.FormatBytes()}' ({partitionPartInfo.Size} bytes)");

            OnDebugMessage(
                $"Importing partition from source offset '{sourceOffset}' to destination offset '{destinationOffset}'");

            var isVhd = commandHelper.IsVhd(destinationPath);

            using var streamCopier = new StreamCopier();
            streamCopier.DataProcessed += (_, e) =>
            {
                statusBytesProcessed = e.BytesProcessed;
                statusTimeElapsed = e.TimeElapsed;
                OnDataProcessed(e.Indeterminate, e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal,
                    e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal, e.BytesPerSecond);
            };
            await streamCopier.Copy(token, sourceStream, destinationStream, sourceSize, 0, destinationOffset,
                isVhd);

            OnInformationMessage(
                $"Imported '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) in {statusTimeElapsed.FormatElapsed()}");

            if (destinationMedia.IsPhysicalDrive)
            {
                await commandHelper.RescanPhysicalDrives();
            }

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
                    .FirstOrDefault(x =>
                        x.PartType == PartType.Partition && x.BiosType == ((int)partitionType).ToString());
            }

            return null;
        }
    }
}