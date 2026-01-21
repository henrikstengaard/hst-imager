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
            var srcResolvedMediaResult = commandHelper.ResolveMedia(sourcePath);
            if (srcResolvedMediaResult.IsFaulted)
            {
                return new Result(srcResolvedMediaResult.Error);
            }

            OnDebugMessage($"Media Path: '{srcResolvedMediaResult.Value.MediaPath}'");
            OnDebugMessage($"Virtual Path: '{srcResolvedMediaResult.Value.FileSystemPath}'");            

            OnDebugMessage($"Opening source path '{sourcePath}' as readable");

            var srcMediaResult = await commandHelper.GetReadableFileMedia(srcResolvedMediaResult.Value.MediaPath, srcResolvedMediaResult.Value.Modifiers);
            if (srcMediaResult.IsFaulted)
            {
                return new Result(srcMediaResult.Error);
            }

            // get pistorm rdb media from src media
            var srcPiStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(
                srcMediaResult.Value, srcResolvedMediaResult.Value.FileSystemPath,
                srcResolvedMediaResult.Value.DirectorySeparatorChar);

            using var srcMedia = srcPiStormRdbMediaResult.Media;
            var srcStream = srcMedia.Stream;
            var srcFileSystemPath = srcPiStormRdbMediaResult.FileSystemPath;

            // get start offset and source size from src media
            var startOffsetAndSizeResult = await MediaHelper.GetStartOffsetAndSize(commandHelper, srcMedia,
                srcFileSystemPath);
            if (startOffsetAndSizeResult.IsFaulted)
            {
                return new Result(startOffsetAndSizeResult.Error);
            }
            
            var (srcStartOffset, srcSize) = startOffsetAndSizeResult.Value;

            OnInformationMessage($"Source start offset '{srcStartOffset}'");
            OnInformationMessage($"Source size '{srcSize.FormatBytes()}' ({srcSize} bytes)");

            // add start offset
            if (srcStart.HasValue)
            {
                srcStartOffset += srcStart.Value;
            }

            var transferSize = srcSize.ResolveSize(size);
            OnInformationMessage($"Size '{transferSize.FormatBytes()}' ({transferSize} bytes)");
            
            // resolve media path
            var destResolvedMediaResult = commandHelper.ResolveMedia(destinationPath);
            if (destResolvedMediaResult.IsFaulted && destResolvedMediaResult.Error is not PathNotFoundError)
            {
                return new Result(destResolvedMediaResult.Error);
            }

            if (destResolvedMediaResult.IsSuccess)
            {
                OnDebugMessage($"Media Path: '{destResolvedMediaResult.Value.MediaPath}'");
                OnDebugMessage($"Virtual Path: '{destResolvedMediaResult.Value.FileSystemPath}'");            
            }

            OnDebugMessage($"Opening destination path '{destinationPath}' as writable");

            var destMediaPath = destResolvedMediaResult.Value?.MediaPath ?? destinationPath;
            var destVirtualPath = destResolvedMediaResult.Value?.FileSystemPath ?? string.Empty;
            var createDestMedia = destResolvedMediaResult.IsFaulted && destResolvedMediaResult.Error is PathNotFoundError;
            var directorySeparatorChar = destResolvedMediaResult.Value?.DirectorySeparatorChar ??
                                         srcResolvedMediaResult.Value.DirectorySeparatorChar;

            var destinationMediaResult = createDestMedia
                ? await commandHelper.GetWritableFileMedia(destMediaPath, size: transferSize, create: true)
                : await commandHelper.GetWritableFileMedia(destMediaPath);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            // get pistorm rdb media from dest media
            var destPiStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(
                destinationMediaResult.Value, destVirtualPath,
                directorySeparatorChar);

            using var destMedia = destPiStormRdbMediaResult.Media;
            var destStream = destMedia.Stream;
            var destFileSystemPath = destPiStormRdbMediaResult.FileSystemPath;
            
            var destStartOffset = destStart ?? 0;
            
            if (!createDestMedia)
            {
                // get dest offset and size
                var destStartOffsetAndSizeResult = await MediaHelper.GetStartOffsetAndSize(commandHelper,
                    destMedia, destFileSystemPath);
                if (destStartOffsetAndSizeResult.IsFaulted)
                {
                    return new Result(destStartOffsetAndSizeResult.Error);
                }

                destStartOffset += destStartOffsetAndSizeResult.Value.Item1;
            }
            
            using var streamCopier = new StreamCopier(verify: verify, retries: 0);
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
            var result = await streamCopier.Copy(token, srcStream, destStream, transferSize, srcStartOffset, destStartOffset, skipZeroFilled);
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