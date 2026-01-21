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
            
            var srcMediaResult = await commandHelper.GetReadableMedia(physicalDrivesList, 
                srcResolvedMediaResult.Value.MediaPath, srcResolvedMediaResult.Value.Modifiers);
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

            // add src start offset, if defined
            if (sourceStartOffset > 0)
            {
                srcStartOffset += sourceStartOffset;
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

            var destMediaResult = await commandHelper.GetReadableMedia(physicalDrivesList, 
                destResolvedMediaResult.Value.MediaPath, destResolvedMediaResult.Value.Modifiers);
            if (destMediaResult.IsFaulted)
            {
                return new Result(destMediaResult.Error);
            }

            // get pistorm rdb media from dest media
            var destPiStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(
                destMediaResult.Value, destResolvedMediaResult.Value.FileSystemPath,
                destResolvedMediaResult.Value.DirectorySeparatorChar);

            using var destMedia = destPiStormRdbMediaResult.Media;
            var destStream = destMedia.Stream;
            var destFileSystemPath = destPiStormRdbMediaResult.FileSystemPath;

            // get start offset and source size from dest media
            var destStartOffsetAndSizeResult = await MediaHelper.GetStartOffsetAndSize(commandHelper, destMedia,
                destFileSystemPath);
            if (destStartOffsetAndSizeResult.IsFaulted)
            {
                return new Result(destStartOffsetAndSizeResult.Error);
            }
            
            var (destStartOffset, destSize) = destStartOffsetAndSizeResult.Value;

            OnInformationMessage($"Destination start offset '{destStartOffset}'");
            OnInformationMessage($"Destination size '{destSize.FormatBytes()}' ({destSize} bytes)");

            // add destination start offset, if defined
            if (destinationStartOffset > 0)
            {
                destStartOffset += destinationStartOffset;
            }
            
            if (size.Value != 0)
            {
                srcSize = srcSize.ResolveSize(size);
            }

            if (srcSize > destSize)
            {
                return new Result(new CompareSizeTooLargeError(srcSize, destSize,
                    $"Source part size '{srcSize.FormatBytes()}' ({srcSize} bytes) is larger than destination part size '{destSize.FormatBytes()}' ({destSize} bytes)"));
            }
            
            var compareSize = Math.Min(srcSize, destSize);

            OnInformationMessage($"Compare size '{compareSize.FormatBytes()}' ({compareSize} bytes)");
            
            // return error, if compare size is zero
            if (compareSize <= 0)
            {
                return new Result(new Error($"Invalid compare size '{compareSize}' for source or destination"));
            }

            if (compareSize > destSize)
            {
                return new Result(new InvalidCompareSizeError(compareSize, destSize,
                    $"Compare size {compareSize} is larger than size {destSize}"));
            }

            using var imageVerifier = new ImageVerifier(retries: retries, force: force);
            imageVerifier.DataProcessed += (_, e) =>
            {
                statusBytesProcessed = e.BytesProcessed;
                statusTimeElapsed = e.TimeElapsed;
                OnDataProcessed(e.Indeterminate, e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal, e.BytesPerSecond);
            };
            imageVerifier.SrcError += (_, args) => OnSrcError(args);
            imageVerifier.DestError += (_, args) => OnDestError(args);

            var result = await imageVerifier.Verify(token, srcStream, srcStartOffset,
                destStream, destStartOffset, compareSize, skipZeroFilled);
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

        private void OnSrcError(IoErrorEventArgs args) => SrcError?.Invoke(this, args);

        private void OnDestError(IoErrorEventArgs args) => DestError?.Invoke(this, args);
    }
}