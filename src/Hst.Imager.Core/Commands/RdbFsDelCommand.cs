namespace Hst.Imager.Core.Commands
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amiga.RigidDiskBlocks;
    using Hst.Core;
    using Microsoft.Extensions.Logging;

    public class RdbFsDelCommand : CommandBase
    {
        private readonly ILogger<RdbFsDelCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly int fileSystemNumber;

        public RdbFsDelCommand(ILogger<RdbFsDelCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, int fileSystemNumber)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.fileSystemNumber = fileSystemNumber;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnDebugMessage($"Opening '{path}' for read/write");

            var mediaResult = commandHelper.GetWritableMedia(physicalDrives, path, allowPhysicalDrive: true);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            using var media = mediaResult.Value;
            await using var stream = media.Stream;

            OnDebugMessage("Reading Rigid Disk Block");
            
            var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);

            if (rigidDiskBlock == null)
            {
                return new Result(new Error("Rigid Disk Block not found"));
            }

            var fileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks.ToList();

            OnDebugMessage($"Deleting file system number '{fileSystemNumber}'");
            
            if (fileSystemNumber < 1 || fileSystemNumber > fileSystemHeaderBlocks.Count)
            {
                return new Result(new Error($"Invalid file system number '{fileSystemNumber}'"));
            }

            var fileSystemHeaderBlock = fileSystemHeaderBlocks[fileSystemNumber - 1];
            
            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
            var partitionBlock =
                partitionBlocks.FirstOrDefault(x => x.DosType.SequenceEqual(fileSystemHeaderBlock.DosType)); 
            if (partitionBlock != null)
            {
                return new Result(new Error($"Partition number '{partitionBlocks.IndexOf(partitionBlock) + 1}' uses file system number '{fileSystemNumber}'"));
            }

            fileSystemHeaderBlocks.RemoveAt(fileSystemNumber - 1);
            rigidDiskBlock.FileSystemHeaderBlocks = fileSystemHeaderBlocks;
            
            OnDebugMessage("Writing Rigid Disk Block");
            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
            
            return new Result();
        }
    }
}