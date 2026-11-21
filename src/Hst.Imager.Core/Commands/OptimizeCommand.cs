namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Core;
    using Extensions;
    using MagicBytes;
    using Models;
    using Optimization;
    using SparseFiles;

    /// <summary>
    /// Optimize an image file.
    /// Without compact, a raw image file is truncated to optimized size in place.
    /// With compact, used sectors are copied to a new sparse image file (raw sparse file or dynamic vhd),
    /// which replaces the image file. Vhd image files are always compacted.
    /// </summary>
    public class OptimizeCommand(
        ICommandHelper commandHelper,
        string path,
        Size size,
        PartitionTable? partitionTable,
        bool compact = false,
        bool examineFileSystems = false)
        : CommandBase
    {
        /// <summary>
        /// Size of image file before optimizing. Size on disk, when compacting.
        /// </summary>
        public long OriginalFileSize { get; private set; }

        /// <summary>
        /// Size of image file after optimizing. Size on disk, when compacting.
        /// </summary>
        public long OptimizedFileSize { get; private set; }

        /// <summary>
        /// Image file is compacted. True, if compact or examine file systems is enabled or image file is vhd.
        /// </summary>
        public bool IsCompacted { get; private set; }

        /// <summary>
        /// Percent file size is reduced by optimizing. Null, if file size is not reduced,
        /// e.g. a dynamic vhd can be larger than the original due to vhd structures.
        /// </summary>
        public double? PercentReduced => GetPercentReduced(OriginalFileSize, OptimizedFileSize);

        public static double? GetPercentReduced(long originalSize, long optimizedSize) =>
            originalSize <= 0 || optimizedSize >= originalSize
                ? null
                : (double)(originalSize - optimizedSize) / originalSize * 100;

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Optimizing image file at '{path}'");

            var mediaResult = commandHelper.ResolveMedia(path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            var isVhd = await commandHelper.DetectDataType(mediaResult.Value.MediaPath) == DataType.Vhd;

            IsCompacted = compact || examineFileSystems || isVhd;
            return IsCompacted
                ? await Compact(mediaResult.Value.MediaPath, token)
                : await Truncate();
        }

        private async Task<Result> Truncate()
        {
            OnDebugMessage($"Opening '{path}' as writable");

            var mediaResult = await commandHelper.GetWritableFileMedia(path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            using var media = mediaResult.Value;

            if (media.Type != Media.MediaType.Raw)
            {
                return new Result(new UnsupportedImageError(path));
            }

            OnDebugMessage($"Media size '{media.Size}'");

            var diskInfo = await commandHelper.ReadDiskInfo(media);
            if (diskInfo == null)
            {
                return new Result(new Error("Failed to read disk info"));
            }

            OnInformationMessage($"Size '{diskInfo.Size}'");

            var optimizedSizeResult = GetOptimizedSize(diskInfo);
            if (optimizedSizeResult.IsFaulted)
            {
                return new Result(optimizedSizeResult.Error);
            }

            var optimizedSize = optimizedSizeResult.Value;

            // return error, if optimized size is zero
            if (optimizedSize <= 0)
            {
                return new Result(new Error($"Invalid optimized size '{optimizedSize}'"));
            }

            OriginalFileSize = mediaResult.Value.Stream.Length;

            mediaResult.Value.Stream.SetLength(optimizedSize);

            OptimizedFileSize = optimizedSize;

            OnInformationMessage($"Optimized size '{optimizedSize}'");
            OnInformationMessage($"Original file size {FormatFileSize(OriginalFileSize)}");
            OnInformationMessage($"Optimized file size {FormatFileSize(OptimizedFileSize)}");
            OnInformationMessage(FormatPercentReduced("Optimized"));

            return new Result();
        }

        private async Task<Result> Compact(string mediaPath, CancellationToken token)
        {
            OriginalFileSize = SparseFile.GetSizeOnDisk(mediaPath);

            OnDebugMessage($"Opening '{path}' as readable");

            var sourceMediaResult = await commandHelper.GetReadableFileMedia(path);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            var sourceMedia = sourceMediaResult.Value;
            var sourceMediaPath = sourceMedia.Path;
            var tempPath = GetTempPath(mediaPath);
            var replaceImage = false;

            try
            {
                if (sourceMedia.Type != Media.MediaType.Raw && sourceMedia.Type != Media.MediaType.Vhd)
                {
                    return new Result(new UnsupportedImageError(path));
                }

                var sourceSize = sourceMedia.Size;
                OnDebugMessage($"Media size '{sourceSize}'");

                var diskInfo = await commandHelper.ReadDiskInfo(sourceMedia);
                if (diskInfo == null)
                {
                    return new Result(new Error("Failed to read disk info"));
                }

                OnInformationMessage($"Size '{diskInfo.Size}'");

                // use source size, if size is not defined and no partition table is found to optimize size to
                var optimizedSizeResult = GetOptimizedSize(diskInfo);
                var optimizedSize = optimizedSizeResult.IsSuccess
                    ? optimizedSizeResult.Value
                    : size.Value == 0 && (partitionTable == null || partitionTable == PartitionTable.None)
                        ? sourceSize
                        : 0;
                if (optimizedSizeResult.IsFaulted && optimizedSize == 0)
                {
                    return new Result(optimizedSizeResult.Error);
                }

                if (optimizedSize <= 0)
                {
                    return new Result(new Error($"Invalid optimized size '{optimizedSize}'"));
                }

                var copySize = Math.Min(optimizedSize, sourceSize);

                var allocationMap = new AllocationMap();
                if (examineFileSystems)
                {
                    OnInformationMessage("Examining file systems for used sectors");
                    var analyzer = new DiskAllocationAnalyzer();
                    analyzer.DebugMessage += (_, message) => OnDebugMessage(message);
                    var diskAllocation = await analyzer.Analyze(sourceMedia.Stream, sourceSize);
                    allocationMap = diskAllocation.AllocationMap;

                    foreach (var volume in diskAllocation.Volumes)
                    {
                        OnInformationMessage(volume.IsKnownFileSystem
                            ? $"{volume.Name}: File system '{volume.FileSystemAllocation.FileSystem}', size {volume.Size.FormatBytes()}, free {volume.FileSystemAllocation.FreeBytes.FormatBytes()}"
                            : $"{volume.Name}: {volume.Message}, size {volume.Size.FormatBytes()}, copying used sectors");
                    }
                }

                OnInformationMessage($"Compacting image file to '{tempPath}'");

                var destinationMediaResult = await commandHelper.CreateSparseFileMedia(tempPath, optimizedSize);
                if (destinationMediaResult.IsFaulted)
                {
                    return new Result(destinationMediaResult.Error);
                }

                Result<ImageCompactorStatistics> compactResult;
                var destinationMedia = destinationMediaResult.Value;
                try
                {
                    var imageCompactor = new ImageCompactor();
                    imageCompactor.DataProcessed += (_, args) => OnDataProcessed(args.Indeterminate,
                        args.PercentComplete, args.BytesProcessed, args.BytesRemaining, args.BytesTotal,
                        args.TimeElapsed, args.TimeRemaining, args.TimeTotal, args.BytesPerSecond);

                    compactResult = await imageCompactor.Compact(sourceMedia.Stream, destinationMedia.Stream,
                        copySize, allocationMap, sourceMedia.Byteswap, token);
                }
                finally
                {
                    var destinationMediaPath = destinationMedia.Path;
                    destinationMedia.Dispose();
                    commandHelper.ClearActiveMedia(destinationMediaPath);
                }

                if (compactResult.IsFaulted)
                {
                    return new Result(compactResult.Error);
                }

                var statistics = compactResult.Value;
                OnInformationMessage($"Used data written {statistics.BytesWritten.FormatBytes()}");
                OnInformationMessage($"Free sectors skipped {statistics.BytesFree.FormatBytes()}");
                OnInformationMessage($"Zero filled sectors skipped {statistics.BytesZeroFilled.FormatBytes()}");
                OnInformationMessage($"Optimized size '{optimizedSize}'");

                replaceImage = true;
            }
            finally
            {
                sourceMedia.Dispose();
                commandHelper.ClearActiveMedia(sourceMediaPath);

                if (!replaceImage && File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }

            OnDebugMessage($"Replacing '{mediaPath}' with '{tempPath}'");
            File.Move(tempPath, mediaPath, true);

            OptimizedFileSize = SparseFile.GetSizeOnDisk(mediaPath);

            OnInformationMessage($"Original file size {FormatFileSize(OriginalFileSize)}");
            OnInformationMessage($"Compacted file size {FormatFileSize(OptimizedFileSize)}");
            OnInformationMessage(FormatPercentReduced("Compacted"));

            return new Result();
        }

        private static string FormatFileSize(long size) => $"{size.FormatBytes()} ({size} bytes)";

        /// <summary>
        /// Format percent file size is reduced by. If file size is not reduced, it's stated instead
        /// of a negative percent.
        /// </summary>
        private string FormatPercentReduced(string action) =>
            PercentReduced is { } percent
                ? $"{action} {percent.ToString("0.0", CultureInfo.InvariantCulture)}%"
                : "File size not reduced";

        /// <summary>
        /// Get temporary path for compacted image file in same directory as image file
        /// to avoid copying between file systems when replacing image file.
        /// Extension is kept, so vhd image files are created as vhd.
        /// </summary>
        private static string GetTempPath(string mediaPath)
        {
            var directory = Path.GetDirectoryName(mediaPath) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(mediaPath);
            var extension = Path.GetExtension(mediaPath);
            return Path.Combine(directory, $"{fileName}.{Guid.NewGuid().ToString("N")[..8]}.optimizing{extension}");
        }

        private Result<long> GetOptimizedSize(DiskInfo diskInfo) =>
            partitionTable.HasValue && partitionTable.Value != PartitionTable.None
                ? GetDefinedOptimizeSize(diskInfo)
                : GetAutoOptimizeSize(diskInfo);

        private Result<long> GetDefinedOptimizeSize(DiskInfo diskInfo)
        {
            // return resolves size if it is not zero
            if (size.Value != 0)
            {
                return new Result<long>(diskInfo.Size.ResolveSize(size));
            }

            switch (partitionTable)
            {
                case PartitionTable.Gpt:
                    var guidPartitionTable = diskInfo.PartitionTables
                        .FirstOrDefault(x => x.Type == PartitionTableType.GuidPartitionTable);

                    return guidPartitionTable == null
                        ? new Result<long>(new Error("Guid Partition Table not found"))
                        : new Result<long>(guidPartitionTable.Size);

                case PartitionTable.Mbr:
                    var mbrPartitionTable = diskInfo.PartitionTables
                        .FirstOrDefault(x => x.Type == PartitionTableType.MasterBootRecord);

                    return mbrPartitionTable == null
                        ? new Result<long>(new Error("Master Boot Record not found"))
                        : new Result<long>(mbrPartitionTable.Size);

                case PartitionTable.Rdb:
                    var rdbPartitionTable = diskInfo.PartitionTables
                        .FirstOrDefault(x => x.Type == PartitionTableType.RigidDiskBlock);

                    return rdbPartitionTable == null
                        ? new Result<long>(new Error("Rigid Disk Block not found"))
                        : new Result<long>(rdbPartitionTable.Size);
                default:
                    return new Result<long>(new Error(
                        "Unable to optimize size of image file when no partition table are found"));
            }
        }

        private Result<long> GetAutoOptimizeSize(DiskInfo diskInfo)
        {
            // return resolves size if it is not zero
            if (size.Value != 0)
            {
                return new Result<long>(diskInfo.Size.ResolveSize(size));
            }

            // get partition tables
            var guidPartitionTable = diskInfo.PartitionTables
                .FirstOrDefault(x => x.Type == PartitionTableType.GuidPartitionTable);
            var mbrPartitionTable = diskInfo.PartitionTables
                .FirstOrDefault(x => x.Type == PartitionTableType.MasterBootRecord);
            var rdbPartitionTable = diskInfo.PartitionTables
                .FirstOrDefault(x => x.Type == PartitionTableType.RigidDiskBlock);

            // return guid partition table size, if guid partition table is present
            if (guidPartitionTable != null)
            {
                return new Result<long>(guidPartitionTable.Size);
            }

            // return master boot record size, if master boot record is present
            // and rigid disk block is not present
            if (mbrPartitionTable != null && rdbPartitionTable == null)
            {
                return new Result<long>(mbrPartitionTable.Size);
            }

            // return rigid disk block size, if rigid disk block is present
            if (rdbPartitionTable != null)
            {
                var rdbSize = rdbPartitionTable.Size;

                // if master boot record is present and is larger than rigid disk block
                // then use master boot record size
                if (mbrPartitionTable != null && mbrPartitionTable.Size > rdbSize)
                {
                    rdbSize = mbrPartitionTable.Size;
                }

                return new Result<long>(rdbSize);
            }

            return new Result<long>(new Error(
                "Unable to optimize size of image file when no partition table are found"));
        }
    }
}
