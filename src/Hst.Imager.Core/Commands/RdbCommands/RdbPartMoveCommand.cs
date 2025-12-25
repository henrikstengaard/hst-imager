using Hst.Core;
using Hst.Imager.Core.Helpers;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System;
using Hst.Imager.Core.Extensions;
using System.Drawing.Drawing2D;
using DiscUtils.OpticalDiscSharing;
using DiscUtils.Streams;
using Hst.Imager.Core.Models;
using Hst.Amiga.RigidDiskBlocks;

namespace Hst.Imager.Core.Commands.RdbCommands
{
    public class RdbPartMoveCommand : CommandBase
    {
        private readonly ILogger<RdbPartMoveCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly int partitionNumber;
        private readonly uint startCylinder;
        private long statusBytesProcessed;
        private TimeSpan statusTimeElapsed;

        public event EventHandler<IoErrorEventArgs> SrcError;
        public event EventHandler<IoErrorEventArgs> DestError;

        public RdbPartMoveCommand(ILogger<RdbPartMoveCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber, uint startCylinder)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.partitionNumber = partitionNumber;
            this.startCylinder = (uint)startCylinder;
            this.statusBytesProcessed = 0;
            this.statusTimeElapsed = TimeSpan.Zero;
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
            var stream = MediaHelper.GetStreamFromMedia(media);

            //var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);

            OnDebugMessage("Reading Rigid Disk Block");

            //var diskInfo = await commandHelper.ReadDiskInfo(media);
            var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(media);

            if (rigidDiskBlock == null)
            {
                return new Result(new Error("Rigid Disk Block not found"));
            }

            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();

            OnDebugMessage($"Moving partition number '{partitionNumber}'");

            if (partitionNumber < 1 || partitionNumber > partitionBlocks.Count)
            {
                return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
            }

            var partitionBlock = partitionBlocks[partitionNumber - 1];

            OnInformationMessage($"Rigid Disk Block:");
            OnInformationMessage($"- Start cylinder '{rigidDiskBlock.LoCylinder}'");
            OnInformationMessage($"- End cylinder '{rigidDiskBlock.HiCylinder}'");

            OnInformationMessage($"Move partition from:");
            OnInformationMessage($"- Start cylinder '{partitionBlock.LowCyl}'");
            OnInformationMessage($"- End cylinder '{partitionBlock.HighCyl}'");

            uint endCylinder = startCylinder + partitionBlock.HighCyl - partitionBlock.LowCyl;

            OnInformationMessage($"Move partition to:");
            OnInformationMessage($"- Start cylinder '{startCylinder}'");
            OnInformationMessage($"- End cylinder '{endCylinder}'");

            if (partitionBlock.LowCyl == startCylinder)
            {
                OnInformationMessage($"From and to partition cylinders are the same.");

                return new Result();
            }

            if (startCylinder < rigidDiskBlock.LoCylinder)
            {
                return new Result(new Error(
                    $"Start cylinder '{startCylinder}' is lower than Rigid Disk Block start cylinder '{rigidDiskBlock.LoCylinder}'"));
            }

            if (endCylinder > rigidDiskBlock.HiCylinder)
            {
                return new Result(new Error(
                    $"End cylinder '{endCylinder}' is higher than Rigid Disk Block end cylinder '{rigidDiskBlock.HiCylinder}'"));
            }

            if (!CanMovePartition(rigidDiskBlock, partitionBlock, startCylinder, endCylinder))
            {
                return new Result(new Error(
                    $"Rigid Disk Block does not have unallocated disk space from start cylinder '{startCylinder}' to end cylinder '{endCylinder}'"));
            }

            var cylinderSize = rigidDiskBlock.Sectors * rigidDiskBlock.Heads * rigidDiskBlock.BlockSize;

            var srcOffset = partitionBlock.LowCyl * cylinderSize;
            var destOffset = startCylinder * cylinderSize;
            var copySize = partitionBlock.PartitionSize;

            var streamCopier = new StreamCopier();
            streamCopier.DataProcessed += (_, e) =>
            {
                statusBytesProcessed = e.BytesProcessed;
                statusTimeElapsed = e.TimeElapsed;
                OnDataProcessed(e.Indeterminate, e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal,
                    e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal, e.BytesPerSecond);
            };
            streamCopier.SrcError += (_, args) => OnSrcError(args);
            streamCopier.DestError += (_, args) => OnDestError(args);

            var result = await streamCopier.Copy(token, stream, stream, copySize, sourceOffset: srcOffset, destinationOffset: destOffset);
            if (result.IsFaulted)
            {
                return new Result(result.Error);
            }

            if (copySize != 0 && statusBytesProcessed != copySize)
            {
                return new Result(new Error(
                    $"Written '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) is not equal to size '{copySize.FormatBytes()}' ({copySize} bytes)"));
            }

            OnInformationMessage(
                $"Moved '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) in {statusTimeElapsed.FormatElapsed()}");

            OnDebugMessage("Reading Rigid Disk Block");

            partitionBlock.LowCyl = (uint)startCylinder;
            partitionBlock.HighCyl = (uint)endCylinder;

            await MediaHelper.WriteRigidDiskBlockToMedia(media, rigidDiskBlock);

            await stream.FlushAsync(token);
            
            return new Result();
        }

        private static bool CanMovePartition(RigidDiskBlock rigidDiskBlock, PartitionBlock partitionBlock, uint startCylinder, uint endCylinder)
        {
            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();

            if (!partitionBlocks.Any())
            {
                return true;
            }

            var overlappingPartitionBlocks = partitionBlocks.Where(x =>
                IsOverlapping(x.LowCyl, x.HighCyl, startCylinder, endCylinder)).ToList();

            // can move partition, if no partition exist between start and end cylinder or
            // if partition number to move overlaps with start and end cylinder
            return overlappingPartitionBlocks.Count == 0 ||
                (overlappingPartitionBlocks.Count == 1 && overlappingPartitionBlocks[0] == partitionBlock);
        }

        private static bool IsOverlapping(uint startCylinder1, uint endCylinder1, uint startCylinder2, uint endCylinder2)
        {
            return startCylinder1 <= endCylinder2 && startCylinder2 <= endCylinder1;
        }

        private void OnSrcError(IoErrorEventArgs args) => SrcError?.Invoke(this, args);

        private void OnDestError(IoErrorEventArgs args) => DestError?.Invoke(this, args);
    }
}
