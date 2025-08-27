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
        private readonly string fileSystem;

        public MbrPartFormatCommand(ILogger<MbrPartFormatCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, int partitionNumber, string name, string fileSystem)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.partitionNumber = partitionNumber;
            this.name = name;
            this.fileSystem = fileSystem;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            MbrPartType parsedType = MbrPartType.Fat32;

            if (!string.IsNullOrWhiteSpace(fileSystem) &&
                !Enum.TryParse<MbrPartType>(fileSystem, true, out parsedType))
            {
                return new Result(new Error($"Unsupported file system '{fileSystem}'"));
            }

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

            var fileSystemFromBiosTypeResult = GetFileSystemFromBiosType(partitionInfo.BiosType);
            if (fileSystemFromBiosTypeResult.IsFaulted)
            {
                return new Result(fileSystemFromBiosTypeResult.Error);
            }

            var partitionFileSystem = !string.IsNullOrWhiteSpace(fileSystem)
                ? parsedType : fileSystemFromBiosTypeResult.Value;

            OnInformationMessage($"- Type '{partitionInfo.TypeAsString}'");
            OnInformationMessage($"- File system '{partitionFileSystem}'");
            OnInformationMessage($"- Partition name '{name}'");

            // format mbr partition
            switch (partitionFileSystem)
            {
                case MbrPartType.Fat12:
                case MbrPartType.Fat16:
                case MbrPartType.Fat16Small:
                case MbrPartType.Fat16Lba:
                    FatFileSystem.FormatPartition(disk, partitionNumber - 1, name);
                    break;
                case MbrPartType.Fat32:
                case MbrPartType.Fat32Lba:
                    var partitionOffset = partitionInfo.FirstSector * disk.Geometry.Value.BytesPerSector;
                    await Fat32Formatter.FormatPartition(disk.Content, partitionOffset,
                        partitionInfo.SectorCount * disk.Geometry.Value.BytesPerSector,
                        disk.Geometry.Value.BytesPerSector, disk.Geometry.Value.SectorsPerTrack, disk.Geometry.Value.HeadsPerCylinder,
                        name);
                    break;
                case MbrPartType.Ntfs:
                    NtfsFileSystem.Format(partitionInfo.Open(), name, Geometry.FromCapacity(partitionInfo.SectorCount * 512),
                        partitionInfo.FirstSector, partitionInfo.SectorCount);
                    break;
                case MbrPartType.ExFat:
                    ExFat.Filesystem.ExFatEntryFilesystem.Format(partitionInfo.Open(), new ExFat.ExFatFormatOptions(), name);
                    break;
                default:
                    return new Result(new Error("Unsupported partition type"));
            }

            // flush disk content
            await disk.Content.FlushAsync(token);

            if (media.IsPhysicalDrive)
            {
                await commandHelper.RescanPhysicalDrives();
            }
            
            return new Result();
        }

        private static Result<MbrPartType> GetFileSystemFromBiosType(byte biosType)
        {
            switch (biosType)
            {
                case BiosPartitionTypes.Fat12:
                    return new Result<MbrPartType>(MbrPartType.Fat12);
                case BiosPartitionTypes.Fat16:
                    return new Result<MbrPartType>(MbrPartType.Fat16);
                case BiosPartitionTypes.Fat16Small:
                    return new Result<MbrPartType>(MbrPartType.Fat16Small);
                case BiosPartitionTypes.Fat16Lba:
                    return new Result<MbrPartType>(MbrPartType.Fat16Lba);
                case BiosPartitionTypes.Fat32:
                    return new Result<MbrPartType>(MbrPartType.Fat32);
                case BiosPartitionTypes.Fat32Lba:
                    return new Result<MbrPartType>(MbrPartType.Fat32Lba);
                case BiosPartitionTypes.Ntfs:
                    return new Result<MbrPartType>(MbrPartType.Ntfs);
                default:
                    return new Result<MbrPartType>(new Error($"Unsupported partition type '{biosType}'"));
            }
        }
    }
}