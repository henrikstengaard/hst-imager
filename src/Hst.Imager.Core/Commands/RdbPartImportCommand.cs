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
        private readonly int fileSystemBlockSize;
        private readonly bool bootable;
        private long statusBytesProcessed;
        private TimeSpan statusTimeElapsed;

        public event EventHandler<DataProcessedEventArgs> DataProcessed;

        public RdbPartImportCommand(ILogger<RdbPartImportCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath, string destinationPath, string name,
            string dosType, int fileSystemBlockSize, bool bootable)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.sourcePath = sourcePath;
            this.destinationPath = destinationPath;
            this.name = name;
            this.dosType = dosType;
            this.bootable = bootable;
            this.fileSystemBlockSize = fileSystemBlockSize;
            this.statusBytesProcessed = 0;
            this.statusTimeElapsed = TimeSpan.Zero;
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
            
            OnInformationMessage($"Importing partition from '{sourcePath}' to '{destinationPath}'");
            
            OnDebugMessage($"Opening source path '{sourcePath}' as readable");

            var sourceMediaResult =
                commandHelper.GetReadableMedia(physicalDrives, sourcePath);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            using var sourceMedia = sourceMediaResult.Value;
            var sourceStream = sourceMedia.Stream;

            OnDebugMessage($"Opening destination path '{destinationPath}' as writable");

            var destinationMediaResult =
                commandHelper.GetWritableMedia(physicalDrives, destinationPath);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            using var destinationMedia = destinationMediaResult.Value;
            var destinationStream = destinationMedia.Stream;

            OnDebugMessage("Reading destination Rigid Disk Block");

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
                sourceStream.Length, fileSystemBlockSize, bootable);

            var nameBytes = AmigaTextHelper.GetBytes(name.ToUpper());
            if (destinationPartitionBlocks.Any(x => AmigaTextHelper.GetBytes(x.DriveName).SequenceEqual(nameBytes)))
            {
                return new Result(new Error($"Partition name '{name}' already exists"));
            }

            OnInformationMessage(
                $"- Size '{partitionBlock.PartitionSize.FormatBytes()}' ({partitionBlock.PartitionSize} bytes)");
            OnInformationMessage($"- Low Cyl '{partitionBlock.LowCyl}'");
            OnInformationMessage($"- High Cyl '{partitionBlock.HighCyl}'");
            OnInformationMessage($"- Reserved '{partitionBlock.Reserved}'");
            OnInformationMessage($"- PreAlloc '{partitionBlock.PreAlloc}'");
            OnInformationMessage($"- Buffers '{partitionBlock.NumBuffer}'");
            OnInformationMessage($"- Max Transfer '{partitionBlock.MaxTransfer}'");
            OnInformationMessage($"- File System Block Size '{partitionBlock.FileSystemBlockSize}'");

            destinationRigidDiskBlock.PartitionBlocks =
                destinationRigidDiskBlock.PartitionBlocks.Concat(new[] { partitionBlock });

            // calculate source cylinder size, offset and size
            var destinationCylinderSize = destinationRigidDiskBlock.Heads * destinationRigidDiskBlock.Sectors *
                                          destinationRigidDiskBlock.BlockSize;
            var destinationOffset = (long)partitionBlock.LowCyl * destinationCylinderSize;
            const int sourceOffset = 0;

            OnDebugMessage($"Importing partition from source offset '{sourceOffset}' to destination offset '{destinationOffset}'");

            var isVhd = commandHelper.IsVhd(destinationPath);

            var streamCopier = new StreamCopier();
            streamCopier.DataProcessed += (_, e) =>
            {
                statusBytesProcessed = e.BytesProcessed;
                statusTimeElapsed = e.TimeElapsed;
                OnDataProcessed(e.Indeterminate, e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal, e.BytesPerSecond);
            };
            await streamCopier.Copy(token, sourceStream, destinationStream, sourceStream.Length, 0, destinationOffset,
                isVhd);

            OnDebugMessage("Writing destination Rigid Disk Block");
            await RigidDiskBlockWriter.WriteBlock(destinationRigidDiskBlock, destinationStream);

            OnInformationMessage($"Imported '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) in {statusTimeElapsed.FormatElapsed()}");
            
            return new Result();
        }

        private void OnDataProcessed(bool indeterminate, double percentComplete, long bytesProcessed, long bytesRemaining, long bytesTotal,
            TimeSpan timeElapsed, TimeSpan timeRemaining, TimeSpan timeTotal, long bytesPerSecond)
        {
            DataProcessed?.Invoke(this,
                new DataProcessedEventArgs(indeterminate, percentComplete, bytesProcessed, bytesRemaining, bytesTotal, timeElapsed,
                    timeRemaining, timeTotal, bytesPerSecond));
        }
    }
}