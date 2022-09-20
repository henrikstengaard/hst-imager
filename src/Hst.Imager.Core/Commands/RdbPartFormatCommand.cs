namespace HstWbInstaller.Imager.Core.Commands
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Amiga.FileSystems.FastFileSystem;
    using Hst.Amiga.FileSystems.Pfs3;
    using Hst.Core;
    using Microsoft.Extensions.Logging;

    public class RdbPartFormatCommand : CommandBase
    {
        private readonly ILogger<RdbInitCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly int partitionNumber;
        private readonly string name;

        public RdbPartFormatCommand(ILogger<RdbInitCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber, string name)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.partitionNumber = partitionNumber;
            this.name = name;
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

            OnProgressMessage("Reading Rigid Disk Block");

            var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);

            if (rigidDiskBlock == null)
            {
                return new Result(new Error("Rigid Disk Block not found"));
            }
            
            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();

            OnProgressMessage($"Formatting partition number '{partitionNumber}' with volume name '{name}'");
            
            if (partitionNumber < 1 || partitionNumber > partitionBlocks.Count)
            {
                return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
            }

            var partitionBlock = partitionBlocks[partitionNumber - 1];

            OnProgressMessage($"DOS type '{partitionBlock.DosTypeFormatted}'");
            
            switch (partitionBlock.DosTypeFormatted)
            {
                case "DOS\\3":
                    await FastFileSystemFormatter.FormatPartition(stream, partitionBlock, name);
                    break;
                case "PDS\\3":
                case "PFS\\3":
                    await Pfs3Formatter.FormatPartition(stream, partitionBlock, name);
                    break;
                default:
                    return new Result(new Error($"Unsupported file system '{partitionBlock.DosTypeFormatted}'"));
            }

            return new Result();
        }
    }
}