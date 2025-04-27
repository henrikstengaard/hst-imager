namespace Hst.Imager.Core.Commands
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Core;
    using Extensions;
    using Microsoft.Extensions.Logging;
    using Models;

    public class OptimizeCommand(
        ILogger<OptimizeCommand> logger,
        ICommandHelper commandHelper,
        string path,
        Size size,
        PartitionTable? partitionTable)
        : CommandBase
    {
        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Optimizing image file at '{path}'");

            if (commandHelper.IsVhd(path))
            {
                return new Result(new UnsupportedImageError(path));
            }

            OnDebugMessage($"Opening '{path}' as writable");
            
            var mediaResult = await commandHelper.GetWritableFileMedia(path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            using var media = mediaResult.Value;

            OnDebugMessage($"Media size '{media.Size}'");
        
            var diskInfo = await commandHelper.ReadDiskInfo(media);
            if (diskInfo == null)
            {
                return new Result(new Error("Failed to read disk info"));
            }

            OnInformationMessage($"Size '{diskInfo.Size}'");

            var optimizedSizeResult = partitionTable.HasValue && partitionTable.Value != PartitionTable.None
                ? GetDefinedOptimizeSize(diskInfo)
                : GetAutoOptimizeSize(diskInfo);
            
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

            mediaResult.Value.Stream.SetLength(optimizedSize);

            OnInformationMessage($"Optimized size '{optimizedSize}'");
            
            return new Result();
        }

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