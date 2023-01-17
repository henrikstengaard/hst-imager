namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Core;
    using Microsoft.Extensions.Logging;
    using Size = Models.Size;

    public class ConvertCommand : CommandBase
    {
        private readonly ILogger<ConvertCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly string sourcePath;
        private readonly string destinationPath;
        private readonly Size size;
        private readonly int retries;
        private long statusBytesProcessed;
        private TimeSpan statusTimeElapsed;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;
        public event EventHandler<IoErrorEventArgs> SrcError;
        public event EventHandler<IoErrorEventArgs> DestError;

        public ConvertCommand(ILogger<ConvertCommand> logger, ICommandHelper commandHelper,
            string sourcePath,
            string destinationPath, Size size, int retries)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.sourcePath = sourcePath;
            this.destinationPath = destinationPath;
            this.size = size;
            this.retries = retries;
            this.statusBytesProcessed = 0;
            this.statusTimeElapsed = TimeSpan.Zero;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Converting image file from '{sourcePath}' to '{destinationPath}'");
            
            OnDebugMessage($"Opening '{sourcePath}' as readable");

            var sourceMediaResult =
                commandHelper.GetReadableMedia(Enumerable.Empty<IPhysicalDrive>(), sourcePath, false);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            using var sourceMedia = sourceMediaResult.Value;
            await using var sourceStream = sourceMedia.Stream;

            var sourceSize = sourceMedia.Size;
            OnDebugMessage($"Source size '{sourceSize.FormatBytes()}' ({sourceSize} bytes)");
            
            var convertSize = Convert
                .ToInt64(size.Value == 0 ? sourceSize : size.Value)
                .ResolveSize(size);
            OnInformationMessage($"Size '{convertSize.FormatBytes()}' ({convertSize} bytes)");

            OnDebugMessage($"Opening '{destinationPath}' as writable");

            var destinationMediaResult =
                commandHelper.GetWritableMedia(Enumerable.Empty<IPhysicalDrive>(), destinationPath, convertSize, false, true);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            using var destinationMedia = destinationMediaResult.Value;
            await using var destinationStream = destinationMedia.Stream;

            var isVhd = commandHelper.IsVhd(destinationPath);
            if (!isVhd)
            {
                destinationStream.SetLength(convertSize);
            }

            var streamCopier = new StreamCopier(retries: retries);
            streamCopier.DataProcessed += (_, e) =>
            {
                statusBytesProcessed = e.BytesProcessed;
                statusTimeElapsed = e.TimeElapsed;
                OnDataProcessed(e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal, e.BytesPerSecond);
            };
            streamCopier.SrcError += (_, args) => OnSrcError(args);
            streamCopier.DestError += (_, args) => OnDestError(args);
            
            var result = await streamCopier.Copy(token, sourceStream, destinationStream, convertSize, 0, 0, isVhd);

            OnInformationMessage($"Converted '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) in {statusTimeElapsed.FormatElapsed()}");

            return result;
        }

        private void OnDataProcessed(double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond)
        {
            DataProcessed?.Invoke(this,
                new DataProcessedEventArgs(percentComplete, bytesProcessed, bytesRemaining, bytesTotal, timeElapsed,
                    timeRemaining, timeTotal, bytesPerSecond));
        }

        private void OnSrcError(IoErrorEventArgs args) => SrcError?.Invoke(this, args);

        private void OnDestError(IoErrorEventArgs args) => DestError?.Invoke(this, args);
    }
}