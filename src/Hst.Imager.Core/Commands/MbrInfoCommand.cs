namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Core;
    using Microsoft.Extensions.Logging;

    public class MbrInfoCommand : CommandBase
    {
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;

        public MbrInfoCommand(ILogger<MbrInfoCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path)
        {
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
        }

        public event EventHandler<InfoReadEventArgs> MbrInfoRead;
        
        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Reading Master Boot Record information from '{path}'");
            
            OnDebugMessage($"Opening '{path}' as readable");

            var physicalDrivesList = physicalDrives.ToList();
            var mediaResult = await commandHelper.GetReadableMedia(physicalDrivesList, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }
            using var media = mediaResult.Value;
            
            OnDebugMessage($"Reading Master Boot Record from path '{path}'");

            var diskInfo = await commandHelper.ReadDiskInfo(media);
            
            OnMbrInfoRead(new MediaInfo
            {
                Path = path,
                Name = media.Model,
                IsPhysicalDrive = media.IsPhysicalDrive,
                Type = media.Type,
                DiskSize = diskInfo.Size,
                DiskInfo = diskInfo
            });

            return new Result();
        }

        private void OnMbrInfoRead(MediaInfo mediaInfo)
        {
            MbrInfoRead?.Invoke(this, new InfoReadEventArgs(mediaInfo));
        }        
    }
}