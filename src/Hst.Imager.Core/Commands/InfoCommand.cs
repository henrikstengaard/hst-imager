namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Core;
    using Microsoft.Extensions.Logging;

    public class InfoCommand : CommandBase
    {
        private readonly ILogger<InfoCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;

        public InfoCommand(ILogger<InfoCommand> logger, ICommandHelper commandHelper, IEnumerable<IPhysicalDrive> physicalDrives, string path)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
        }

        public event EventHandler<InfoReadEventArgs> DiskInfoRead;
        
        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Reading information from '{path}'");
            
            OnDebugMessage($"Opening '{path}' as readable");
            
            var sourceMediaResult = commandHelper.GetReadableMedia(physicalDrives, path);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }
            using var media = sourceMediaResult.Value;
            await using var stream = media.Stream;

            OnDebugMessage($"Physical drive size '{media.Size}'");
                
            var diskInfo = await commandHelper.ReadDiskInfo(media, stream);
            
            var streamSize = stream.Length;
            var diskSize = streamSize is > 0 ? streamSize : media.Size;
            
            OnDebugMessage($"Path '{path}', disk size '{diskSize}'");
            
            OnDiskInfoRead(new MediaInfo
            {
                Path = path,
                Model = media.Model,
                IsPhysicalDrive = media.IsPhysicalDrive,
                Type = media.Type,
                DiskSize = diskSize,
                DiskInfo = diskInfo
            });

            return new Result();
        }

        private void OnDiskInfoRead(MediaInfo mediaInfo)
        {
            DiskInfoRead?.Invoke(this, new InfoReadEventArgs(mediaInfo, mediaInfo.DiskInfo));
        }
    }
}