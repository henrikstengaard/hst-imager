namespace Hst.Imager.Core.Commands
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Core;
    using Core;
    using Extensions;
    using Microsoft.Extensions.Logging;
    using Models;

    public class OptimizeCommand : CommandBase
    {
        private readonly ILogger<OptimizeCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly string path;
        private readonly Size size;
        private readonly PartitionTable partitionTable;

        public OptimizeCommand(ILogger<OptimizeCommand> logger, ICommandHelper commandHelper, string path, Size size,
            PartitionTable partitionTable)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.path = path;
            this.size = size;
            this.partitionTable = partitionTable;
        }
        
        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Optimizing image file at '{path}'");

            if (commandHelper.IsVhd(path))
            {
                return new Result(new UnsupportedImageError(path));
            }

            OnDebugMessage($"Opening '{path}' as writable");
            
            var mediaResult = commandHelper.GetWritableFileMedia(path);
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

            var optimizedSizeResult = GetOptimizeSize(diskInfo);
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

        private Result<long> GetOptimizeSize(DiskInfo diskInfo)
        {
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
                    return new Result<long>(0);
            }
        }
    }
}