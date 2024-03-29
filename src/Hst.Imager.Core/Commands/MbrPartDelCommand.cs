﻿using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DiscUtils.Partitions;
    using DiscUtils.Raw;
    using DiscUtils.Streams;
    using Hst.Core;
    using Microsoft.Extensions.Logging;

    public class MbrPartDelCommand : CommandBase
    {
        private readonly ILogger<MbrPartDelCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly int partitionNumber;

        public MbrPartDelCommand(ILogger<MbrPartDelCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.partitionNumber = partitionNumber;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Deleting partition from Master Boot Record at '{path}'");
            
            OnDebugMessage($"Opening '{path}' as writable");

            var physicalDrivesList = physicalDrives.ToList();
            var mediaResult = await commandHelper.GetWritableMedia(physicalDrivesList, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }
            using var media = mediaResult.Value;
            
            var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new Disk(media.Stream, Ownership.None);
            
            OnDebugMessage("Reading Master Boot Record");
            
            BiosPartitionTable biosPartitionTable;
            try
            {
                biosPartitionTable = new BiosPartitionTable(disk);
            }
            catch (Exception)
            {
                return new Result(new Error("Master Boot Record not found"));
            }

            OnDebugMessage($"Deleting partition number '{partitionNumber}'");
            
            if (partitionNumber < 1 || partitionNumber > biosPartitionTable.Partitions.Count)
            {
                return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
            }
            
            // delete mbr partition
            biosPartitionTable.Delete(partitionNumber - 1);
            
            return new Result();
        }
    }
}