namespace HstWbInstaller.Imager.Core.Commands
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Core;
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
            if (size.Unit != Unit.Bytes)
            {
                return new Result(new Error("Size must be in bytes"));
            }
            
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            var mediaSize = Convert.ToInt64(compatibleSize ? size.Value * 0.95 : size.Value);

            OnProgressMessage($"Creating blank image '{path}'");
            OnProgressMessage($"Size '{mediaSize.FormatBytes()}' ({mediaSize} bytes)");
            OnProgressMessage($"Compatible '{compatibleSize}'");
            
            OnProgressMessage($"Opening '{path}' for read/write");
            
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