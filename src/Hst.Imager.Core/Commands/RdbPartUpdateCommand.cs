namespace Hst.Imager.Core.Commands
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Amiga;
    using Hst.Amiga.RigidDiskBlocks;
    using Hst.Core;
    using Microsoft.Extensions.Logging;

    public class RdbPartUpdateCommand : CommandBase
    {
        private readonly ILogger<RdbPartUpdateCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly int partitionNumber;
        private readonly string name;
        private readonly int? reserved;
        private readonly int? preAlloc;
        private readonly int? buffers;
        private readonly uint? maxTransfer;
        private readonly uint? mask;
        private readonly string dosType;
        private readonly bool? noMount;
        private readonly bool? bootable;
        private readonly int? bootPriority;
        private readonly int? blockSize;

        public RdbPartUpdateCommand(ILogger<RdbPartUpdateCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber, string name, string dosType,
            int? reserved,
            int? preAlloc, int? buffers, uint? maxTransfer, uint? mask, bool? noMount, bool? bootable, int? bootPriority, int? blockSize)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.partitionNumber = partitionNumber;
            this.name = name;
            this.reserved = reserved;
            this.preAlloc = preAlloc;
            this.buffers = buffers;
            this.maxTransfer = maxTransfer;
            this.mask = mask;
            this.dosType = dosType;
            this.noMount = noMount;
            this.bootable = bootable;
            this.bootPriority = bootPriority;
            this.blockSize = blockSize;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            if (!string.IsNullOrWhiteSpace(dosType) && dosType.Length != 4)
            {
                return new Result(new Error("DOS type must be 4 characters"));
            }

            if (blockSize.HasValue && blockSize % 512 != 0)
            {
                return new Result(new Error("Block size must be dividable by 512"));
            }
            
            OnInformationMessage($"Updating partition in Rigid Disk Block at '{path}'");

            OnDebugMessage($"Opening '{path}' as readable");

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

            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();

            OnDebugMessage($"Updating partition number '{partitionNumber}'");

            if (partitionNumber < 1 || partitionNumber > partitionBlocks.Count)
            {
                return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
            }

            var partitionBlock = partitionBlocks[partitionNumber - 1];

            var hasChanges = false;
            
            // update dos type, if defined
            if (!string.IsNullOrEmpty(dosType))
            {
                var dosTypeBytes = DosTypeHelper.FormatDosType(dosType.ToUpper());

                var fileSystemHeaderBlock =
                    rigidDiskBlock.FileSystemHeaderBlocks.FirstOrDefault(x => x.DosType.SequenceEqual(dosTypeBytes));

                if (fileSystemHeaderBlock == null)
                {
                    return new Result(
                        new Error($"File system with DOS type '{dosType}' not found in Rigid Disk Block"));
                }

                partitionBlock.DosType = dosTypeBytes;

                hasChanges = true;
                OnDebugMessage(
                    $"DOS Type '0x{partitionBlock.DosType.FormatHex().ToUpper()}, {partitionBlock.DosTypeFormatted}'");
            }

            // update drive name, if defined
            if (!string.IsNullOrWhiteSpace(name))
            {
                var nameBytes = AmigaTextHelper.GetBytes(name.ToUpper());
                if (partitionBlocks.Any(x => AmigaTextHelper.GetBytes(x.DriveName).SequenceEqual(nameBytes)))
                {
                    return new Result(new Error($"Partition name '{name}' already exists"));
                }

                partitionBlock.DriveName = name;

                hasChanges = true;
                OnDebugMessage($"Drive name '{name}'");
            }

            var flags = partitionBlock.Flags;
            
            // update bootable
            if (bootable.HasValue)
            {
                // remove bootable flag
                flags &= ~(uint)PartitionBlock.PartitionFlagsEnum.Bootable;

                // set bootable, if true
                if (bootable.Value)
                {
                    flags |= (int)PartitionBlock.PartitionFlagsEnum.Bootable;
                }
                
                hasChanges = true;
                OnDebugMessage($"Bootable '{(flags & (int)PartitionBlock.PartitionFlagsEnum.Bootable) == (int)PartitionBlock.PartitionFlagsEnum.Bootable}'");
            }
            
            // update no mount
            if (noMount.HasValue)
            {
                // remove no mount flag
                flags &= ~(uint)PartitionBlock.PartitionFlagsEnum.NoMount;
                
                // set no mount, if true
                if (noMount.Value)
                {
                    flags |= (int)PartitionBlock.PartitionFlagsEnum.NoMount;
                }
                
                hasChanges = true;
                OnDebugMessage($"Bootable '{(flags & (int)PartitionBlock.PartitionFlagsEnum.NoMount) == (int)PartitionBlock.PartitionFlagsEnum.NoMount}'");
            }

            // update flags, if changed
            if (flags != partitionBlock.Flags)
            {
                partitionBlock.Flags = flags;
            }

            // update reserved
            if (reserved.HasValue)
            {
                partitionBlock.Reserved = (uint)reserved;
                hasChanges = true;
                OnDebugMessage($"Reserved '{partitionBlock.Reserved}'");
            }

            // update prealloc
            if (preAlloc.HasValue)
            {
                partitionBlock.PreAlloc = (uint)preAlloc;
                hasChanges = true;
                OnDebugMessage($"PreAlloc '{partitionBlock.PreAlloc}'");
            }

            // update buffers
            if (buffers.HasValue)
            {
                partitionBlock.NumBuffer = (uint)buffers;
                hasChanges = true;
                OnDebugMessage($"Buffers '{partitionBlock.NumBuffer}'");
            }
            
            // update max transfer
            if (maxTransfer.HasValue)
            {
                partitionBlock.MaxTransfer = maxTransfer.Value;
                hasChanges = true;
                OnDebugMessage($"Max Transfer '0x{partitionBlock.MaxTransfer.FormatHex().ToUpper()}, {partitionBlock.MaxTransfer}'");
            }

            // update mask
            if (mask.HasValue)
            {
                partitionBlock.Mask = mask.Value;
                hasChanges = true;
                OnDebugMessage($"Mask '0x{partitionBlock.Mask.FormatHex().ToUpper()}, {partitionBlock.Mask}'");
            }
            
            // update boot priority
            if (bootPriority.HasValue)
            {
                partitionBlock.BootPriority = (uint)bootPriority;
                hasChanges = true;
                OnDebugMessage($"Boot priority '{partitionBlock.BootPriority}'");
            }

            // update block size
            if (blockSize.HasValue)
            {
                partitionBlock.SizeBlock = (uint)(blockSize / Hst.Amiga.SizeOf.Long);
                hasChanges = true;
                OnDebugMessage($"SizeBlock '{partitionBlock.SizeBlock * Hst.Amiga.SizeOf.Long}'");
            }

            if (!hasChanges)
            {
                OnDebugMessage($"No Rigid Disk Block changes");
                return new Result();
            }
            
            OnDebugMessage("Writing Rigid Disk Block");
            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

            return new Result();
        }
    }
}