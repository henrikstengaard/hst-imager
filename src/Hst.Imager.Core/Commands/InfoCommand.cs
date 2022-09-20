namespace HstWbInstaller.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Core;
    using Microsoft.Extensions.Logging;

    public class InfoCommand : CommandBase
    {
        private readonly ILogger<InfoCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;

        public InfoCommand(ILogger<InfoCommand> logger, ICommandHelper commandHelper, IEnumerable<IPhysicalDrive> physicalDrives, string path)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
        }

        public event EventHandler<InfoReadEventArgs> DiskInfoRead;
        
        public override async Task<Result> Execute(CancellationToken token)
        {
            var sourceMediaResult = commandHelper.GetReadableMedia(physicalDrives, path);
            if (sourceMediaResult.IsFaulted)
            {
                return new Result(sourceMediaResult.Error);
            }
            using var media = sourceMediaResult.Value;
            await using var stream = media.Stream;

            var diskInfo = await commandHelper.ReadDiskInfo(media, stream);
            
            // using var disk = new Disk(stream, Ownership.None);
            //
            // OnProgressMessage("Reading Master Boot Record");
            //
            // BiosPartitionTable biosPartitionTable = null;
            // try
            // {
            //     biosPartitionTable = new BiosPartitionTable(disk);
            // }
            // catch (Exception)
            // {
            //     // ignored, if read master boot record fails
            // }
            //
            // OnProgressMessage("Reading Rigid Disk Block");
            //
            // RigidDiskBlock rigidDiskBlock = null;
            // try
            // {
            //     rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);
            // }
            // catch (Exception e)
            // {
            //     Console.WriteLine(e);
            //     // ignored, if read rigid disk block fails
            // }
            //
            // var partitionTables = new List<PartitionTableInfo>();
            //
            // if (biosPartitionTable != null)
            // {
            //     var mbrPartitionNumber = 0;
            //     
            //     partitionTables.Add(new PartitionTableInfo
            //     {
            //         Type = PartitionTableInfo.PartitionTableType.MasterBootRecord,
            //         Size = disk.Capacity,
            //         Partitions = biosPartitionTable.Partitions.Select(x => new PartitionInfo
            //         {
            //             PartitionNumber = ++mbrPartitionNumber,
            //             Type = x.TypeAsString,
            //             Size = x.SectorCount * disk.BlockSize,
            //             StartOffset = x.FirstSector * disk.BlockSize,
            //             EndOffset = ((x.LastSector + 1) * disk.BlockSize) - 1
            //         }).ToList(),
            //         StartOffset = 0,
            //         EndOffset = 511
            //     });
            // }
            //
            // if (rigidDiskBlock != null)
            // {
            //     var cylinderSize = rigidDiskBlock.Heads * rigidDiskBlock.Sectors * rigidDiskBlock.BlockSize;
            //     var rdbPartitionNumber = 0;
            //     partitionTables.Add(new PartitionTableInfo
            //     {
            //         Type = PartitionTableInfo.PartitionTableType.RigidDiskBlock,
            //         Size = rigidDiskBlock.DiskSize,
            //         Partitions = rigidDiskBlock.PartitionBlocks.Select(x => new PartitionInfo
            //         {
            //             PartitionNumber = ++rdbPartitionNumber,
            //             Type = x.DosTypeFormatted,
            //             Size = x.PartitionSize,
            //             StartOffset = (long)x.LowCyl * cylinderSize,
            //             EndOffset = ((long)x.HighCyl + 1) * cylinderSize - 1
            //         }).ToList(),
            //         StartOffset = rigidDiskBlock.RdbBlockLo * rigidDiskBlock.BlockSize,
            //         EndOffset = ((rigidDiskBlock.RdbBlockHi + 1) * rigidDiskBlock.BlockSize) - 1
            //     });
            // }
            
            logger.LogDebug($"Physical drive size '{media.Size}'");
                
            var streamSize = stream.Length;
            logger.LogDebug($"Stream size '{streamSize}'");
            
            var diskSize = streamSize is > 0 ? streamSize : media.Size;
            
            logger.LogDebug($"Path '{path}', disk size '{diskSize}'");
            
            OnDiskInfoRead(new MediaInfo
            {
                Path = path,
                Model = media.Model,
                IsPhysicalDrive = media.IsPhysicalDrive,
                Type = media.Type,
                DiskSize = diskSize,
                DiskInfo = diskInfo
            });

            return new Result();
        }

        protected virtual void OnDiskInfoRead(MediaInfo mediaInfo)
        {
            DiskInfoRead?.Invoke(this, new InfoReadEventArgs(mediaInfo, mediaInfo.DiskInfo));
        }
    }
}