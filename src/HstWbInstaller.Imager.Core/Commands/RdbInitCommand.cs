﻿namespace HstWbInstaller.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Core;
    using Microsoft.Extensions.Logging;
    using Models;

    public class RdbInitCommand : CommandBase
    {
        private readonly ILogger<RdbInitCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly string name;
        private readonly Size size;
        private readonly int rdbBlockLo;

        public RdbInitCommand(ILogger<RdbInitCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, string name, Size size, int rdbBlockLo)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.name = name;
            this.size = size;
            this.rdbBlockLo = rdbBlockLo;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnProgressMessage($"Opening '{path}' for read/write");

            var mediaResult = commandHelper.GetWritableMedia(physicalDrives, path, allowPhysicalDrive: true);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            using var media = mediaResult.Value;
            await using var stream = media.Stream;

            OnProgressMessage("Initializing Rigid Disk Block");
            
            var defaultName = media.IsPhysicalDrive ? media.Model : Path.GetFileNameWithoutExtension(media.Model);

            var rdbSize = Convert.ToInt64(size.Value == 0 ? stream.Length : size.Value).ResolveSize(size);
            
            OnProgressMessage($"Size '{rdbSize.FormatBytes()}' ({rdbSize} bytes)");

            var rigidDiskBlock =
                Hst.Amiga.RigidDiskBlocks.RigidDiskBlock.Create(rdbSize);
            rigidDiskBlock.DiskProduct = string.IsNullOrWhiteSpace(name) ? defaultName : name;
            rigidDiskBlock.DiskRevision = "0.1";
            rigidDiskBlock.DiskVendor = "HstImage";

            if (rdbBlockLo is > 0 and < 16)
            {
                rigidDiskBlock.RdbBlockLo = (uint)rdbBlockLo;
            }

            OnProgressMessage($"RdbBlockLo '{rdbBlockLo}'");
            OnProgressMessage($"RdbBlockHi '{rigidDiskBlock.RdbBlockHi}'");
            
            OnProgressMessage("Writing Rigid Disk Block");
            await Hst.Amiga.RigidDiskBlocks.RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
            
            return new Result();
        }
    }
}