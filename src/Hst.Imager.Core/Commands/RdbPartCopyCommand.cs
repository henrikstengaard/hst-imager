﻿namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amiga.RigidDiskBlocks;
    using Extensions;
    using Hst.Core;
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

        public event EventHandler<DataProcessedEventArgs> DataProcessed;

        public RdbPartCopyCommand(ILogger<RdbPartCopyCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string sourcePath, int partitionNumber, string destinationPath,
            string name)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.sourcePath = sourcePath;
            this.partitionNumber = partitionNumber;
            this.destinationPath = destinationPath;
            this.name = name;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Copying partition from '{sourcePath}' to '{destinationPath}'");

            OnDebugMessage($"Opening source path '{sourcePath}' as readable");

            var sourceMediaResult =
                commandHelper.GetReadableMedia(physicalDrives, sourcePath);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }

            using var sourceMedia = sourceMediaResult.Value;
            await using var sourceStream = sourceMedia.Stream;

            OnDebugMessage("Reading source Rigid Disk Block");

            var sourceRigidDiskBlock = await commandHelper.GetRigidDiskBlock(sourceStream);

            var sourcePartitionBlocks = sourceRigidDiskBlock.PartitionBlocks.ToList();

            OnDebugMessage($"Copying source partition number '{partitionNumber}'");

            if (partitionNumber < 1 || partitionNumber > sourcePartitionBlocks.Count)
            {
                return new Result(new Error($"Invalid source partition number '{partitionNumber}'"));
            }

            // get partition block to copy
            var partitionBlock = sourcePartitionBlocks[partitionNumber - 1];

            OnInformationMessage($"Source partition number '{partitionNumber}':");
            OnInformationMessage($"Name '{partitionBlock.DriveName}'");
            OnInformationMessage($"LowCyl '{partitionBlock.LowCyl}'");
            OnInformationMessage($"HighCyl '{partitionBlock.HighCyl}'");
            OnInformationMessage($"Size '{partitionBlock.PartitionSize.FormatBytes()}' ({partitionBlock.PartitionSize} bytes)");

            OnDebugMessage($"Opening destination path '{destinationPath}' as writable");

            var destinationMediaResult =
                commandHelper.GetWritableMedia(physicalDrives, destinationPath);
            if (destinationMediaResult.IsFaulted)
            {
                return new Result(destinationMediaResult.Error);
            }

            using var destinationMedia = destinationMediaResult.Value;
            await using var destinationStream = destinationMedia.Stream;

            OnDebugMessage("Reading destination Rigid Disk Block");

            var destinationRigidDiskBlock = await commandHelper.GetRigidDiskBlock(destinationStream);
            var destinationPartitionBlocks = destinationRigidDiskBlock.PartitionBlocks.ToList();
            
            var driveName = (!string.IsNullOrWhiteSpace(name) ? name : partitionBlock.DriveName).ToUpper();
            
            // return error, if partition with name already exists
            if (destinationPartitionBlocks.Any(x => x.DriveName.ToUpper() == driveName))
            {
                return new Result(new Error($"Partition name '{driveName}' already exists in destination Rigid Disk Block"));
            }

            // calculate source cylinder size, offset and size
            var sourceCylinderSize = sourceRigidDiskBlock.Heads * sourceRigidDiskBlock.Sectors *
                                     sourceRigidDiskBlock.BlockSize;
            var sourceOffset = partitionBlock.LowCyl * sourceCylinderSize;
            var sourceSize = (partitionBlock.HighCyl - partitionBlock.LowCyl + 1) * sourceCylinderSize;
            
            // update partition block
            var destinationPartitionBlock = PartitionBlock.Create(destinationRigidDiskBlock, partitionBlock.DosType,
                driveName, sourceSize);
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

            OnDebugMessage($"Destination partition number '{destinationPartitionBlocks.IndexOf(destinationPartitionBlock) + 1}':");
            OnDebugMessage($"Name '{destinationPartitionBlock.DriveName}'");
            OnDebugMessage($"LowCyl '{destinationPartitionBlock.LowCyl}'");
            OnDebugMessage($"HighCyl '{destinationPartitionBlock.HighCyl}'");
            OnDebugMessage($"Size '{destinationPartitionBlock.PartitionSize.FormatBytes()}' ({destinationPartitionBlock.PartitionSize} bytes)");
            
            OnDebugMessage("Writing destination Rigid Disk Block");
            await RigidDiskBlockWriter.WriteBlock(destinationRigidDiskBlock, destinationStream);

            var destinationCylinderSize = destinationRigidDiskBlock.Heads * destinationRigidDiskBlock.Sectors *
                                          destinationRigidDiskBlock.BlockSize;
            var destinationOffset = destinationPartitionBlock.LowCyl * destinationCylinderSize;
            
            var isVhd = commandHelper.IsVhd(destinationPath);

            OnDebugMessage($"Copying partition from source offset '{sourceOffset}' to destination offset '{destinationOffset}'");
            
            var streamCopier = new StreamCopier();
            streamCopier.DataProcessed += (_, e) =>
            {
                OnDataProcessed(e.PercentComplete, e.BytesProcessed, e.BytesRemaining, e.BytesTotal, e.TimeElapsed,
                    e.TimeRemaining, e.TimeTotal);
            };
            await streamCopier.Copy(token, sourceStream, destinationStream, sourceSize, sourceOffset, destinationOffset, isVhd);
            
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