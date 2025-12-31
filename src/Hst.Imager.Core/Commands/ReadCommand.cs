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
            var mediaResult = commandHelper.ResolveMedia(sourcePath);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            OnDebugMessage($"Media Path: '{mediaResult.Value.MediaPath}'");
            OnDebugMessage($"Virtual Path: '{mediaResult.Value.FileSystemPath}'");            

            OnDebugMessage($"Opening '{sourcePath}' as readable");
            
            var physicalDrivesList = physicalDrives.ToList();
            
            var srcMediaResult = await commandHelper.GetReadableMedia(physicalDrivesList, mediaResult.Value.MediaPath, mediaResult.Value.Modifiers);
            if (srcMediaResult.IsFaulted)
            {
                return new Result(srcMediaResult.Error);
            }
            
            // get src media and stream
            using var srcMedia = srcMediaResult.Value;
            var srcStream = MediaHelper.GetStreamFromMedia(srcMedia);

            // get start offset and source size
            var startOffsetAndSizeResult = await MediaHelper.GetStartOffsetAndSize(commandHelper, srcMedia,
                mediaResult.Value.FileSystemPath);
            if (startOffsetAndSizeResult.IsFaulted)
            {
                return new Result(startOffsetAndSizeResult.Error);
            }
            
            var (startOffset, sourceSize) = startOffsetAndSizeResult.Value;

            OnDebugMessage($"Start offset '{startOffset}'");
            OnDebugMessage($"Source size '{sourceSize.FormatBytes()}' ({sourceSize} bytes)");

            // add start offset
            if (start.HasValue)
            {
                startOffset += start.Value;
            }
            
            var readSize = sourceSize.ResolveSize(size);
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

            var result = await streamCopier.Copy(token, srcStream, destStream, readSize, startOffset, 0, isVhd);
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