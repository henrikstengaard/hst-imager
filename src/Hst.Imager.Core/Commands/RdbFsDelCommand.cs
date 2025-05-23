﻿namespace Hst.Imager.Core.Commands
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Core;
    using Hst.Imager.Core.Helpers;
    using Microsoft.Extensions.Logging;

    public class RdbFsDelCommand : CommandBase
    {
        private readonly ILogger<RdbFsDelCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly int fileSystemNumber;

        public RdbFsDelCommand(ILogger<RdbFsDelCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, int fileSystemNumber)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.fileSystemNumber = fileSystemNumber;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Deleting file system from Rigid Disk Block at '{path}'");

            OnDebugMessage($"Opening '{path}' for read/write");

            var writableMediaResult = await commandHelper.GetWritableMedia(physicalDrives, path);
            if (writableMediaResult.IsFaulted)
            {
                return new Result(writableMediaResult.Error);
            }

            using var media = MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, writableMediaResult.Value, path);
            var stream = media.Stream;

            OnDebugMessage("Reading Rigid Disk Block");
            
            var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(media);

            if (rigidDiskBlock == null)
            {
                return new Result(new Error("Rigid Disk Block not found"));
            }

            var fileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks.ToList();

            OnDebugMessage($"Deleting file system number '{fileSystemNumber}'");
            
            if (fileSystemNumber < 1 || fileSystemNumber > fileSystemHeaderBlocks.Count)
            {
                return new Result(new Error($"Invalid file system number '{fileSystemNumber}'"));
            }

            var fileSystemHeaderBlock = fileSystemHeaderBlocks[fileSystemNumber - 1];
            
            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
            var partitionBlock =
                partitionBlocks.FirstOrDefault(x => x.DosType.SequenceEqual(fileSystemHeaderBlock.DosType)); 
            if (partitionBlock != null)
            {
                return new Result(new Error($"Partition number '{partitionBlocks.IndexOf(partitionBlock) + 1}' uses file system number '{fileSystemNumber}'"));
            }

            fileSystemHeaderBlocks.RemoveAt(fileSystemNumber - 1);
            rigidDiskBlock.FileSystemHeaderBlocks = fileSystemHeaderBlocks;
            
            OnDebugMessage("Writing Rigid Disk Block");
            await MediaHelper.WriteRigidDiskBlockToMedia(media, rigidDiskBlock);

            return new Result();
        }
    }
}