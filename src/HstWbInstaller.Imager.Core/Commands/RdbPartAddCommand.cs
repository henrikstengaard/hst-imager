namespace HstWbInstaller.Imager.Core.Commands
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Amiga;
    using Hst.Amiga.RigidDiskBlocks;
    using Hst.Core;
    using Size = Models.Size;
    using Microsoft.Extensions.Logging;
    using PartitionBlock = Hst.Amiga.RigidDiskBlocks.PartitionBlock;
    using RigidDiskBlockWriter = Hst.Amiga.RigidDiskBlocks.RigidDiskBlockWriter;

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
        private readonly int blockSize;

        public RdbPartAddCommand(ILogger<RdbPartAddCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, string name, string dosType, Size size,
            int reserved, int preAlloc, int buffers, int maxTransfer, bool noMount, bool bootable, int priority, int blockSize)
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
            this.blockSize = blockSize;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            if (dosType.Length != 4)
            {
                return new Result(new Error("DOS type must be 4 characters"));
            }

            if (blockSize % 512 != 0)
            {
                return new Result(new Error("Block size must be dividable by 512"));
            }

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
            
            // needs to calculate space left and not just use disk size
            
            var partitionSize = rigidDiskBlock.DiskSize.ResolveSize(size);

            OnProgressMessage($"Adding partition number '{partitionBlocks.Count + 1}'");
            OnProgressMessage($"Device name '{name}'");

            var partitionBlock =
                PartitionBlock.Create(rigidDiskBlock, dosTypeBytes, name, partitionSize,
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
            partitionBlock.SizeBlock = (uint)(blockSize / Hst.Amiga.SizeOf.Long);

            OnProgressMessage($"Size '{partitionBlock.PartitionSize.FormatBytes()}' ({partitionBlock.PartitionSize} bytes)");
            OnProgressMessage($"Low cyl '{partitionBlock.LowCyl}'");
            OnProgressMessage($"High cyl '{partitionBlock.HighCyl}'");
            OnProgressMessage($"Reserved '{partitionBlock.Reserved}'");
            OnProgressMessage($"PreAlloc '{partitionBlock.PreAlloc}'");
            OnProgressMessage($"Buffers '{partitionBlock.NumBuffer}'");
            OnProgressMessage($"Max Transfer '{partitionBlock.MaxTransfer}'");
            OnProgressMessage($"SizeBlock '{partitionBlock.SizeBlock * Hst.Amiga.SizeOf.Long}'");
            
            rigidDiskBlock.PartitionBlocks = rigidDiskBlock.PartitionBlocks.Concat(new[] { partitionBlock });
            
            OnProgressMessage("Writing Rigid Disk Block");
            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

            return new Result();
        }
    }
}