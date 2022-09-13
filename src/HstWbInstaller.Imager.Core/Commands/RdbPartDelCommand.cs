namespace HstWbInstaller.Imager.Core.Commands
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Amiga.RigidDiskBlocks;
    using HstWbInstaller.Core;
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
            OnProgressMessage($"Opening '{path}' for read/write");

            var mediaResult = commandHelper.GetWritableMedia(physicalDrives, path, allowPhysicalDrive: true);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            using var media = mediaResult.Value;
            await using var stream = media.Stream;

            OnProgressMessage($"Reading Rigid Disk Block");
            
            var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

            if (rigidDiskBlock == null)
            {
                return new Result(new Error("Rigid Disk Block not found"));
            }

            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();

            OnProgressMessage($"Deleting partition number '{partitionNumber}'");
            
            if (partitionNumber < 1 || partitionNumber > partitionBlocks.Count)
            {
                return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
            }
            
            partitionBlocks.RemoveAt(partitionNumber - 1);
            rigidDiskBlock.PartitionBlocks = partitionBlocks;
            
            OnProgressMessage($"Writing Rigid Disk Block");
            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
            
            return new Result();
        }
    }
}