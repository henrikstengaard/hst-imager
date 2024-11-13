namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Core;
    using Hst.Imager.Core.Helpers;
    using Microsoft.Extensions.Logging;

    public class RdbInfoCommand : CommandBase
    {
        private readonly ILogger<RdbInfoCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;

        public RdbInfoCommand(ILogger<RdbInfoCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
        }

        public event EventHandler<InfoReadEventArgs> RdbInfoRead;
        
        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Reading Rigid Disk Block information from '{path}'");

            OnDebugMessage($"Opening '{path}' as readable");

            var readableMediaResult = await commandHelper.GetReadableMedia(physicalDrives, path);
            if (readableMediaResult.IsFaulted)
            {
                return new Result(readableMediaResult.Error);
            }

            using var media = await MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, readableMediaResult.Value, path);

            OnDebugMessage($"Reading Rigid Disk Block from path '{path}'");
            
            var diskInfo = await commandHelper.ReadDiskInfo(media);

            OnRdbInfoRead(new MediaInfo
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
        
        protected virtual void OnRdbInfoRead(MediaInfo mediaInfo)
        {
            RdbInfoRead?.Invoke(this, new InfoReadEventArgs(mediaInfo));
        }        
    }
}