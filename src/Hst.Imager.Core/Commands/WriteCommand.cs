﻿namespace Hst.Imager.Core.Commands
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
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnDebugMessage($"Writing source path '{sourcePath}' to destination path '{destinationPath}'");
            
            var physicalDrivesList = physicalDrives.ToList();
            var sourceMediaResult = commandHelper.GetReadableMedia(physicalDrivesList, sourcePath, false);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }
            using var sourceMedia = sourceMediaResult.Value;
            await using var sourceStream = sourceMedia.Stream;

            // RigidDiskBlock rigidDiskBlock = null;
            // try
            // {
            //     var firstBytes = await sourceStream.ReadBytes(512 * 2048);
            //     rigidDiskBlock = await commandHelper.GetRigidDiskBlock(new MemoryStream(firstBytes));
            // }
            // catch (Exception)
            // {
            //     // ignored
            // }

            var sourceSize = sourceMedia.Size;
            //var writeSize = size is > 0 ? size.Value : sourceSize;
            var writeSize = sourceSize.ResolveSize(size);

            logger.LogDebug($"Source size '{sourceSize.FormatBytes()}' ({sourceSize} bytes)");
            // logger.LogDebug($"Size '{(size is > 0 ? size.Value : "N/A")}'");
            // logger.LogDebug($"Source size '{sourceSize}'");
            //logger.LogDebug($"Rigid disk block size '{(rigidDiskBlock == null ? "N/A" : rigidDiskBlock.DiskSize)}'");
            logger.LogDebug($"Write size '{writeSize.FormatBytes()}' ({writeSize} bytes)");
            
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
                OnDataProcessed(e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal);
            };
            await streamCopier.Copy(token, sourceStream, destinationStream, writeSize);
            
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