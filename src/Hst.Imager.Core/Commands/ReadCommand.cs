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

    public class ReadCommand : CommandBase
    {
        private readonly ILogger<ReadCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string sourcePath;
        private readonly string destinationPath;
        private readonly Size size;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;
        
        public ReadCommand(ILogger<ReadCommand> logger, ICommandHelper commandHelper, IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath,
            string destinationPath, Size size)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.sourcePath = sourcePath;
            this.destinationPath = destinationPath;
            this.size = size;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnProgressMessage($"Reading source path '{sourcePath}' to destination path '{destinationPath}'");
            
            var physicalDrivesList = physicalDrives.ToList();
            var sourceMediaResult = commandHelper.GetReadableMedia(physicalDrivesList, sourcePath);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }
            using var sourceMedia = sourceMediaResult.Value;
            await using var sourceStream = sourceMedia.Stream;

            var sourceSize = sourceMedia.Size;
            var readSize = sourceSize.ResolveSize(size);
            //var readSize = size is > 0 ? size.Value : rigidDiskBlock?.DiskSize ?? sourceSize;

            logger.LogDebug($"Source size '{sourceSize.FormatBytes()}' ({sourceSize} bytes)");
            //logger.LogDebug($"Size '{size}'");
            //logger.LogDebug($"Rigid disk block size '{(rigidDiskBlock == null ? "N/A" : rigidDiskBlock.DiskSize)}'");
            logger.LogDebug($"Read size '{readSize.FormatBytes()}' ({readSize} bytes)");
            
            var destinationMediaResult = commandHelper.GetWritableMedia(physicalDrivesList, destinationPath, readSize, false);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }
            using var destinationMedia = destinationMediaResult.Value;
            await using var destinationStream = destinationMedia.Stream;

            var isVhd = commandHelper.IsVhd(destinationPath);
            if (!isVhd)
            {
                destinationStream.SetLength(readSize);
            }
            
            var imageConverter = new ImageConverter();
            imageConverter.DataProcessed += (_, e) =>
            {
                OnDataProcessed(e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal);
            };
            await imageConverter.Convert(token, sourceStream, destinationStream, readSize, isVhd);
            
            return new Result();
        }

        private void OnDataProcessed(double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal)
        {
            DataProcessed?.Invoke(this,
                new DataProcessedEventArgs(percentComplete, bytesProcessed, bytesRemaining, bytesTotal, timeElapsed,
                    timeRemaining, timeTotal));
        }
    }
}