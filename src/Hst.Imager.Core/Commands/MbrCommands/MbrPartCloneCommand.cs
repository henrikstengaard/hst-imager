using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System;
using DiscUtils.Streams;
using Hst.Core;
using Hst.Imager.Core.Models;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using Hst.Imager.Core.Extensions;

namespace Hst.Imager.Core.Commands.MbrCommands
{
    /// <summary>
    /// Master Boor Record clone command clones the content of the partition from
    /// one Master Boot record to the same or another Master Boot Record partition.
    /// </summary>
    public class MbrPartCloneCommand : CommandBase
    {
        private readonly ILogger<MbrPartCloneCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string sourcePath;
        private readonly int srcPartitionNumber;
        private readonly string destPath;
        private readonly int destPartitionNumber;
        private long statusBytesProcessed;
        private TimeSpan statusTimeElapsed;

        public MbrPartCloneCommand(ILogger<MbrPartCloneCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string srcPath, int srcPartitionNumber, string destPath,
            int destPartitionNumber)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            sourcePath = srcPath;
            this.srcPartitionNumber = srcPartitionNumber;
            this.destPath = destPath;
            this.destPartitionNumber = destPartitionNumber;
            statusBytesProcessed = 0;
            statusTimeElapsed = TimeSpan.Zero;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Cloning partition from '{sourcePath}' to '{destPath}'");

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

            OnInformationMessage($"- Partition number '{srcPartitionNumber}'");

            var srcPartitionPartInfo = sourceDiskInfo.MbrPartitionTablePart.Parts
                .FirstOrDefault(x => x.PartType == PartType.Partition && x.PartitionNumber == srcPartitionNumber);

            if (srcPartitionPartInfo == null)
            {
                return new Result(new Error($"Invalid partition number '{srcPartitionNumber}'"));
            }

            var srcSize = srcPartitionPartInfo.Size;

            OnInformationMessage($"- Type '{srcPartitionPartInfo.BiosType}'");
            OnInformationMessage($"- Start offset '{srcPartitionPartInfo.StartOffset}'");
            OnInformationMessage($"- End offset '{srcPartitionPartInfo.EndOffset}'");
            OnInformationMessage($"- Start sector '{srcPartitionPartInfo.StartSector}'");
            OnInformationMessage($"- End sector '{srcPartitionPartInfo.EndSector}'");
            OnInformationMessage($"- Size '{srcSize.FormatBytes()}' ({srcSize} bytes)");

            OnDebugMessage($"Opening destination path '{destPath}' as writable");

            OnInformationMessage("Destination:");
            OnInformationMessage($"- Path '{destPath}'");

            var destinationMediaResult =
                await commandHelper.GetWritableMedia(physicalDrives, destPath);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            using var destMedia = destinationMediaResult.Value;
            var destDisk = destMedia is DiskMedia destDiskMedia
                ? destDiskMedia.Disk
                : new DiscUtils.Raw.Disk(destMedia.Stream, Ownership.None);

            var destDiskInfo = await commandHelper.ReadDiskInfo(destMedia, PartitionTableType.MasterBootRecord);

            if (destDiskInfo.MbrPartitionTablePart == null)
            {
                return new Result(new Error("Master Boot Record not found"));
            }

            OnInformationMessage($"- Partition number '{destPartitionNumber}'");

            var destPartitionPartInfo = destDiskInfo.MbrPartitionTablePart.Parts
                .FirstOrDefault(x => x.PartType == PartType.Partition && x.PartitionNumber == destPartitionNumber);

            if (destPartitionPartInfo == null)
            {
                return new Result(new Error($"Invalid partition number '{destPartitionNumber}'"));
            }

            var destSize = destPartitionPartInfo.Size;

            OnInformationMessage($"- Type '{destPartitionPartInfo.BiosType}'");
            OnInformationMessage($"- Start offset '{destPartitionPartInfo.StartOffset}'");
            OnInformationMessage($"- End offset '{destPartitionPartInfo.EndOffset}'");
            OnInformationMessage($"- Start sector '{destPartitionPartInfo.StartSector}'");
            OnInformationMessage($"- End sector '{destPartitionPartInfo.EndSector}'");
            OnInformationMessage($"- Size '{destSize.FormatBytes()}' ({destSize} bytes)");

            if (srcSize > destSize)
            {
                return new Result(new Error($"Source partition size '{srcSize}' is larger than destination partition size '{destSize}'"));
            }

            var srcOffset = srcPartitionPartInfo.StartOffset;
            var srcStream = sourceDisk.Content;

            var destOffset = destPartitionPartInfo.StartOffset;
            var destStream = destDisk.Content;

            OnDebugMessage($"Cloning partition from source offset '{srcOffset}' to destination offset '{destOffset}'");

            using var streamCopier = new StreamCopier();
            streamCopier.DataProcessed += (_, e) =>
            {
                statusBytesProcessed = e.BytesProcessed;
                statusTimeElapsed = e.TimeElapsed;
                OnDataProcessed(e.Indeterminate, e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal, e.BytesPerSecond);
            };
            await streamCopier.Copy(token, srcStream, destStream, srcSize, srcOffset, destOffset, false);

            OnInformationMessage($"Cloned '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) in {statusTimeElapsed.FormatElapsed()}");

            if (destMedia.IsPhysicalDrive)
            {
                await commandHelper.RescanPhysicalDrives();
            }

            return new Result();
        }
    }
}