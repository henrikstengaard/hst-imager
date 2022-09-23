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

        public OptimizeCommand(ILogger<OptimizeCommand> logger, ICommandHelper commandHelper, string path, Size size)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.path = path;
            this.size = size;
        }
        
        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Optimizing image file at '{path}'");
            
            OnDebugMessage($"Opening '{path}' as writable");
            
            if (commandHelper.IsVhd(path))
            {
                return new Result(new UnsupportedImageError(path));
            }

            var mediaResult = commandHelper.GetWritableMedia(Enumerable.Empty<IPhysicalDrive>(), path, allowPhysicalDrive: false);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }
            using var media = mediaResult.Value;
            await using var stream = media.Stream;
            var currentSize = stream.Length;

            OnDebugMessage($"Size '{currentSize}'");

            if (size.Value == 0)
            {
                return new Result();
            }

            // optimize
            var optimizedSize = currentSize.ResolveSize(size);
            stream.SetLength(optimizedSize);

            OnInformationMessage($"Optimized size '{optimizedSize}'");
            
            return new Result();
        }
    }
}