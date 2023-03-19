namespace Hst.Imager.Core.Commands
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Core;
    using Core;
    using Extensions;
    using Hst.Core.Extensions;
    using Microsoft.Extensions.Logging;
    using Models;

    public class OptimizeCommand : CommandBase
    {
        private readonly ILogger<OptimizeCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly string path;
        private readonly Size size;
        private readonly bool rdb;

        public OptimizeCommand(ILogger<OptimizeCommand> logger, ICommandHelper commandHelper, string path, Size size,
            bool rdb)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.path = path;
            this.size = size;
            this.rdb = rdb;
        }
        
        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Optimizing image file at '{path}'");

            if (commandHelper.IsVhd(path))
            {
                return new Result(new UnsupportedImageError(path));
            }

            OnDebugMessage($"Opening '{path}' as writable");
            
            var mediaResult = commandHelper.GetWritableMedia(Enumerable.Empty<IPhysicalDrive>(), path, allowPhysicalDrive: false);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }
            using var media = mediaResult.Value;
            var stream = media.Stream;
            var currentSize = stream.Length;

            OnInformationMessage($"Size '{currentSize}'");

            long optimizedSize = 0;            
            if (rdb)
            {
                var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);

                if (rigidDiskBlock == null)
                {
                    return new Result(new Error("Rigid Disk Block not found"));
                }
                
                optimizedSize = rigidDiskBlock.DiskSize;
            }
            else if (size.Value != 0)
            {
                optimizedSize = currentSize.ResolveSize(size).ToSectorSize();
            }

            // return error, if optimized size is zero
            if (optimizedSize == 0)
            {
                return new Result(new Error($"Invalid optimized size '{optimizedSize}'"));
            }

            // optimize
            stream.SetLength(optimizedSize);

            OnInformationMessage($"Optimized size '{optimizedSize}'");
            
            return new Result();
        }
    }
}