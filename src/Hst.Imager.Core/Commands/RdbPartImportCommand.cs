namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amiga;
    using Amiga.RigidDiskBlocks;
    using Extensions;
    using Hst.Core;
    using Microsoft.Extensions.Logging;

    public class RdbPartImportCommand : CommandBase
    {
        private readonly ILogger<RdbPartImportCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string sourcePath;
        private readonly string destinationPath;
        private readonly string name;
        private readonly string dosType;
        private readonly bool bootable;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;

        public RdbPartImportCommand(ILogger<RdbPartImportCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath, string destinationPath, string name,
            string dosType, bool bootable)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.sourcePath = sourcePath;
            this.destinationPath = destinationPath;
            this.name = name;
            this.dosType = dosType;
            this.bootable = bootable;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            if (dosType.Length != 4)
            {
                return new Result(new Error("DOS type must be 4 characters"));
            }

            OnProgressMessage($"Opening source path '{sourcePath}' as readable");

            var sourceMediaResult =
                commandHelper.GetReadableMedia(physicalDrives, sourcePath, allowPhysicalDrive: false);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            using var sourceMedia = sourceMediaResult.Value;
            await using var sourceStream = sourceMedia.Stream;

            OnProgressMessage($"Opening destination path '{destinationPath}' as writable");

            var destinationMediaResult =
                commandHelper.GetWritableMedia(physicalDrives, destinationPath, allowPhysicalDrive: false);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            using var destinationMedia = destinationMediaResult.Value;
            await using var destinationStream = destinationMedia.Stream;

            OnProgressMessage("Reading destination Rigid Disk Block");

            var destinationRigidDiskBlock = await commandHelper.GetRigidDiskBlock(destinationStream);

            var destinationPartitionBlocks = destinationRigidDiskBlock.PartitionBlocks.ToList();

            var dosTypeBytes = DosTypeHelper.FormatDosType(dosType.ToUpper());

            var fileSystemHeaderBlock =
                destinationRigidDiskBlock.FileSystemHeaderBlocks.FirstOrDefault(x =>
                    x.DosType.SequenceEqual(dosTypeBytes));

            if (fileSystemHeaderBlock == null)
            {
                return new Result(new Error($"File system with DOS type '{dosType}' not found in Rigid Disk Block"));
            }

            var partitionBlock = PartitionBlock.Create(destinationRigidDiskBlock, dosTypeBytes, name,
                sourceStream.Length, bootable);

            var nameBytes = AmigaTextHelper.GetBytes(name.ToUpper());
            if (destinationPartitionBlocks.Any(x => AmigaTextHelper.GetBytes(x.DriveName).SequenceEqual(nameBytes)))
            {
                return new Result(new Error($"Partition name '{name}' already exists"));
            }

            OnProgressMessage(
                $"Size '{partitionBlock.PartitionSize.FormatBytes()}' ({partitionBlock.PartitionSize} bytes)");
            OnProgressMessage($"Low cyl '{partitionBlock.LowCyl}'");
            OnProgressMessage($"High cyl '{partitionBlock.HighCyl}'");
            OnProgressMessage($"Reserved '{partitionBlock.Reserved}'");
            OnProgressMessage($"PreAlloc '{partitionBlock.PreAlloc}'");
            OnProgressMessage($"Buffers '{partitionBlock.NumBuffer}'");
            OnProgressMessage($"Max Transfer '{partitionBlock.MaxTransfer}'");
            OnProgressMessage($"SizeBlock '{partitionBlock.SizeBlock * SizeOf.Long}'");

            destinationRigidDiskBlock.PartitionBlocks =
                destinationRigidDiskBlock.PartitionBlocks.Concat(new[] { partitionBlock });

            // calculate source cylinder size, offset and size
            var destinationCylinderSize = destinationRigidDiskBlock.Heads * destinationRigidDiskBlock.Sectors *
                                          destinationRigidDiskBlock.BlockSize;
            var destinationOffset = partitionBlock.LowCyl * destinationCylinderSize;

            OnProgressMessage($"Importing partition from '{sourcePath}' to '{destinationPath}'");
            OnProgressMessage($"destinationOffset '{destinationOffset}'");

            var isVhd = commandHelper.IsVhd(destinationPath);

            var streamCopier = new StreamCopier();
            streamCopier.DataProcessed += (_, e) =>
            {
                OnDataProcessed(e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal);
            };
            await streamCopier.Copy(token, sourceStream, destinationStream, sourceStream.Length, 0, destinationOffset,
                isVhd);

            OnProgressMessage("Writing destination Rigid Disk Block");
            await RigidDiskBlockWriter.WriteBlock(destinationRigidDiskBlock, destinationStream);

            return new Result();
        }

        private void OnDataProcessed(double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal)
        {
            DataProcessed?.Invoke(this,
                new DataProcessedEventArgs(percentComplete, bytesProcessed, bytesRemaining, bytesTotal, timeElapsed,
                    timeRemaining, timeTotal));
        }
    }
}