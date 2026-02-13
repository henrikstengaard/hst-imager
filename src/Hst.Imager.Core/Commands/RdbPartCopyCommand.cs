using Hst.Amiga;
using Hst.Amiga.Extensions;
using Hst.Core.Extensions;

namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amiga.RigidDiskBlocks;
    using Extensions;
    using Hst.Core;
    using Hst.Imager.Core.Helpers;
    using Microsoft.Extensions.Logging;

    public class RdbPartCopyCommand : CommandBase
    {
        private readonly ILogger<RdbPartCopyCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string sourcePath;
        private readonly int partitionNumber;
        private readonly string destinationPath;
        private readonly string name;
        private readonly string dosType;
        private long statusBytesProcessed;
        private TimeSpan statusTimeElapsed;

        public RdbPartCopyCommand(ILogger<RdbPartCopyCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath, int partitionNumber, string destinationPath,
            string name, string dosType)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.sourcePath = sourcePath;
            this.partitionNumber = partitionNumber;
            this.destinationPath = destinationPath;
            this.name = name;
            this.dosType = dosType;
            statusBytesProcessed = 0;
            statusTimeElapsed = TimeSpan.Zero;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Copying partition from '{sourcePath}' to '{destinationPath}'");

            OnDebugMessage($"Opening source path '{sourcePath}' as readable");

            var sourceMediaResult =
                await commandHelper.GetReadableMedia(physicalDrives, sourcePath);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            using var sourceMedia = MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, sourceMediaResult.Value, sourcePath);
            var sourceStream = sourceMedia.Stream;
            OnDebugMessage("Reading source Rigid Disk Block");

            var sourceRigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(sourceMedia);

            var sourcePartitionBlocks = sourceRigidDiskBlock.PartitionBlocks.ToList();

            OnDebugMessage($"Copying source partition number '{partitionNumber}'");

            if (partitionNumber < 1 || partitionNumber > sourcePartitionBlocks.Count)
            {
                return new Result(new Error($"Invalid source partition number '{partitionNumber}'"));
            }

            // get partition block to copy
            var partitionBlock = sourcePartitionBlocks[partitionNumber - 1];

            OnInformationMessage($"- Size '{partitionBlock.PartitionSize.FormatBytes()}' ({partitionBlock.PartitionSize} bytes)");
            OnInformationMessage($"- Name '{partitionBlock.DriveName}'");
            OnInformationMessage($"- LowCyl '{partitionBlock.LowCyl}'");
            OnInformationMessage($"- HighCyl '{partitionBlock.HighCyl}'");
            OnInformationMessage($"- DOS type '0x{partitionBlock.DosType.FormatHex().ToUpper()}' ({partitionBlock.DosType.FormatDosType()})");

            OnDebugMessage($"Opening destination path '{destinationPath}' as writable");

            var destinationMediaResult =
                await commandHelper.GetWritableMedia(physicalDrives, destinationPath);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            using var destinationMedia = MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, destinationMediaResult.Value, destinationPath);
            var destinationStream = destinationMedia.Stream;

            OnDebugMessage("Reading destination Rigid Disk Block");

            var destinationRigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(destinationMedia);

            if (sourceRigidDiskBlock.Heads != destinationRigidDiskBlock.Heads)
            {
                return new Result(new Error($"Source Rigid Disk Block heads '{sourceRigidDiskBlock.Heads}' is not equal to destination Rigid Disk Block heads '{destinationRigidDiskBlock.Heads}'"));
            }

            if (sourceRigidDiskBlock.Sectors != destinationRigidDiskBlock.Sectors)
            {
                return new Result(new Error($"Source Rigid Disk Block sectors '{sourceRigidDiskBlock.Sectors}' is not equal to destination Rigid Disk Block sectors '{destinationRigidDiskBlock.Sectors}'"));
            }

            var destinationDosTypeBytes = partitionBlock.DosType;
            if (!string.IsNullOrWhiteSpace(dosType))
            {
                destinationDosTypeBytes = DosTypeHelper.FormatDosType(dosType.ToUpper());
            }
            
            var destinationPartitionBlocks = destinationRigidDiskBlock.PartitionBlocks.ToList();
            
            var fileSystemHeaderBlock =
                destinationRigidDiskBlock.FileSystemHeaderBlocks.FirstOrDefault(x =>
                    x.DosType.SequenceEqual(destinationDosTypeBytes));

            // return error, if file system with dos type not found in destination rigid disk block
            if (fileSystemHeaderBlock == null)
            {
                return new Result(new Error($"File system with DOS type '{FormatDosType(destinationDosTypeBytes)}' not found in destination Rigid Disk Block"));
            }

            var destName = (!string.IsNullOrWhiteSpace(name) ? name : partitionBlock.DriveName).ToUpper();
            
            // return error, if partition with name already exists
            var destinationNameBytes = AmigaTextHelper.GetBytes(destName.ToUpper());
            if (destinationPartitionBlocks.Any(x => AmigaTextHelper.GetBytes(x.DriveName).SequenceEqual(destinationNameBytes)))
            {
                return new Result(new Error($"Partition name '{name}' already exists in destination Rigid Disk Block"));
            }

            // calculate source cylinder size, offset and size
            var sourceCylinderSize = sourceRigidDiskBlock.Heads * sourceRigidDiskBlock.Sectors *
                                     sourceRigidDiskBlock.BlockSize;
            var sourceOffset = (long)partitionBlock.LowCyl * sourceCylinderSize;
            var sourceSize = ((long)partitionBlock.HighCyl - partitionBlock.LowCyl + 1) * sourceCylinderSize;
            
            // update partition block
            var destinationPartitionBlock = PartitionBlock.Create(destinationRigidDiskBlock, destinationDosTypeBytes,
                destName, sourceSize);
            destinationPartitionBlock.Flags = partitionBlock.Flags;
            destinationPartitionBlock.NumBuffer = partitionBlock.NumBuffer;
            destinationPartitionBlock.Reserved = partitionBlock.Reserved;
            destinationPartitionBlock.PreAlloc = partitionBlock.PreAlloc;
            destinationPartitionBlock.FileSystemBlockSize = partitionBlock.FileSystemBlockSize;
            destinationPartitionBlock.MaxTransfer = partitionBlock.MaxTransfer;
            destinationPartitionBlock.Mask = partitionBlock.Mask;

            // add partition to destination rigid disk block
            destinationPartitionBlocks.Add(destinationPartitionBlock);
            destinationRigidDiskBlock.PartitionBlocks = destinationPartitionBlocks;

            OnInformationMessage($"Destination partition number '{destinationPartitionBlocks.IndexOf(destinationPartitionBlock) + 1}':");
            OnInformationMessage($"- Size '{destinationPartitionBlock.PartitionSize.FormatBytes()}' ({destinationPartitionBlock.PartitionSize} bytes)");
            OnInformationMessage($"- Name '{destinationPartitionBlock.DriveName}'");
            OnInformationMessage($"- LowCyl '{destinationPartitionBlock.LowCyl}'");
            OnInformationMessage($"- HighCyl '{destinationPartitionBlock.HighCyl}'");
            OnInformationMessage($"- DOS type '0x{destinationPartitionBlock.DosType.FormatHex().ToUpper()}' ({destinationPartitionBlock.DosType.FormatDosType()})");
            
            OnDebugMessage("Writing destination Rigid Disk Block");
            await RigidDiskBlockWriter.WriteBlock(destinationRigidDiskBlock, destinationStream);

            var destinationCylinderSize = destinationRigidDiskBlock.Heads * destinationRigidDiskBlock.Sectors *
                                          destinationRigidDiskBlock.BlockSize;
            var destinationOffset = (long)destinationPartitionBlock.LowCyl * destinationCylinderSize;
            
            var isVhd = commandHelper.IsVhd(destinationPath);

            OnDebugMessage($"Copying partition from source offset '{sourceOffset}' to destination offset '{destinationOffset}'");
            
            using var streamCopier = new StreamCopier();
            streamCopier.DataProcessed += (_, e) =>
            {
                statusBytesProcessed = e.BytesProcessed;
                statusTimeElapsed = e.TimeElapsed;
                OnDataProcessed(e.Indeterminate, e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal, e.BytesPerSecond);
            };
            await streamCopier.Copy(token, sourceStream, destinationStream, sourceSize, sourceOffset, destinationOffset, isVhd);
            
            OnInformationMessage($"Copied '{statusBytesProcessed.FormatBytes()}' ({statusBytesProcessed} bytes) in {statusTimeElapsed.FormatElapsed()}");
            
            return new Result();
        }

        private static string FormatDosType(byte[] dosTypeBytes)
        {
            if (dosTypeBytes.Length != 4)
            {
                throw new ArgumentException("Dos type must be 4 bytes in length", nameof(dosType));
            }

            if (dosTypeBytes[3] + 48 > 255)
            {
                throw new ArgumentException($"Dos type contains invalid version {dosTypeBytes[3]}", nameof(dosType));
            }
            
            return string.Concat(AmigaTextHelper.Iso88591.GetString(dosTypeBytes.AsSpan(0, 3)),
                AmigaTextHelper.Iso88591.GetString(new byte[] { (byte)(dosTypeBytes[3] + 48) }));
        }
    }
}