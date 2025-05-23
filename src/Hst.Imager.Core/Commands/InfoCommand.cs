﻿using Hst.Imager.Core.Helpers;

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

        public InfoCommand(ILogger<InfoCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
        }

        public event EventHandler<InfoReadEventArgs> DiskInfoRead;

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Reading disk information from '{path}'");

            OnDebugMessage($"Opening '{path}' as readable");

            var sourceMediaResult = await commandHelper.GetReadableMedia(physicalDrives, path);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            using var media = MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, sourceMediaResult.Value, path);

            OnDebugMessage($"Media size '{media.Size}'");

            var diskInfo = await commandHelper.ReadDiskInfo(media);

            OnDebugMessage($"Path '{path}', disk size '{diskInfo.Size}'");

            OnDiskInfoRead(new MediaInfo
            {
                Path = media.Path,
                Name = media.Model,
                IsPhysicalDrive = media.IsPhysicalDrive,
                Type = media.Type,
                DiskSize = diskInfo.Size,
                DiskInfo = diskInfo,
                Byteswap = media.Byteswap
            });

            return new Result();
        }

        private void OnDiskInfoRead(MediaInfo mediaInfo)
        {
            DiskInfoRead?.Invoke(this, new InfoReadEventArgs(mediaInfo));
        }
    }
}