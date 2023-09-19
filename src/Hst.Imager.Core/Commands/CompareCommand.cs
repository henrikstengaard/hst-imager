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
        private readonly string destinationPath;
        private readonly Size size;
        private readonly int retries;
        private readonly bool force;
        private long statusBytesProcessed;
        private TimeSpan statusTimeElapsed;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;
        public event EventHandler<IoErrorEventArgs> SrcError;
        public event EventHandler<IoErrorEventArgs> DestError;

        public CompareCommand(ILogger<CompareCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath,
            string destinationPath, Size size, int retries, bool force)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.sourcePath = sourcePath;
            this.destinationPath = destinationPath;
            this.size = size;
            this.retries = retries;
            this.force = force;
            this.statusBytesProcessed = 0;
            this.statusTimeElapsed = TimeSpan.Zero;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Compare source '{sourcePath}' and destination '{destinationPath}'");

            OnDebugMessage($"Opening source '{sourcePath}' as readable");

            var physicalDrivesList = physicalDrives.ToList();
            var sourceMediaResult = await commandHelper.GetReadableMedia(physicalDrivesList, sourcePath);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            using var sourceMedia = sourceMediaResult.Value;
            var sourceStream = sourceMedia.Stream;

            var sourceSize = sourceMedia.Size;
            OnDebugMessage($"Source size '{sourceSize.FormatBytes()}' ({sourceSize} bytes)");

            OnDebugMessage($"Opening destination '{destinationPath}' as readable");

            var destinationMediaResult = await commandHelper.GetReadableMedia(physicalDrivesList, destinationPath);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            using var destinationMedia = destinationMediaResult.Value;
            var destinationStream = destinationMedia.Stream;

            var destinationSize = destinationMedia.Size;
            OnDebugMessage($"Destination size '{destinationSize.FormatBytes()}' ({destinationSize} bytes)");

            var compareSize = GetCompareSize(sourceSize, destinationSize);

            // return error, if compare size is zero
            if (compareSize <= 0)
            {
                return new Result(new Error($"Invalid compare size '{compareSize}' for source or destination"));
            }

            if (compareSize > destinationSize)
            {
                return new Result(new InvalidCompareSizeError(compareSize, destinationSize,
                    $"Compare size {compareSize} is larger than size {destinationSize}"));
            }

            OnInformationMessage($"Compare size '{compareSize.FormatBytes()}' ({compareSize} bytes)");

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

            var result = await imageVerifier.Verify(token, sourceStream, destinationStream, compareSize);
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

        private void OnDataProcessed(bool indeterminate, double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond)
        {
            DataProcessed?.Invoke(this,
                new DataProcessedEventArgs(indeterminate, percentComplete, bytesProcessed, bytesRemaining, bytesTotal, timeElapsed,
                    timeRemaining, timeTotal, bytesPerSecond));
        }

        private void OnSrcError(IoErrorEventArgs args) => SrcError?.Invoke(this, args);

        private void OnDestError(IoErrorEventArgs args) => DestError?.Invoke(this, args);
    }
}