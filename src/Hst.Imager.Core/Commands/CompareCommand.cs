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

    public class CompareCommand : CommandBase
    {
        private readonly ILogger<CompareCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string sourcePath;
        private readonly long sourceStartOffset;
        private readonly string destinationPath;
        private readonly long destinationStartOffset;
        private readonly Size size;
        private readonly int retries;
        private readonly bool force;
        private readonly bool skipZeroFilled;
        private long statusBytesProcessed;
        private TimeSpan statusTimeElapsed;

        public event EventHandler<IoErrorEventArgs> SrcError;
        public event EventHandler<IoErrorEventArgs> DestError;

        public CompareCommand(ILogger<CompareCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath, long sourceStartOffset,
            string destinationPath, long destinationStartOffset, Size size, int retries, bool force,
            bool skipZeroFilled)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.sourcePath = sourcePath;
            this.sourceStartOffset = sourceStartOffset;
            this.destinationPath = destinationPath;
            this.destinationStartOffset = destinationStartOffset;
            this.size = size;
            this.retries = retries;
            this.force = force;
            this.skipZeroFilled = skipZeroFilled;
            this.statusBytesProcessed = 0;
            this.statusTimeElapsed = TimeSpan.Zero;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Compare source '{sourcePath}' and destination '{destinationPath}'");

            // resolve source media path
            var srcResolvedMediaResult = commandHelper.ResolveMedia(sourcePath);
            if (srcResolvedMediaResult.IsFaulted)
            {
                return new Result(srcResolvedMediaResult.Error);
            }

            OnDebugMessage($"Source Media Path: '{srcResolvedMediaResult.Value.MediaPath}'");
            OnDebugMessage($"Source Virtual Path: '{srcResolvedMediaResult.Value.FileSystemPath}'");            

            OnDebugMessage($"Opening source '{sourcePath}' as readable");

            var physicalDrivesList = physicalDrives.ToList();
            
            var sourceMediaResult = await commandHelper.GetReadableMedia(physicalDrivesList, 
                srcResolvedMediaResult.Value.MediaPath, srcResolvedMediaResult.Value.Modifiers);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }
            
            // get src media and stream
            using var srcMedia = sourceMediaResult.Value;
            var srcStream = MediaHelper.GetStreamFromMedia(srcMedia);

            OnDebugMessage($"Source media is '{srcMedia.GetType().Name}', path '{srcMedia.Path}', media type '{srcMedia.Type}' and size {srcMedia.Size} bytes");
            OnInformationMessage($"Source size '{srcMedia.Size.FormatBytes()}' ({srcMedia.Size} bytes)");

            // get start offset and source size
            var srcStartOffsetAndSizeResult = await MediaHelper.GetStartOffsetAndSize(commandHelper, srcMedia, 
                srcResolvedMediaResult.Value.FileSystemPath);
            if (srcStartOffsetAndSizeResult.IsFaulted)
            {
                return new Result(srcStartOffsetAndSizeResult.Error);
            }
            
            var (srcPartStartOffset, srcPartSize) = srcStartOffsetAndSizeResult.Value;

            OnInformationMessage($"Source part start offset '{srcPartStartOffset}' and size '{srcPartSize.FormatBytes()}' ({srcPartSize} bytes)");

            if (sourceStartOffset > 0)
            {
                OnDebugMessage($"Source start offset within part '{sourceStartOffset}'");
                srcPartStartOffset += sourceStartOffset;
            }
            
            // resolve destination media path
            var destResolvedMediaResult = commandHelper.ResolveMedia(destinationPath);
            if (destResolvedMediaResult.IsFaulted)
            {
                return new Result(destResolvedMediaResult.Error);
            }

            OnDebugMessage($"Destination Media Path: '{destResolvedMediaResult.Value.MediaPath}'");
            OnDebugMessage($"Destination Virtual Path: '{destResolvedMediaResult.Value.FileSystemPath}'");            

            OnDebugMessage($"Opening destination '{destinationPath}' as readable");

            var destinationMediaResult = await commandHelper.GetReadableMedia(physicalDrivesList, 
                destResolvedMediaResult.Value.MediaPath, destResolvedMediaResult.Value.Modifiers);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            // get dest media and stream
            using var destMedia = destinationMediaResult.Value;
            var destStream = MediaHelper.GetStreamFromMedia(destMedia);

            OnDebugMessage($"Destination media is '{destMedia.GetType().Name}', path '{destMedia.Path}', media type '{destMedia.Type}' and size {destMedia.Size} bytes");
            OnInformationMessage($"Destination size '{destMedia.Size.FormatBytes()}' ({destMedia.Size} bytes)");

            // get dest start offset and source size
            var destStartOffsetAndSizeResult = await MediaHelper.GetStartOffsetAndSize(commandHelper, destMedia, 
                destResolvedMediaResult.Value.FileSystemPath);
            if (destStartOffsetAndSizeResult.IsFaulted)
            {
                return new Result(destStartOffsetAndSizeResult.Error);
            }
            
            var (destPartStartOffset, destPartSize) = destStartOffsetAndSizeResult.Value;

            OnInformationMessage($"Destination part start offset '{destPartStartOffset}' and size '{destPartSize.FormatBytes()}' ({destPartSize} bytes)");

            if (destinationStartOffset > 0)
            {
                OnDebugMessage($"Destination start offset within part '{destinationStartOffset}'");
                destPartStartOffset += destinationStartOffset;
            }
            
            if (srcPartSize > destPartSize)
            {
                return new Result(new CompareSizeTooLargeError(srcPartSize, destPartSize,
                    $"Source part size '{srcPartSize.FormatBytes()}' ({srcPartSize} bytes) is larger than destination part size '{destPartSize.FormatBytes()}' ({destPartSize} bytes)"));
            }

            var compareSize = GetCompareSize(srcPartSize, destPartSize);

            OnInformationMessage($"Compare size '{compareSize.FormatBytes()}' ({compareSize} bytes)");
            
            // return error, if compare size is zero
            if (compareSize <= 0)
            {
                return new Result(new Error($"Invalid compare size '{compareSize}' for source or destination"));
            }

            if (compareSize > destPartSize)
            {
                return new Result(new InvalidCompareSizeError(compareSize, destPartSize,
                    $"Compare size {compareSize} is larger than size {destPartSize}"));
            }

            var imageVerifier = new ImageVerifier(retries: retries, force: force);
            imageVerifier.DataProcessed += (_, e) =>
            {
                statusBytesProcessed = e.BytesProcessed;
                statusTimeElapsed = e.TimeElapsed;
                OnDataProcessed(e.Indeterminate, e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal, e.BytesPerSecond);
            };
            imageVerifier.SrcError += (_, args) => OnSrcError(args);
            imageVerifier.DestError += (_, args) => OnDestError(args);

            var result = await imageVerifier.Verify(token, srcStream, srcPartStartOffset,
                destStream, destPartStartOffset, compareSize, skipZeroFilled);
            if (result.IsFaulted)
            {
                return new Result(result.Error);
            }

            if (statusBytesProcessed != compareSize)
            {
                return new Result(new Error(
                    $"Compared '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) is not equal to size '{compareSize.FormatBytes()}' ({compareSize} bytes)"));
            }

            OnInformationMessage(
                $"Compared '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) in {statusTimeElapsed.FormatElapsed()}, source and destination are identical");

            return new Result();
        }

        private long GetCompareSize(long sourceSize, long destinationSize)
        {
            // return largest comparable size, if size is zero
            if (size.Value == 0)
            {
                return Math.Min(sourceSize, destinationSize);
            }
            
            return size.Value != 0 ? sourceSize.ResolveSize(size) : sourceSize;
        }

        private void OnSrcError(IoErrorEventArgs args) => SrcError?.Invoke(this, args);

        private void OnDestError(IoErrorEventArgs args) => DestError?.Invoke(this, args);
    }
}