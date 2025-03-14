using DiscUtils.Streams;

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
        private readonly int retries;
        private readonly bool verify;
        private readonly bool force;
        private readonly long? start;
        private long statusBytesProcessed;
        private TimeSpan statusTimeElapsed;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;
        public event EventHandler<IoErrorEventArgs> SrcError;
        public event EventHandler<IoErrorEventArgs> DestError;
        
        public ReadCommand(ILogger<ReadCommand> logger, ICommandHelper commandHelper, IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath,
            string destinationPath, Size size, int retries, bool verify, bool force, long? start)
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
            this.start = start;
            this.statusBytesProcessed = 0;
            this.statusTimeElapsed = TimeSpan.Zero;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Reading from '{sourcePath}' to '{destinationPath}'");
            
            OnDebugMessage($"Opening '{sourcePath}' as readable");
            
            // resolve media path
            var mediaResult = commandHelper.ResolveMedia(sourcePath);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            OnDebugMessage($"Media Path: '{mediaResult.Value.MediaPath}'");
            OnDebugMessage($"Virtual Path: '{mediaResult.Value.FileSystemPath}'");            

            var physicalDrivesList = physicalDrives.ToList();
            
            var srcMediaResult = await commandHelper.GetReadableMedia(physicalDrivesList, mediaResult.Value.MediaPath, mediaResult.Value.Modifiers);
            if (srcMediaResult.IsFaulted)
            {
                return new Result(srcMediaResult.Error);
            }
            
            using var srcMedia = srcMediaResult.Value;
            
            var srcDisk = srcMedia is DiskMedia diskMedia
                ? diskMedia.Disk
                : new DiscUtils.Raw.Disk(srcMedia.Stream, Ownership.None);
            var srcStream = srcDisk.Content;

            // read disk info
            var diskInfo = await commandHelper.ReadDiskInfo(srcMedia);

            // get start offset and source size
            var startOffsetAndSizeResult = GetStartOffsetAndSize(mediaResult.Value.FileSystemPath, diskInfo);
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
            
            srcStream.Position = startOffset;
            
            var readSize = sourceSize.ResolveSize(size);
            OnInformationMessage($"Size '{readSize.FormatBytes()}' ({readSize} bytes)");
            
            OnDebugMessage($"Opening '{destinationPath}' as writable");
            
            var destMediaResult = await commandHelper.GetWritableFileMedia(destinationPath, size: readSize, create: true);
            if (destMediaResult.IsFaulted)
            {
                return new Result(destMediaResult.Error);
            }
            using var destMedia = destMediaResult.Value;
            var destStream = destMedia.Stream;

            var isVhd = commandHelper.IsVhd(destinationPath);
            var isZip = commandHelper.IsZip(destinationPath);
            var isGZip = commandHelper.IsGZip(destinationPath);
            if (!isVhd && !isZip && !isGZip)
            {
                destStream.SetLength(readSize);
            }
            
            var streamCopier = new StreamCopier(verify: verify, retries: retries, force: force);
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

        private void OnDataProcessed(bool indeterminate, double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond) =>
            DataProcessed?.Invoke(this,
                new DataProcessedEventArgs(indeterminate, percentComplete, bytesProcessed, bytesRemaining, bytesTotal, timeElapsed,
                    timeRemaining, timeTotal, bytesPerSecond));

        private void OnSrcError(IoErrorEventArgs args) => SrcError?.Invoke(this, args);

        private void OnDestError(IoErrorEventArgs args) => DestError?.Invoke(this, args);

        private static Result<Tuple<long, long>> GetStartOffsetAndSize(string path, DiskInfo diskInfo)
        {
            var pathComponents = string.IsNullOrEmpty(path)
                ? []
                : path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).ToArray();

            if (pathComponents.Length == 0)
            {
                return new Result<Tuple<long, long>>(new Tuple<long, long>(0, diskInfo.Size));
            }
            
            switch (pathComponents[0])
            {
                case "gpt":
                    if (diskInfo.GptPartitionTablePart == null)
                    {
                        return new Result<Tuple<long, long>>(new Error("Guid Partition Table not found"));
                    }

                    return pathComponents.Length == 1
                        ? new Result<Tuple<long, long>>(new Tuple<long, long>(0, diskInfo.GptPartitionTablePart.Size))
                        : GetPartitionStartOffsetAndSize(path, pathComponents.Skip(1).ToArray(), diskInfo.GptPartitionTablePart);
                case "mbr":
                    if (diskInfo.MbrPartitionTablePart == null)
                    {
                        return new Result<Tuple<long, long>>(new Error("Master Boot Record not found"));
                    }

                    return pathComponents.Length == 1
                        ? new Result<Tuple<long, long>>(new Tuple<long, long>(0, diskInfo.MbrPartitionTablePart.Size))
                        : GetPartitionStartOffsetAndSize(path, pathComponents.Skip(1).ToArray(), diskInfo.MbrPartitionTablePart);
                case "rdb":
                    if (diskInfo.RdbPartitionTablePart == null)
                    {
                        return new Result<Tuple<long, long>>(new Error("Rigid Disk Block not found"));
                    }

                    return pathComponents.Length == 1
                        ? new Result<Tuple<long, long>>(new Tuple<long, long>(0, diskInfo.RdbPartitionTablePart.Size))
                        : GetPartitionStartOffsetAndSize(path, pathComponents.Skip(1).ToArray(), diskInfo.RdbPartitionTablePart);
                default:
                    return new Result<Tuple<long, long>>(new Error($"Unsupported path '{path}'"));
            }
        }

        private static Result<Tuple<long, long>> GetPartitionStartOffsetAndSize(string path, string[] pathComponents,
            PartitionTablePart partitionTablePart)
        {
            if (pathComponents.Length == 0)
            {
                return new Result<Tuple<long, long>>(new Error($"Partition number not found in path '{path}'"));
            }
            
            if (pathComponents.Length > 1 ||
                !int.TryParse(pathComponents[0], out var partitionNumber))
            {
                return new Result<Tuple<long, long>>(new Error($"Invalid partition number in path '{path}'"));
            }

            var partition = partitionTablePart.Parts.FirstOrDefault(x => x.PartitionNumber == partitionNumber);
            
            return partition == null 
                ? new Result<Tuple<long, long>>(new Error($"Partition number {partitionNumber} not found"))
                : new Result<Tuple<long, long>>(new Tuple<long, long>(partition.StartOffset, partition.Size));
        }
    }
}