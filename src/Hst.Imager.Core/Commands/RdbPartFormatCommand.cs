namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amiga.Extensions;
    using Amiga.FileSystems.FastFileSystem;
    using Amiga.FileSystems.Pfs3;
    using Amiga.RigidDiskBlocks;
    using Extensions;
    using Hst.Core;
    using Hst.Core.Extensions;
    using Hst.Imager.Core.Helpers;
    using Microsoft.Extensions.Logging;

    public class RdbPartFormatCommand : CommandBase
    {
        private readonly ILogger<RdbPartFormatCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly int partitionNumber;
        private readonly string name;
        private readonly bool nonRdb;
        private readonly string chs;
        private readonly string dosType;

        public RdbPartFormatCommand(ILogger<RdbPartFormatCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber, string name, bool nonRdb,
            string chs, string dosType)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.partitionNumber = partitionNumber;
            this.name = name;
            this.nonRdb = nonRdb;
            this.chs = chs;
            this.dosType = dosType;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            var nonRdbDiskGeometry = new RdbDiskGeometry
            {
                Heads = 16,
                Sectors = 63
            };

            if (nonRdb && string.IsNullOrWhiteSpace(dosType))
            {
                return new Result(new Error($"DOS type is required for formatting a non-RDB partition"));
            }

            if (nonRdb && !string.IsNullOrWhiteSpace(chs))
            {
                var values = chs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                if (values.Length != 3)
                {
                    return new Result(new Error($"Invalid cylinders, heads and sectors value '{chs}'"));
                }

                if (!int.TryParse(values[0], out var cylinders) || !int.TryParse(values[1], out var heads) ||
                    !int.TryParse(values[2], out var sectors))
                {
                    return new Result(new Error($"Invalid cylinders, heads and sectors value '{chs}'"));
                }

                nonRdbDiskGeometry = new RdbDiskGeometry
                {
                    DiskSize = (long)cylinders * heads * sectors * 512,
                    Cylinders = cylinders,
                    Heads = heads,
                    Sectors = sectors
                };
            }

            OnInformationMessage($"Formatting partition in Rigid Disk Block at '{path}'");

            OnDebugMessage($"Opening '{path}' as writable");

            var writableMediaResult = await commandHelper.GetWritableMedia(physicalDrives, path);
            if (writableMediaResult.IsFaulted)
            {
                return new Result(writableMediaResult.Error);
            }

            using var media = MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, writableMediaResult.Value, path);

            if (nonRdb && media.IsPhysicalDrive)
            {
                return new Result(new Error($"Physical drives doesn't support non RDB"));
            }

            var stream = media.Stream;

            List<PartitionBlock> partitionBlocks;
            if (!nonRdb)
            {
                OnDebugMessage("Reading Rigid Disk Block");

                var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(media);

                if (rigidDiskBlock == null)
                {
                    return new Result(new Error("Rigid Disk Block not found"));
                }

                partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
            }
            else
            {
                var cylinderSize = (long)nonRdbDiskGeometry.Heads * nonRdbDiskGeometry.Sectors * 512;
                var cylinders = nonRdbDiskGeometry.Cylinders == 0
                    ? Convert.ToInt64((double)stream.Length / cylinderSize)
                    : nonRdbDiskGeometry.Cylinders;
                var partitionSize = cylinderSize * cylinders;

                if (stream.Length != partitionSize)
                {
                    stream.SetLength(partitionSize);
                }

                // non rdb adds a dummy dh0 partition to represent the partition
                partitionBlocks = new List<PartitionBlock>
                {
                    new PartitionBlock
                    {
                        DriveName = "DH0",
                        DosType = DosTypeHelper.FormatDosType(dosType),
                        Surfaces = (uint)nonRdbDiskGeometry.Heads,
                        BlocksPerTrack = (uint)nonRdbDiskGeometry.Sectors,
                        Reserved = 2,
                        PartitionSize = partitionSize,
                        LowCyl = 0,
                        HighCyl = (uint)(cylinders - 1),
                        FileSystemBlockSize = 512
                    }
                };
            }

            OnDebugMessage($"Formatting partition number '{partitionNumber}':");

            if (partitionNumber < 1 || partitionNumber > partitionBlocks.Count)
            {
                return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
            }

            var partitionBlock = partitionBlocks[partitionNumber - 1];

            OnInformationMessage(
                $"- Size '{partitionBlock.PartitionSize.FormatBytes()}' ({partitionBlock.PartitionSize} bytes)");
            if (nonRdb)
            {
                OnInformationMessage($"- Cylinders '{(partitionBlock.HighCyl - partitionBlock.LowCyl + 1)}'");
                OnInformationMessage($"- Heads '{partitionBlock.Surfaces}'");
                OnInformationMessage($"- Sectors '{partitionBlock.BlocksPerTrack}'");
            }

            OnInformationMessage($"- Low Cyl '{partitionBlock.LowCyl}'");
            OnInformationMessage($"- High Cyl '{partitionBlock.HighCyl}'");
            OnInformationMessage($"- Reserved '{partitionBlock.Reserved}'");
            if (!nonRdb)
            {
                OnInformationMessage($"- Name '{partitionBlock.DriveName}'");
            }

            OnInformationMessage(
                $"- DOS type '0x{partitionBlock.DosType.FormatHex()}' ({partitionBlock.DosType.FormatDosType()})");
            OnInformationMessage($"- Volume name '{name}'");

            switch (partitionBlock.DosTypeFormatted)
            {
                case "DOS\\0":
                case "DOS\\1":
                case "DOS\\2":
                case "DOS\\3":
                case "DOS\\4":
                case "DOS\\5":
                case "DOS\\6":
                case "DOS\\7":
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