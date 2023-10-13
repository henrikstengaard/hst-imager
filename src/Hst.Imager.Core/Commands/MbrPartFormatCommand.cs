using DiscUtils;
using DiscUtils.Ntfs;
using Hst.Imager.Core.FileSystems.Fat32;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DiscUtils.Fat;
    using DiscUtils.Partitions;
    using DiscUtils.Raw;
    using DiscUtils.Streams;
    using Hst.Core;
    using Microsoft.Extensions.Logging;

    public class MbrPartFormatCommand : CommandBase
    {
        private readonly ILogger<MbrPartFormatCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly int partitionNumber;
        private readonly string name;

        public MbrPartFormatCommand(ILogger<MbrPartFormatCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber, string name)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.partitionNumber = partitionNumber;
            this.name = name;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Formatting partition in Master Boot Record at '{path}'");

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

            OnInformationMessage($"- Partition number '{partitionNumber}'");

            if (partitionNumber < 1 || partitionNumber > biosPartitionTable.Partitions.Count)
            {
                return new Result(new Error($"Invalid partition number '{partitionNumber}'"));
            }

            var partitionInfo = biosPartitionTable.Partitions[partitionNumber - 1];
            
            OnInformationMessage($"- Type '{partitionInfo.TypeAsString}'");
            OnInformationMessage($"- Partition name '{name}'");

            // format mbr partition
            switch (partitionInfo.BiosType)
            {
                case BiosPartitionTypes.Fat12:
                case BiosPartitionTypes.Fat16:
                case BiosPartitionTypes.Fat16Small:
                case BiosPartitionTypes.Fat16Lba:
                    FatFileSystem.FormatPartition(disk, partitionNumber - 1, name);
                    break;
                case BiosPartitionTypes.Fat32:
                case BiosPartitionTypes.Fat32Lba:
                    var partitionOffset = partitionInfo.FirstSector * disk.Geometry.BytesPerSector;
                    await Fat32Formatter.FormatPartition(disk.Content, partitionOffset,
                        partitionInfo.SectorCount * disk.Geometry.BytesPerSector,
                        disk.Geometry.BytesPerSector, disk.Geometry.SectorsPerTrack, disk.Geometry.HeadsPerCylinder, 
                        name);
                    break;
                case BiosPartitionTypes.Ntfs:
                    var partition = disk.Partitions.Partitions[partitionNumber - 1];
                    NtfsFileSystem.Format(partition.Open(), name, Geometry.FromCapacity(partition.SectorCount * 512), 
                        partition.FirstSector, partition.SectorCount);
                    break;
                default:
                    return new Result(new Error("Unsupported partition type"));
            }

            // flush disk content
            await disk.Content.FlushAsync(token);

            return new Result();
        }
    }
}