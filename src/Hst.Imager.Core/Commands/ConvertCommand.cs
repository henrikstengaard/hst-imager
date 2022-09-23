namespace Hst.Imager.Core.Commands
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Amiga.RigidDiskBlocks;
    using Hst.Core;
    using Hst.Core.Extensions;
    using Microsoft.Extensions.Logging;
    using Size = Models.Size;

    public class ConvertCommand : CommandBase
    {
        private readonly ILogger<ConvertCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly string sourcePath;
        private readonly string destinationPath;
        private readonly Size size;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;

        public ConvertCommand(ILogger<ConvertCommand> logger, ICommandHelper commandHelper,
            string sourcePath,
            string destinationPath, Size size)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.sourcePath = sourcePath;
            this.destinationPath = destinationPath;
            this.size = size;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnDebugMessage($"Converting source path '{sourcePath}' to destination path '{destinationPath}'");
            
            var sourceMediaResult =
                commandHelper.GetReadableMedia(Enumerable.Empty<IPhysicalDrive>(), sourcePath, false);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            using var sourceMedia = sourceMediaResult.Value;
            await using var sourceStream = sourceMedia.Stream;

            RigidDiskBlock rigidDiskBlock = null;
            try
            {
                var firstBytes = await sourceStream.ReadBytes(512 * 2048);
                rigidDiskBlock = await commandHelper.GetRigidDiskBlock(new MemoryStream(firstBytes));
            }
            catch (Exception)
            {
                // ignored
            }

            var convertSize = Convert
                .ToInt64(size.Value == 0 ? rigidDiskBlock?.DiskSize ?? sourceStream.Length : size.Value)
                .ResolveSize(size);
            
            OnDebugMessage($"Size '{convertSize.FormatBytes()}' ({convertSize} bytes)");

            var destinationMediaResult =
                commandHelper.GetWritableMedia(Enumerable.Empty<IPhysicalDrive>(), destinationPath, convertSize, false);
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

            var streamCopier = new StreamCopier();
            streamCopier.DataProcessed += (_, e) =>
            {
                OnDataProcessed(e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal);
            };
            return await streamCopier.Copy(token, sourceStream, destinationStream, convertSize, 0, 0, isVhd);
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