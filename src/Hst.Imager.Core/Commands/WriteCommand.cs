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
            var destResolveMediaResult = commandHelper.ResolveMedia(destinationPath);
            if (destResolveMediaResult.IsFaulted)
            {
                return new Result(destResolveMediaResult.Error);
            }

            OnDebugMessage($"Media Path: '{destResolveMediaResult.Value.MediaPath}'");
            OnDebugMessage($"Virtual Path: '{destResolveMediaResult.Value.FileSystemPath}'");             

            var physicalDrivesList = physicalDrives.ToList();

            var destMediaResult =
                await commandHelper.GetWritableMedia(physicalDrivesList, destResolveMediaResult.Value.MediaPath, destResolveMediaResult.Value.Modifiers);
            if (destMediaResult.IsFaulted)
            {
                return new Result(destMediaResult.Error);
            }

            // get dest media and stream
            using var destMedia = destMediaResult.Value;
            var destStream = MediaHelper.GetStreamFromMedia(destMedia);

            // get start offset and source size
            var startOffsetAndSizeResult = await MediaHelper.GetStartOffsetAndSize(commandHelper, destMedia,
                destResolveMediaResult.Value.FileSystemPath);
            if (startOffsetAndSizeResult.IsFaulted)
            {
                return new Result(startOffsetAndSizeResult.Error);
            }
            
            var (destStartOffset, destSize) = startOffsetAndSizeResult.Value;

            OnDebugMessage($"Destination start offset '{destStartOffset}'");
            OnDebugMessage($"Destination size '{destSize.FormatBytes()}' ({destSize} bytes)");

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