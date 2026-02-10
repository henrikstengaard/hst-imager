using Hst.Imager.Core.Helpers;

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
    using Models;

    public class WriteCommand(
        ILogger<WriteCommand> logger,
        ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives,
        string sourcePath,
        string destinationPath,
        Size size,
        int retries,
        bool verify,
        bool force,
        bool skipZeroFilled,
        long? start)
        : CommandBase
    {
        private readonly ILogger<WriteCommand> logger = logger;
        private long statusBytesProcessed = 0;
        private TimeSpan statusTimeElapsed = TimeSpan.Zero;

        public event EventHandler<IoErrorEventArgs> SrcError;
        public event EventHandler<IoErrorEventArgs> DestError;

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Writing source path '{sourcePath}' to destination path '{destinationPath}'");

            OnDebugMessage($"Opening '{sourcePath}' as readable");

            var sourceMediaResult = await commandHelper.GetReadableFileMedia(sourcePath);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            // get src media and stream
            using var srcMedia = sourceMediaResult.Value;
            var srcStream = MediaHelper.GetStreamFromMedia(srcMedia);

            var srcSize = srcMedia.Size;
            OnDebugMessage($"Source size '{srcSize.FormatBytes()}' ({srcSize} bytes)");

            var writeSize = Convert
                .ToInt64(size.Value == 0 ? srcSize : size.Value)
                .ResolveSize(size);
            OnInformationMessage($"Size '{writeSize.FormatBytes()}' ({writeSize} bytes)");

            OnDebugMessage($"Opening '{destinationPath}' as writable");

            // resolve media for destination path
            var destResolvedMediaResult = commandHelper.ResolveMedia(destinationPath);
            if (destResolvedMediaResult.IsFaulted)
            {
                return new Result(destResolvedMediaResult.Error);
            }

            OnDebugMessage($"Media Path: '{destResolvedMediaResult.Value.MediaPath}'");
            OnDebugMessage($"Virtual Path: '{destResolvedMediaResult.Value.FileSystemPath}'");             

            var physicalDrivesList = physicalDrives.ToList();

            var destMediaResult =
                await commandHelper.GetWritableMedia(physicalDrivesList, destResolvedMediaResult.Value.MediaPath, destResolvedMediaResult.Value.Modifiers);
            if (destMediaResult.IsFaulted)
            {
                return new Result(destMediaResult.Error);
            }

            // get pistorm rdb media from dest media
            var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(
                destMediaResult.Value, destResolvedMediaResult.Value.FileSystemPath,
                destResolvedMediaResult.Value.DirectorySeparatorChar);

            using var destMedia = piStormRdbMediaResult.Media;
            var destStream = destMedia.Stream;
            var destFileSystemPath = piStormRdbMediaResult.FileSystemPath;
            
            // get start offset and source size from dest media
            var startOffsetAndSizeResult = await MediaHelper.GetStartOffsetAndSize(commandHelper, destMedia,
                destFileSystemPath);
            if (startOffsetAndSizeResult.IsFaulted)
            {
                return new Result(startOffsetAndSizeResult.Error);
            }
            
            var (destStartOffset, destSize) = startOffsetAndSizeResult.Value;

            OnInformationMessage($"Destination start offset '{destStartOffset}'");
            OnInformationMessage($"Destination size '{destSize.FormatBytes()}' ({destSize} bytes)");

            // add destination start offset, if defined
            if (start.HasValue)
            {
                destStartOffset += start.Value;
            }
            
            if (writeSize > destSize)
            {
                return new Result(new WriteSizeTooLargeError(destSize, writeSize,
                        $"Source size {writeSize.FormatBytes()} ({writeSize} bytes) is too large for destination size {destSize.FormatBytes()} ({destSize} bytes)"));
            }

            using var streamCopier = new StreamCopier(verify: verify, retries: retries, force: force);
            streamCopier.DataProcessed += (_, e) =>
            {
                statusBytesProcessed = e.BytesProcessed;
                statusTimeElapsed = e.TimeElapsed;
                OnDataProcessed(e.Indeterminate, e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal,
                    e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal, e.BytesPerSecond);
            };
            streamCopier.SrcError += (_, args) => OnSrcError(args);
            streamCopier.DestError += (_, args) => OnDestError(args);

            var result = await streamCopier.Copy(token, srcStream, destStream, writeSize, 0L, 
                destStartOffset, skipZeroFilled);
            if (result.IsFaulted)
            {
                return new Result(result.Error);
            }

            if (writeSize != 0 && statusBytesProcessed != writeSize)
            {
                return new Result(new Error(
                    $"Written '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) is not equal to size '{writeSize.FormatBytes()}' ({writeSize} bytes)"));
            }

            OnInformationMessage(
                $"Written '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) in {statusTimeElapsed.FormatElapsed()}");

            if (destMedia.IsPhysicalDrive)
            {
                await commandHelper.RescanPhysicalDrives();
            }
            
            return new Result();
        }

        private void OnSrcError(IoErrorEventArgs args) => SrcError?.Invoke(this, args);

        private void OnDestError(IoErrorEventArgs args) => DestError?.Invoke(this, args);
    }
}