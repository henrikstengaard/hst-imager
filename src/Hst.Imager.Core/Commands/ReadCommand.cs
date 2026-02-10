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

    public class ReadCommand(
        ILogger<ReadCommand> logger,
        ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives,
        string sourcePath,
        string destinationPath,
        Size size,
        int retries,
        bool verify,
        bool force,
        long? start)
        : CommandBase
    {
        private readonly ILogger<ReadCommand> logger = logger;
        private long statusBytesProcessed = 0;
        private TimeSpan statusTimeElapsed = TimeSpan.Zero;

        public event EventHandler<IoErrorEventArgs> SrcError;
        public event EventHandler<IoErrorEventArgs> DestError;

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Reading from '{sourcePath}' to '{destinationPath}'");
            
            // resolve media path
            var srcResolvedMediaResult = commandHelper.ResolveMedia(sourcePath);
            if (srcResolvedMediaResult.IsFaulted)
            {
                return new Result(srcResolvedMediaResult.Error);
            }

            OnDebugMessage($"Media Path: '{srcResolvedMediaResult.Value.MediaPath}'");
            OnDebugMessage($"Virtual Path: '{srcResolvedMediaResult.Value.FileSystemPath}'");            

            OnDebugMessage($"Opening '{sourcePath}' as readable");
            
            var physicalDrivesList = physicalDrives.ToList();
            
            var srcMediaResult = await commandHelper.GetReadableMedia(physicalDrivesList,
                srcResolvedMediaResult.Value.MediaPath, srcResolvedMediaResult.Value.Modifiers);
            if (srcMediaResult.IsFaulted)
            {
                return new Result(srcMediaResult.Error);
            }
            
            // get pistorm rdb media from src media
            var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(
                srcMediaResult.Value, srcResolvedMediaResult.Value.FileSystemPath,
                srcResolvedMediaResult.Value.DirectorySeparatorChar);

            using var srcMedia = piStormRdbMediaResult.Media;
            var srcStream = srcMedia.Stream;
            var srcFileSystemPath = piStormRdbMediaResult.FileSystemPath;
            
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

            // add src start offset, if defined
            if (start.HasValue)
            {
                srcStartOffset += start.Value;
            }
            
            var readSize = srcSize.ResolveSize(size);
            OnInformationMessage($"Size '{readSize.FormatBytes()}' ({readSize} bytes)");
            
            OnDebugMessage($"Opening '{destinationPath}' as writable");
            
            var destMediaResult = await commandHelper.GetWritableFileMedia(destinationPath, size: readSize, create: true);
            if (destMediaResult.IsFaulted)
            {
                return new Result(destMediaResult.Error);
            }
            
            // get dest media and stream
            using var destMedia = destMediaResult.Value;
            var destStream = MediaHelper.GetStreamFromMedia(destMedia);

            var isVhd = commandHelper.IsVhd(destinationPath);
            var isZip = commandHelper.IsZip(destinationPath);
            var isGZip = commandHelper.IsGZip(destinationPath);
            if (!isVhd && !isZip && !isGZip)
            {
                destStream.SetLength(readSize);
            }
            
            using var streamCopier = new StreamCopier(verify: verify, retries: retries, force: force);
            streamCopier.DataProcessed += (_, e) =>
            {
                statusBytesProcessed = e.BytesProcessed;
                statusTimeElapsed = e.TimeElapsed;
                OnDataProcessed(e.Indeterminate, e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal, e.BytesPerSecond);
            };
            streamCopier.SrcError += (_, args) => OnSrcError(args);
            streamCopier.DestError += (_, args) => OnDestError(args);

            var result = await streamCopier.Copy(token, srcStream, destStream, readSize, srcStartOffset, 0, isVhd);
            if (result.IsFaulted)
            {
                return new Result(result.Error);
            }
            
            if (statusBytesProcessed != readSize)
            {
                return new Result(new Error($"Read '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) is not equal to size '{readSize.FormatBytes()}' ({readSize} bytes)"));
            }

            OnInformationMessage($"Read '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) in {statusTimeElapsed.FormatElapsed()}");
            
            return new Result();
        }

        private void OnSrcError(IoErrorEventArgs args) => SrcError?.Invoke(this, args);

        private void OnDestError(IoErrorEventArgs args) => DestError?.Invoke(this, args);
    }
}