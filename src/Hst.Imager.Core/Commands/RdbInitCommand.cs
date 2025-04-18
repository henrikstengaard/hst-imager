﻿namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Amiga.RigidDiskBlocks;
    using Extensions;
    using Hst.Core;
    using Hst.Core.Extensions;
    using Hst.Imager.Core.Helpers;
    using Hst.Imager.Core.Models;
    using Microsoft.Extensions.Logging;
    using Size = Models.Size;

    public class RdbInitCommand : CommandBase
    {
        private readonly ILogger<RdbInitCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly string name;
        private readonly Size size;
        private readonly string chs;
        private readonly int rdbBlockLo;

        public RdbInitCommand(ILogger<RdbInitCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, string name, Size size, string chs, int rdbBlockLo)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.name = name;
            this.size = size;
            this.chs = chs;
            this.rdbBlockLo = rdbBlockLo;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            RdbDiskGeometry rdbDiskGeometry = null;

            if (!string.IsNullOrWhiteSpace(chs))
            {
                var values = chs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                if (values.Length != 3)
                {
                    return new Result(new Error($"Invalid cylinders, heads and sectors value '{chs}'"));
                }

                if (!int.TryParse(values[0], out var cylinders) || !int.TryParse(values[1], out var heads) ||
                    !int.TryParse(values[2], out var sectors))
                {
                    return new Result(new Error($"Invalid cylinders, heads and sectors value '{chs}'"));
                }

                rdbDiskGeometry = new RdbDiskGeometry
                {
                    DiskSize = (long)cylinders * heads * sectors * 512,
                    Cylinders = cylinders,
                    Heads = heads,
                    Sectors = sectors
                };
            }

            OnInformationMessage($"Initializing Rigid Disk Block at '{path}'");

            OnDebugMessage($"Opening '{path}' for read/write");

            var writableMediaResult = await commandHelper.GetWritableMedia(physicalDrives, path);
            if (writableMediaResult.IsFaulted)
            {
                return new Result(writableMediaResult.Error);
            }

            using var media = MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, writableMediaResult.Value, path);

            OnDebugMessage("Initializing Rigid Disk Block");

            var defaultName = media.IsPhysicalDrive ? media.Model : Path.GetFileNameWithoutExtension(media.Model);

            var diskSize = media is DiskMedia diskMedia ? diskMedia.Size : media.Stream.Length;
            var rigidDiskBlockSize = diskSize.ResolveSize(size).ToSectorSize();

            OnDebugMessage($"Disk size '{diskSize.FormatBytes()}' ({diskSize} bytes)");

            var rigidDiskBlock = rdbDiskGeometry != null
                ? CreateFromDiskGeometry(rdbDiskGeometry)
                : RigidDiskBlock.Create(rigidDiskBlockSize);

            if (rigidDiskBlock.DiskSize > diskSize)
            {
                return new Result(new Error($"Invalid Rigid Disk Block size '{rigidDiskBlock.DiskSize}' is larger than disk size '{diskSize}'"));
            }

            OnInformationMessage(
                $"Rigid Disk Block size '{rigidDiskBlock.DiskSize.FormatBytes()}' ({rigidDiskBlock.DiskSize} bytes)");

            rigidDiskBlock.DiskProduct = string.IsNullOrWhiteSpace(name) ? defaultName : name;
            rigidDiskBlock.DiskRevision = "0.1";
            rigidDiskBlock.DiskVendor = "HstImage";

            if (rdbBlockLo is > 0 and < 16)
            {
                rigidDiskBlock.RdbBlockLo = (uint)rdbBlockLo;
            }

            OnDebugMessage($"RdbBlockLo '{rdbBlockLo}'");
            OnDebugMessage($"RdbBlockHi '{rigidDiskBlock.RdbBlockHi}'");

            OnDebugMessage("Writing Rigid Disk Block");
            await MediaHelper.WriteRigidDiskBlockToMedia(media, rigidDiskBlock);

            return new Result();
        }

        private RigidDiskBlock CreateFromDiskGeometry(RdbDiskGeometry rdbDiskGeometry)
        {
            var cylinders = (uint)rdbDiskGeometry.Cylinders;
            return new RigidDiskBlock
            {
                DiskSize = rdbDiskGeometry.DiskSize,
                CylBlocks = (uint)(rdbDiskGeometry.Heads * rdbDiskGeometry.Sectors),
                Cylinders = cylinders,
                Heads = (uint)rdbDiskGeometry.Heads,
                Sectors = (uint)rdbDiskGeometry.Sectors,
                LoCylinder = 2,
                HiCylinder = cylinders - 1,
                ParkingZone = cylinders - 1,
                ReducedWrite = cylinders,
                WritePreComp = cylinders
            };
        }
    }
}