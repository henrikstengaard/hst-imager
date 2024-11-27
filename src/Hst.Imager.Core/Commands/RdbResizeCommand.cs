namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DiscUtils.Streams;
    using Hst.Core;
    using Hst.Core.Extensions;
    using Hst.Imager.Core.Extensions;
    using Hst.Imager.Core.Helpers;
    using Hst.Imager.Core.Models;
    using Microsoft.Extensions.Logging;
    using Size = Models.Size;

    public class RdbResizeCommand : CommandBase
    {
        private readonly ILogger<RdbResizeCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly Size size;

        public RdbResizeCommand(ILogger<RdbResizeCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, Size size)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.size = size;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Resizing Rigid Disk Block at '{path}'");

            OnDebugMessage($"Opening '{path}' as writable");

            var writableMediaResult = await commandHelper.GetWritableMedia(physicalDrives, path);
            if (writableMediaResult.IsFaulted)
            {
                return new Result(writableMediaResult.Error);
            }

            using var media = MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, writableMediaResult.Value, path);

            OnDebugMessage("Reading Rigid Disk Block");

            var diskInfo = await commandHelper.ReadDiskInfo(media);
            var rigidDiskBlock = diskInfo.RigidDiskBlock;

            if (rigidDiskBlock == null)
            {
                return new Result(new Error("Rigid Disk Block not found"));
            }

            var lastPartitionBlock = rigidDiskBlock.PartitionBlocks
                .OrderByDescending(x => x.HighCyl)
                .FirstOrDefault();

            var cylinderSize = diskInfo.RigidDiskBlock.Sectors * diskInfo.RigidDiskBlock.Heads *
                               diskInfo.RigidDiskBlock.BlockSize;

            var highestUsedCylinder = lastPartitionBlock == null
                ? rigidDiskBlock.LoCylinder
                : lastPartitionBlock.HighCyl;

            var minimumRigidDiskBlockSize = (long)highestUsedCylinder * cylinderSize;

            var diskSize = media.Size;
            var newRigidDiskBlockSize = diskSize.ResolveSize(size).ToSectorSize();

            var hiCylinder = Convert.ToUInt32(Math.Floor((double)newRigidDiskBlockSize / cylinderSize));

            OnInformationMessage($"Disk size '{diskSize.FormatBytes()}' ({diskSize} bytes)");

            OnDebugMessage($"Old Rigid Disk Block size '{rigidDiskBlock.DiskSize.FormatBytes()}' ({rigidDiskBlock.DiskSize} bytes)");
            OnDebugMessage($"Highest used cylinder '{highestUsedCylinder}'");
            OnDebugMessage($"New Rigid Disk Block size '{newRigidDiskBlockSize.FormatBytes()}' ({newRigidDiskBlockSize} bytes)");

            if (newRigidDiskBlockSize < minimumRigidDiskBlockSize)
            {
                OnDebugMessage($"Adjusted to smallest Rigid Disk Block size '{minimumRigidDiskBlockSize.FormatBytes()}' ({minimumRigidDiskBlockSize} bytes)");
                hiCylinder = highestUsedCylinder;
            }

            var largestCylinder = Convert.ToUInt32(Math.Floor((double)diskSize / cylinderSize));
            var largestRigidDiskBlockSize = largestCylinder * cylinderSize;

            if (newRigidDiskBlockSize > largestRigidDiskBlockSize)
            {
                OnDebugMessage($"Adjusted to largest Rigid Disk Block size '{minimumRigidDiskBlockSize.FormatBytes()}' ({minimumRigidDiskBlockSize} bytes)");
                hiCylinder = largestCylinder - 1;
            }

            rigidDiskBlock.Cylinders = (uint)(hiCylinder + 1);
            rigidDiskBlock.HiCylinder = (uint)hiCylinder;
            rigidDiskBlock.ParkingZone = (uint)hiCylinder;

            OnDebugMessage("Writing Rigid Disk Block");
            await MediaHelper.WriteRigidDiskBlockToMedia(media, rigidDiskBlock);

            OnDebugMessage($"Resized Rigid Disk Block to size '{newRigidDiskBlockSize.FormatBytes()}' ({newRigidDiskBlockSize} bytes)");

            return new Result();
        }
    }
}