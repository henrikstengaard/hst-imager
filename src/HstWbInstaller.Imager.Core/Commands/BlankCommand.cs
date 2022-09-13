namespace HstWbInstaller.Imager.Core.Commands
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using HstWbInstaller.Core;
    using Microsoft.Extensions.Logging;
    using Models;
    using File = System.IO.File;

    public class BlankCommand : CommandBase
    {
        private readonly ILogger<BlankCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly string path;
        private readonly Size size;
        private readonly bool compatibleSize;

        public BlankCommand(ILogger<BlankCommand> logger, ICommandHelper commandHelper, string path, Size size, bool compatibleSize)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.path = path;
            this.size = size;
            this.compatibleSize = compatibleSize;
        }
        
        public override async Task<Result> Execute(CancellationToken token)
        {
            logger.LogDebug($"Path '{path}', size '{size}'");

            if (size.Unit != Unit.Bytes)
            {
                return new Result(new Error("Size unit must be in bytes"));
            }
            
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            var mediaSize = compatibleSize ? Convert.ToInt64(size.Value * 0.95) : size.Value;
            
            var mediaResult = commandHelper.GetWritableMedia(Enumerable.Empty<IPhysicalDrive>(), path, mediaSize, false);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            using var media = mediaResult.Value;
            await using var stream = media.Stream;

            if (!commandHelper.IsVhd(path))
            {
                stream.SetLength(mediaSize);
            }

            return new Result();
        }
    }
}