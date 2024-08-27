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

    public class WriteCommand : CommandBase
    {
        private readonly ILogger<WriteCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string sourcePath;
        private readonly string destinationPath;
        private readonly Size size;
        private readonly int retries;
        private readonly bool verify;
        private readonly bool force;
        private long statusBytesProcessed;
        private TimeSpan statusTimeElapsed;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;
        public event EventHandler<IoErrorEventArgs> SrcError;
        public event EventHandler<IoErrorEventArgs> DestError;

        public WriteCommand(ILogger<WriteCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath,
            string destinationPath, Size size, int retries, bool verify, bool force)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.sourcePath = sourcePath;
            this.destinationPath = destinationPath;
            this.size = size;
            this.retries = retries;
            this.verify = verify;
            this.force = force;
            this.statusBytesProcessed = 0;
            this.statusTimeElapsed = TimeSpan.Zero;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Writing source path '{sourcePath}' to destination path '{destinationPath}'");

            OnDebugMessage($"Opening '{sourcePath}' as readable");

            var physicalDrivesList = physicalDrives.ToList();
            var sourceMediaResult = await commandHelper.GetReadableFileMedia(sourcePath);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            using var sourceMedia = sourceMediaResult.Value;
            var sourceStream = sourceMedia.Stream;

            var sourceSize = sourceMedia.Size;
            OnDebugMessage($"Source size '{sourceSize.FormatBytes()}' ({sourceSize} bytes)");

            var writeSize = Convert
                .ToInt64(size.Value == 0 ? sourceSize : size.Value)
                .ResolveSize(size);
            OnInformationMessage($"Size '{writeSize.FormatBytes()}' ({writeSize} bytes)");

            OnDebugMessage($"Opening '{destinationPath}' as writable");

            var destinationMediaResult =
                await commandHelper.GetPhysicalDriveMedia(physicalDrivesList, destinationPath, writeable: true);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            using var destinationMedia = destinationMediaResult.Value;

            if (writeSize > destinationMedia.Size)
            {
                return new Result(
                    new WriteSizeTooLargeError(destinationMedia.Size, writeSize,
                        $"Write size {writeSize.FormatBytes()} ({writeSize} bytes) is too large for media size {destinationMedia.Size.FormatBytes()} ({destinationMedia.Size} bytes)"));
            }

            var destinationStream = destinationMedia.Stream;

            var streamCopier = new StreamCopier(verify: verify, retries: retries, force: force);
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

            var result = await streamCopier.Copy(token, sourceStream, destinationStream, writeSize);
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

            return new Result();
        }

        private void OnDataProcessed(bool indeterminate, double percentComplete, long bytesProcessed,
            long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond)
        {
            DataProcessed?.Invoke(this,
                new DataProcessedEventArgs(indeterminate, percentComplete, bytesProcessed, bytesRemaining, bytesTotal,
                    timeElapsed,
                    timeRemaining, timeTotal, bytesPerSecond));
        }

        private void OnSrcError(IoErrorEventArgs args) => SrcError?.Invoke(this, args);

        private void OnDestError(IoErrorEventArgs args) => DestError?.Invoke(this, args);
    }
}