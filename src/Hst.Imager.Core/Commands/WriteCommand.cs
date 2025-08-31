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

    public class WriteCommand(
        ILogger<WriteCommand> logger,
        ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives,
        string sourcePath,
        string destinationPath,
        Size size,
        int retries,
        bool verify,
        bool force,
        bool skipZeroFilled,
        long? start)
        : CommandBase
    {
        private readonly ILogger<WriteCommand> logger = logger;
        private long statusBytesProcessed = 0;
        private TimeSpan statusTimeElapsed = TimeSpan.Zero;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;
        public event EventHandler<IoErrorEventArgs> SrcError;
        public event EventHandler<IoErrorEventArgs> DestError;

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Writing source path '{sourcePath}' to destination path '{destinationPath}'");

            OnDebugMessage($"Opening '{sourcePath}' as readable");

            var sourceMediaResult = await commandHelper.GetReadableFileMedia(sourcePath);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            // get src media and stream
            using var srcMedia = sourceMediaResult.Value;
            var srcStream = MediaHelper.GetStreamFromMedia(srcMedia);

            var srcSize = srcMedia.Size;
            OnDebugMessage($"Source size '{srcSize.FormatBytes()}' ({srcSize} bytes)");

            var writeSize = Convert
                .ToInt64(size.Value == 0 ? srcSize : size.Value)
                .ResolveSize(size);
            OnInformationMessage($"Size '{writeSize.FormatBytes()}' ({writeSize} bytes)");

            OnDebugMessage($"Opening '{destinationPath}' as writable");

            // resolve media for destination path
            var destResolveMediaResult = commandHelper.ResolveMedia(destinationPath);
            if (destResolveMediaResult.IsFaulted)
            {
                return new Result(destResolveMediaResult.Error);
            }

            OnDebugMessage($"Media Path: '{destResolveMediaResult.Value.MediaPath}'");
            OnDebugMessage($"Virtual Path: '{destResolveMediaResult.Value.FileSystemPath}'");             

            var physicalDrivesList = physicalDrives.ToList();

            var destMediaResult =
                await commandHelper.GetWritableMedia(physicalDrivesList, destResolveMediaResult.Value.MediaPath, destResolveMediaResult.Value.Modifiers);
            if (destMediaResult.IsFaulted)
            {
                return new Result(destMediaResult.Error);
            }

            // get dest media and stream
            using var destMedia = destMediaResult.Value;
            var destStream = MediaHelper.GetStreamFromMedia(destMedia);

            // read disk info
            var diskInfo = await commandHelper.ReadDiskInfo(destMedia);

            // get start offset and source size
            var startOffsetAndSizeResult = GetStartOffsetAndSize(destResolveMediaResult.Value.FileSystemPath, diskInfo);
            if (startOffsetAndSizeResult.IsFaulted)
            {
                return new Result(startOffsetAndSizeResult.Error);
            }
            
            var (destStartOffset, destSize) = startOffsetAndSizeResult.Value;

            OnDebugMessage($"Destination start offset '{destStartOffset}'");
            OnDebugMessage($"Destination size '{destSize.FormatBytes()}' ({destSize} bytes)");

            // add destination start offset, if defined
            if (start.HasValue)
            {
                destStartOffset += start.Value;
            }
            
            if (writeSize > destSize)
            {
                return new Result(new WriteSizeTooLargeError(destSize, writeSize,
                        $"Source size {writeSize.FormatBytes()} ({writeSize} bytes) is too large for destination size {destSize.FormatBytes()} ({destSize} bytes)"));
            }

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

            var result = await streamCopier.Copy(token, srcStream, destStream, writeSize, 0L, 
                destStartOffset, skipZeroFilled);
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

            if (destMedia.IsPhysicalDrive)
            {
                await commandHelper.RescanPhysicalDrives();
            }
            
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
        
                private static Result<Tuple<long, long>> GetStartOffsetAndSize(string path, DiskInfo diskInfo)
        {
            var pathComponents = string.IsNullOrEmpty(path)
                ? []
                : path.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries).ToArray();

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