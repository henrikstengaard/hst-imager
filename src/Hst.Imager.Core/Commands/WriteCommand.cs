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
        private long statusBytesProcessed;
        private TimeSpan statusTimeElapsed;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;
        
        public WriteCommand(ILogger<WriteCommand> logger, ICommandHelper commandHelper, IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath,
            string destinationPath, Size size)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.sourcePath = sourcePath;
            this.destinationPath = destinationPath;
            this.size = size;
            this.statusBytesProcessed = 0;
            this.statusTimeElapsed = TimeSpan.Zero;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Writing source path '{sourcePath}' to destination path '{destinationPath}'");
            
            OnDebugMessage($"Opening '{sourcePath}' as readable");
            
            var physicalDrivesList = physicalDrives.ToList();
            var sourceMediaResult = commandHelper.GetReadableMedia(physicalDrivesList, sourcePath, false);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }
            using var sourceMedia = sourceMediaResult.Value;
            await using var sourceStream = sourceMedia.Stream;

            var sourceSize = sourceMedia.Size;
            OnDebugMessage($"Source size '{sourceSize.FormatBytes()}' ({sourceSize} bytes)");
            
            var writeSize = Convert
                .ToInt64(size.Value == 0 ? sourceSize : size.Value)
                .ResolveSize(size);
            OnInformationMessage($"Size '{writeSize.FormatBytes()}' ({writeSize} bytes)");
            
            OnDebugMessage($"Opening '{destinationPath}' as writable");
            
            var destinationMediaResult = commandHelper.GetWritableMedia(physicalDrivesList, destinationPath, writeSize);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }
            using var destinationMedia = destinationMediaResult.Value;
            await using var destinationStream = destinationMedia.Stream;

            var streamCopier = new StreamCopier();
            streamCopier.DataProcessed += (_, e) =>
            {
                statusBytesProcessed = e.BytesProcessed;
                statusTimeElapsed = e.TimeElapsed;
                OnDataProcessed(e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal, e.BytesPerSecond);
            };
            var result = await streamCopier.Copy(token, sourceStream, destinationStream, writeSize);

            OnInformationMessage($"Written '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) in {statusTimeElapsed.FormatElapsed()}");
            
            return result;
        }

        private void OnDataProcessed(double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond)
        {
            DataProcessed?.Invoke(this,
                new DataProcessedEventArgs(percentComplete, bytesProcessed, bytesRemaining, bytesTotal, timeElapsed,
                    timeRemaining, timeTotal, bytesPerSecond));
        }
    }
}