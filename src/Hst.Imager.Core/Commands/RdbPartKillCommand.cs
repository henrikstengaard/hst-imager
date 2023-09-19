namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Core;
    using Hst.Core.Extensions;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Kill Rigid disk block partition
    /// Reference: http://eab.abime.net/showthread.php?t=69128
    /// </summary>
    public class RdbPartKillCommand : CommandBase
    {
        private readonly ILogger<RdbPartKillCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly int partitionNumber;
        private readonly string hexBootBytes;

        public RdbPartKillCommand(ILogger<RdbPartKillCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber, string hexBootBytes)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.partitionNumber = partitionNumber;
            this.hexBootBytes = hexBootBytes;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            if (!string.IsNullOrEmpty(hexBootBytes) && hexBootBytes.Length != 8)
            {
                return new Result(new Error($"Hex boot bytes must be 8 characters {hexBootBytes}"));
            }

            OnInformationMessage($"Killing partition from Rigid Disk Block at '{path}'");
            
            OnDebugMessage($"Opening '{path}' as readable");

            var mediaResult = await commandHelper.GetWritableMedia(physicalDrives, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            using var media = mediaResult.Value;
            var stream = media.Stream;

            OnDebugMessage("Reading Rigid Disk Block");
            
            var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);

            if (rigidDiskBlock == null)
            {
                return new Result(new Error("Rigid Disk Block not found"));
            }

            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();

            OnDebugMessage($"Killing partition number '{partitionNumber}'");
            
            if (partitionNumber < 1 || partitionNumber > partitionBlocks.Count)
            {
                return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
            }
            
            // get partition number
            var partitionBlock = partitionBlocks[partitionNumber - 1];
            
            // calculate partition offset
            var cylinderSize = rigidDiskBlock.Heads * rigidDiskBlock.Sectors *
                                     rigidDiskBlock.BlockSize;
            var partitionOffset = (long)partitionBlock.LowCyl * cylinderSize;

            // seek partition offset
            stream.Seek(partitionOffset, SeekOrigin.Begin);

            // read block
            var blockBytes = await Amiga.Disk.ReadBlock(stream, 512);
            
            OnInformationMessage($"Current boot bytes '0x{blockBytes.Take(4).ToArray().FormatHex()}'");

            if (string.IsNullOrEmpty(hexBootBytes))
            {
                return new Result();
            }
            
            var bootBytes = Convert.FromHexString(hexBootBytes);
            
            for (var i = 0; i < 4; i++)
            {
                blockBytes[i] = bootBytes[i];
            }

            OnInformationMessage($"Writing boot bytes '0x{bootBytes.FormatHex()}'");
            
            // seek partition offset
            stream.Seek(partitionOffset, SeekOrigin.Begin);
            
            // write block
            await Amiga.Disk.WriteBlock(stream, blockBytes);
            
            return new Result();
        }
    }
}