using Hst.Imager.Core.Helpers;

namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Core;
    using Size = Models.Size;

    public class TransferCommand(
        ICommandHelper commandHelper,
        string sourcePath,
        string destinationPath,
        Size size,
        bool verify,
        long? srcStart,
        long? destStart)
        : CommandBase
    {
        private long statusBytesProcessed = 0;
        private TimeSpan statusTimeElapsed = TimeSpan.Zero;

        public event EventHandler<IoErrorEventArgs> SrcError;
        public event EventHandler<IoErrorEventArgs> DestError;

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Transferring from source image file '{sourcePath}' to destination image file '{destinationPath}'");
            
            // resolve media path
            var srcMediaResult = commandHelper.ResolveMedia(sourcePath);
            if (srcMediaResult.IsFaulted)
            {
                return new Result(srcMediaResult.Error);
            }

            OnDebugMessage($"Media Path: '{srcMediaResult.Value.MediaPath}'");
            OnDebugMessage($"Virtual Path: '{srcMediaResult.Value.FileSystemPath}'");            

            OnDebugMessage($"Opening source path '{sourcePath}' as readable");

            var sourceMediaResult = await commandHelper.GetReadableFileMedia(srcMediaResult.Value.MediaPath, srcMediaResult.Value.Modifiers);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            // get src media and stream
            using var srcMedia = sourceMediaResult.Value;
            var srcStream = srcMedia.Stream;

            // get src offset and source size
            var srcStartOffsetAndSizeResult = await MediaHelper.GetStartOffsetAndSize(commandHelper, srcMedia,
                srcMediaResult.Value.FileSystemPath);
            if (srcStartOffsetAndSizeResult.IsFaulted)
            {
                return new Result(srcStartOffsetAndSizeResult.Error);
            }
            
            var (srcStartOffset, srcSize) = srcStartOffsetAndSizeResult.Value;
            
            // add start offset
            if (srcStart.HasValue)
            {
                srcStartOffset += srcStart.Value;
            }

            OnDebugMessage($"Source start offset '{srcStartOffset}'");
            OnDebugMessage($"Source size '{srcSize.FormatBytes()}' ({srcSize} bytes)");
            
            var transferSize = srcSize.ResolveSize(size);
            OnInformationMessage($"Size '{transferSize.FormatBytes()}' ({transferSize} bytes)");
            
            // resolve media path
            var destMediaResult = commandHelper.ResolveMedia(destinationPath);
            if (destMediaResult.IsFaulted && destMediaResult.Error is not PathNotFoundError)
            {
                return new Result(destMediaResult.Error);
            }

            if (destMediaResult.IsSuccess)
            {
                OnDebugMessage($"Media Path: '{destMediaResult.Value.MediaPath}'");
                OnDebugMessage($"Virtual Path: '{destMediaResult.Value.FileSystemPath}'");            
            }

            OnDebugMessage($"Opening destination path '{destinationPath}' as writable");

            var destMediaPath = destMediaResult.Value?.MediaPath ?? destinationPath;
            var destVirtualPath = destMediaResult.Value?.FileSystemPath ?? string.Empty;
            var createDestMedia = destMediaResult.IsFaulted && destMediaResult.Error is PathNotFoundError;

            var destinationMediaResult = createDestMedia
                ? await commandHelper.GetWritableFileMedia(destMediaPath, size: transferSize, create: true)
                : await commandHelper.GetWritableFileMedia(destMediaPath);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            using var destinationMedia = destinationMediaResult.Value;
            var destinationStream = destinationMedia.Stream;

            var destStartOffset = destStart ?? 0;
            
            if (!createDestMedia)
            {
                // get dest offset and size
                var destStartOffsetAndSizeResult = await MediaHelper.GetStartOffsetAndSize(commandHelper,
                    destinationMedia, destVirtualPath);
                if (destStartOffsetAndSizeResult.IsFaulted)
                {
                    return new Result(destStartOffsetAndSizeResult.Error);
                }

                destStartOffset += destStartOffsetAndSizeResult.Value.Item1;
            }
            
            var streamCopier = new StreamCopier(verify: verify, retries: 0);
            streamCopier.DataProcessed += (_, e) =>
            {
                statusBytesProcessed = e.BytesProcessed;
                statusTimeElapsed = e.TimeElapsed;
                OnDataProcessed(e.Indeterminate, e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal, e.BytesPerSecond);
            };
            streamCopier.SrcError += (_, args) => OnSrcError(args);
            streamCopier.DestError += (_, args) => OnDestError(args);
            
            var skipZeroFilled = commandHelper.IsVhd(destinationPath);
            var result = await streamCopier.Copy(token, srcStream, destinationStream, transferSize, srcStartOffset, destStartOffset, skipZeroFilled);
            if (result.IsFaulted)
            {
                return new Result(result.Error);
            }
            
            if (statusBytesProcessed != transferSize)
            {
                return new Result(new Error($"Transferred '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) is not equal to size '{transferSize.FormatBytes()}' ({transferSize} bytes)"));
            }

            OnInformationMessage($"Transferred '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) in {statusTimeElapsed.FormatElapsed()}");

            return new Result();
        }

        private void OnSrcError(IoErrorEventArgs args) => SrcError?.Invoke(this, args);

        private void OnDestError(IoErrorEventArgs args) => DestError?.Invoke(this, args);
    }
}