namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amiga.Extensions;
    using Extensions;
    using Amiga;
    using Amiga.RigidDiskBlocks;
    using Hst.Core;
    using Hst.Core.Extensions;
    using Microsoft.Extensions.Logging;
    using Models;
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
        private readonly uint? reserved;
        private readonly uint? preAlloc;
        private readonly uint? buffers;
        private readonly uint? maxTransfer;
        private readonly uint? mask;
        private readonly string dosType;
        private readonly bool noMount;
        private readonly bool bootable;
        private readonly int? bootPriority;
        private readonly int? fileSystemBlockSize;

        public RdbPartAddCommand(ILogger<RdbPartAddCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, string name, string dosType, Size size,
            uint? reserved, uint? preAlloc, uint? buffers, uint? maxTransfer, uint? mask, bool noMount, bool bootable,
            int? bootPriority, int? fileSystemBlockSize)
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
            this.mask = mask;
            this.dosType = dosType;
            this.noMount = noMount;
            this.bootable = bootable;
            this.bootPriority = bootPriority;
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

            var mediaResult = commandHelper.GetWritableMedia(physicalDrives, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            using var media = mediaResult.Value;
            var stream = media.Stream;

            OnDebugMessage("Reading Rigid Disk Block");

            var diskInfo = await commandHelper.ReadDiskInfo(media);
            var rigidDiskBlock = diskInfo.RigidDiskBlock;

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
            if (partitionBlocks.Any(x => AmigaTextHelper.GetBytes(x.DriveName.ToUpper()).SequenceEqual(nameBytes)))
            {
                return new Result(new Error($"Partition name '{name}' already exists"));
            }

            var partitionSize = size.Value == 0 && size.Unit == Unit.Bytes
                ? 0
                : rigidDiskBlock.DiskSize.ResolveSize(size).ToSectorSize();

            OnInformationMessage($"- Partition number '{partitionBlocks.Count + 1}'");
            OnInformationMessage($"- Name '{name}'");
            OnInformationMessage($"- DOS type '{dosTypeBytes.FormatDosType()}'");

            // find unallocated part for partition size
            var rdbPartitionTable =
                diskInfo.PartitionTables.FirstOrDefault(x => x.Type == PartitionTableType.RigidDiskBlock);
            if (rdbPartitionTable == null)
            {
                return new Result(new Error("Rigid Disk Block not found"));
            }
            
            var unallocatedPart = diskInfo.RdbPartitionTablePart.Parts.FirstOrDefault(x =>
                x.PartType == PartType.Unallocated && x.StartOffset > rdbPartitionTable.Reserved.EndOffset &&
                x.Size >= partitionSize);
            if (unallocatedPart == null)
            {
                return new Result(new Error(
                    $"Rigid Disk Block does not have unallocated disk space for partition size '{size}' ({partitionSize} bytes)"));
            }

            var cylinderSize = diskInfo.RigidDiskBlock.Sectors * diskInfo.RigidDiskBlock.Heads *
                               diskInfo.RigidDiskBlock.BlockSize;
            var lowCyl = Convert.ToUInt32(Math.Ceiling((double)unallocatedPart.StartOffset / cylinderSize));
            if (lowCyl < diskInfo.RigidDiskBlock.LoCylinder)
            {
                lowCyl = diskInfo.RigidDiskBlock.LoCylinder;
            }

            var cylinders = partitionSize == 0
                ? diskInfo.RigidDiskBlock.HiCylinder - lowCyl + 1
                : Convert.ToUInt32(Math.Ceiling((double)partitionSize / cylinderSize));

            if (cylinders <= 0)
            {
                return new Result(new Error(
                    $"Invalid cylinders for partition size '{partitionSize}', low cyl '{lowCyl}', cylinder size '{cylinderSize}', rdb hi cyl '{diskInfo.RigidDiskBlock.HiCylinder}'"));
            }

            var highCyl = lowCyl + cylinders - 1;
            partitionSize = (long)cylinders * cylinderSize;

            var partitionBlock = new PartitionBlock
            {
                PartitionSize = partitionSize,
                DosType = dosTypeBytes,
                DriveName = name,
                Flags = bootable ? (uint)PartitionFlagsEnum.Bootable : 0,
                LowCyl = lowCyl,
                HighCyl = highCyl,
                BlocksPerTrack = rigidDiskBlock.Sectors,
                Surfaces = rigidDiskBlock.Heads,
                FileSystemBlockSize = (uint)(fileSystemBlockSize ?? 512),
                Sectors = (uint)((fileSystemBlockSize ?? 512) / rigidDiskBlock.BlockSize),
                SizeBlock = rigidDiskBlock.BlockSize / SizeOf.Long
            };

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

            if (reserved.HasValue)
            {
                partitionBlock.Reserved = reserved.Value;
            }

            if (preAlloc.HasValue)
            {
                partitionBlock.PreAlloc = preAlloc.Value;
            }

            if (maxTransfer.HasValue)
            {
                partitionBlock.MaxTransfer = maxTransfer.Value;
            }

            if (mask.HasValue)
            {
                partitionBlock.Mask = mask.Value;
            }

            if (buffers.HasValue)
            {
                partitionBlock.NumBuffer = buffers.Value;
            }

            partitionBlock.Flags = flags;

            if (bootPriority.HasValue)
            {
                partitionBlock.BootPriority = bootPriority.Value;
            }

            partitionBlock.SizeBlock = rigidDiskBlock.BlockSize / SizeOf.ULong;

            OnInformationMessage(
                $"- Size '{partitionBlock.PartitionSize.FormatBytes()}' ({partitionBlock.PartitionSize} bytes)");
            OnInformationMessage($"- Low Cyl '{partitionBlock.LowCyl}'");
            OnInformationMessage($"- High Cyl '{partitionBlock.HighCyl}'");
            OnInformationMessage($"- Reserved '{partitionBlock.Reserved}'");
            OnInformationMessage($"- PreAlloc '{partitionBlock.PreAlloc}'");
            OnInformationMessage($"- Buffers '{partitionBlock.NumBuffer}'");
            OnInformationMessage(
                $"- Max Transfer '0x{partitionBlock.MaxTransfer.FormatHex().ToUpper()}' ({partitionBlock.MaxTransfer})");
            OnInformationMessage($"- Mask '0x{partitionBlock.Mask.FormatHex().ToUpper()}' ({partitionBlock.Mask})");
            OnInformationMessage($"- Bootable '{partitionBlock.Bootable.ToString()}'");
            OnInformationMessage($"- Boot priority '{partitionBlock.BootPriority}'");
            OnInformationMessage($"- No mount '{partitionBlock.NoMount}'");
            OnInformationMessage($"- File System Block Size '{partitionBlock.Sectors * rigidDiskBlock.BlockSize}'");

            rigidDiskBlock.PartitionBlocks = rigidDiskBlock.PartitionBlocks.Concat(new[] { partitionBlock });

            OnDebugMessage("Writing Rigid Disk Block");
            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

            return new Result();
        }
    }
}