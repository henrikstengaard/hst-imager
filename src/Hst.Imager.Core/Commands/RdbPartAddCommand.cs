namespace Hst.Imager.Core.Commands
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amiga.Extensions;
    using Extensions;
    using Amiga;
    using Amiga.RigidDiskBlocks;
    using Hst.Core;
    using Microsoft.Extensions.Logging;
    using Size = Models.Size;
    using PartitionBlock = Amiga.RigidDiskBlocks.PartitionBlock;
    using RigidDiskBlockWriter = Amiga.RigidDiskBlocks.RigidDiskBlockWriter;

    public class RdbPartAddCommand : CommandBase
    {
        private readonly ILogger<RdbPartAddCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly string name;
        private readonly Size size;
        private readonly int reserved;
        private readonly int preAlloc;
        private readonly int buffers;
        private readonly int maxTransfer;
        private readonly string dosType;
        private readonly bool noMount;
        private readonly bool bootable;
        private readonly int priority;
        private readonly int fileSystemBlockSize;

        public RdbPartAddCommand(ILogger<RdbPartAddCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, string name, string dosType, Size size,
            int reserved, int preAlloc, int buffers, int maxTransfer, bool noMount, bool bootable, int priority, int fileSystemBlockSize)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.name = name;
            this.size = size;
            this.reserved = reserved;
            this.preAlloc = preAlloc;
            this.buffers = buffers;
            this.maxTransfer = maxTransfer;
            this.dosType = dosType;
            this.noMount = noMount;
            this.bootable = bootable;
            this.priority = priority;
            this.fileSystemBlockSize = fileSystemBlockSize;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            if (dosType.Length != 4)
            {
                return new Result(new Error("DOS type must be 4 characters"));
            }

            if (fileSystemBlockSize % 512 != 0)
            {
                return new Result(new Error("File system block size must be dividable by 512"));
            }

            OnInformationMessage($"Adding partition to Rigid Disk Block at '{path}'");
            
            OnDebugMessage($"Opening '{path}' as writable");

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

            var dosTypeBytes = DosTypeHelper.FormatDosType(dosType.ToUpper());

            var fileSystemHeaderBlock =
                rigidDiskBlock.FileSystemHeaderBlocks.FirstOrDefault(x => x.DosType.SequenceEqual(dosTypeBytes));

            if (fileSystemHeaderBlock == null)
            {
                return new Result(new Error($"File system with DOS type '{dosType}' not found in Rigid Disk Block"));
            }

            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
            var nameBytes = AmigaTextHelper.GetBytes(name.ToUpper());
            if (partitionBlocks.Any(x => AmigaTextHelper.GetBytes(x.DriveName).SequenceEqual(nameBytes)))
            {
                return new Result(new Error($"Partition name '{name}' already exists"));
            }
            
            var partitionSize = rigidDiskBlock.DiskSize.ResolveSize(size);

            OnInformationMessage($"- Partition number '{partitionBlocks.Count + 1}'");
            OnInformationMessage($"- Name '{name}'");
            OnInformationMessage($"- DOS type '{dosTypeBytes.FormatDosType()}'");

            var partitionBlock =
                PartitionBlock.Create(rigidDiskBlock, dosTypeBytes, name, partitionSize, fileSystemBlockSize,
                    bootable);
            
            if (partitionBlock.HighCyl - partitionBlock.LowCyl + 1 <= 0)
            {
                return new Result(new Error($"Invalid size '{size}'"));
            }
            
            var flags = 0U;
            if (bootable)
            {
                flags += (int)PartitionBlock.PartitionFlagsEnum.Bootable;
            }

            if (noMount)
            {
                flags += (int)PartitionBlock.PartitionFlagsEnum.NoMount;
            }

            partitionBlock.Reserved = (uint)reserved;
            partitionBlock.PreAlloc = (uint)preAlloc;
            partitionBlock.MaxTransfer = (uint)maxTransfer;
            partitionBlock.NumBuffer = (uint)buffers;
            partitionBlock.Flags = flags;
            partitionBlock.BootPriority = (uint)priority;
            partitionBlock.SizeBlock = rigidDiskBlock.BlockSize / SizeOf.ULong;

            OnInformationMessage($"- Size '{partitionBlock.PartitionSize.FormatBytes()}' ({partitionBlock.PartitionSize} bytes)");
            OnInformationMessage($"- Low Cyl '{partitionBlock.LowCyl}'");
            OnInformationMessage($"- High Cyl '{partitionBlock.HighCyl}'");
            OnInformationMessage($"- Reserved '{partitionBlock.Reserved}'");
            OnInformationMessage($"- PreAlloc '{partitionBlock.PreAlloc}'");
            OnInformationMessage($"- Buffers '{partitionBlock.NumBuffer}'");
            OnInformationMessage($"- Max Transfer '{partitionBlock.MaxTransfer}'");
            OnInformationMessage($"- File System Block Size '{partitionBlock.Sectors * rigidDiskBlock.BlockSize}'");
            
            rigidDiskBlock.PartitionBlocks = rigidDiskBlock.PartitionBlocks.Concat(new[] { partitionBlock });
            
            OnDebugMessage("Writing Rigid Disk Block");
            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

            return new Result();
        }
    }
}