namespace Hst.Imager.Core.Commands
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Core;
    using Hst.Imager.Core.Helpers;
    using Microsoft.Extensions.Logging;

    public class RdbPartDelCommand : CommandBase
    {
        private readonly ILogger<RdbInitCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly int partitionNumber;

        public RdbPartDelCommand(ILogger<RdbInitCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.partitionNumber = partitionNumber;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Deleting partition from Rigid Disk Block at '{path}'");
            
            OnDebugMessage($"Opening '{path}' for read/write");

            var writableMediaResult = await commandHelper.GetWritableMedia(physicalDrives, path);
            if (writableMediaResult.IsFaulted)
            {
                return new Result(writableMediaResult.Error);
            }

            using var media = MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, writableMediaResult.Value, path);
            var stream = media.Stream;

            OnDebugMessage("Reading Rigid Disk Block");
            
            var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(media);

            if (rigidDiskBlock == null)
            {
                return new Result(new Error("Rigid Disk Block not found"));
            }

            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();

            OnDebugMessage($"Deleting partition number '{partitionNumber}'");
            
            if (partitionNumber < 1 || partitionNumber > partitionBlocks.Count)
            {
                return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
            }
            
            partitionBlocks.RemoveAt(partitionNumber - 1);
            rigidDiskBlock.PartitionBlocks = partitionBlocks;
            
            OnDebugMessage("Writing Rigid Disk Block");
            await MediaHelper.WriteRigidDiskBlockToMedia(media, rigidDiskBlock);

            return new Result();
        }
    }
}