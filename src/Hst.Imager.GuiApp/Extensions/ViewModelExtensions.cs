namespace Hst.Imager.GuiApp.Extensions
{
    using System.Linq;
    using Hst.Amiga.RigidDiskBlocks;
    using Hst.Imager.Core.Commands;
    using Models;

    public static class ViewModelExtensions
    {
        public static MediaInfoViewModel ToViewModel(this MediaInfo mediaInfo)
        {
            return new MediaInfoViewModel
            {
                Path = mediaInfo.Path,
                Name = mediaInfo.Name,
                DiskSize = mediaInfo.DiskSize,
                IsPhysicalDrive = mediaInfo.IsPhysicalDrive,
                DiskInfo = mediaInfo.DiskInfo?.ToViewModel()
            };
        }

        public static PartitionTablePartViewModel ToViewModel(this PartitionTablePart partitionTablePart)
        {
            return new PartitionTablePartViewModel
            {
                Path = partitionTablePart.Path,
                PartitionTableType = partitionTablePart.PartitionTableType,
                Size = partitionTablePart.Size,
                Sectors = partitionTablePart.Sectors,
                Cylinders = partitionTablePart.Cylinders,
                Parts = partitionTablePart.Parts.Select(x => x.ToViewModel()).ToList()
            };
        }

        public static PartViewModel ToViewModel(this PartInfo partInfo)
        {
            return new PartViewModel
            {
                FileSystem = partInfo.FileSystem,
                PartitionNumber = partInfo.PartitionNumber,
                PartitionTableType = partInfo.PartitionTableType,
                PartType = partInfo.PartType,
                Size = partInfo.Size,
                StartOffset = partInfo.StartOffset,
                EndOffset = partInfo.EndOffset,
                StartSector = partInfo.StartSector,
                EndSector = partInfo.EndSector,
                StartCylinder = partInfo.StartCylinder,
                EndCylinder = partInfo.EndCylinder,
                PercentSize = partInfo.PercentSize
            };
        }

        public static DiskInfoViewModel ToViewModel(this DiskInfo diskInfo)
        {
            return new DiskInfoViewModel
            {
                Name = diskInfo.Name,
                Size = diskInfo.Size,
                PartitionTables = diskInfo.PartitionTables,
                StartOffset = diskInfo.StartOffset,
                EndOffset = diskInfo.EndOffset,
                Path = diskInfo.Path,
                RigidDiskBlock = diskInfo.RigidDiskBlock?.ToViewModel(),
                DiskParts = diskInfo.DiskParts.Select(x => x.ToViewModel()).ToList(),
                GptPartitionTablePart = diskInfo.GptPartitionTablePart?.ToViewModel(),
                MbrPartitionTablePart = diskInfo.MbrPartitionTablePart?.ToViewModel(),
                RdbPartitionTablePart = diskInfo.RdbPartitionTablePart?.ToViewModel()
            };
        }

        public static RigidDiskBlockViewModel ToViewModel(this RigidDiskBlock rigidDiskBlock)
        {
            return new RigidDiskBlockViewModel
            {
                BlockSize = rigidDiskBlock.BlockSize,
                ControllerProduct = rigidDiskBlock.ControllerProduct,
                ControllerRevision = rigidDiskBlock.ControllerRevision,
                ControllerVendor = rigidDiskBlock.ControllerVendor,
                CylBlocks = rigidDiskBlock.CylBlocks,
                Cylinders = rigidDiskBlock.Cylinders,
                DiskProduct = rigidDiskBlock.DiskProduct,
                DiskRevision = rigidDiskBlock.DiskRevision,
                DiskVendor = rigidDiskBlock.DiskVendor,
                DiskSize = rigidDiskBlock.DiskSize,
                Heads = rigidDiskBlock.Heads,
                Sectors = rigidDiskBlock.Sectors,
                HiCylinder = rigidDiskBlock.HiCylinder,
                LoCylinder = rigidDiskBlock.LoCylinder,
                ParkingZone = rigidDiskBlock.ParkingZone,
                RdbBlockHi = rigidDiskBlock.RdbBlockHi,
                RdbBlockLo = rigidDiskBlock.RdbBlockLo,
                PartitionBlocks = rigidDiskBlock.PartitionBlocks.Select(x => x.ToViewModel()).ToList(),
                FileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks.Select(x => x.ToViewModel()).ToList()
            };
        }

        public static PartitionBlockViewModel ToViewModel(this PartitionBlock partitionBlock)
        {
            return new PartitionBlockViewModel
            {
                Bootable = partitionBlock.Bootable,
                Mask = partitionBlock.Mask,
                DosType = partitionBlock.DosType,
                DosTypeFormatted = partitionBlock.DosTypeFormatted,
                DosTypeHex = partitionBlock.DosTypeHex,
                Reserved = partitionBlock.Reserved,
                BlocksPerTrack = partitionBlock.BlocksPerTrack,
                BootPriority = partitionBlock.BootPriority,
                Sectors = partitionBlock.Sectors,
                Surfaces = partitionBlock.Surfaces,
                DriveName = partitionBlock.DriveName,
                HighCyl = partitionBlock.HighCyl,
                LowCyl = partitionBlock.LowCyl,
                MaskHex = partitionBlock.MaskHex,
                MaxTransfer = partitionBlock.MaxTransfer,
                MaxTransferHex = partitionBlock.MaxTransferHex,
                NoMount = partitionBlock.NoMount,
                NumBuffer = partitionBlock.NumBuffer,
                PartitionSize = partitionBlock.PartitionSize,
                PreAlloc = partitionBlock.PreAlloc,
                SizeBlock = partitionBlock.SizeBlock,
                SizeOfVector = partitionBlock.SizeOfVector,
                FileSystemBlockSize = partitionBlock.FileSystemBlockSize
            };
        }

        public static FileSystemHeaderBlockViewModel ToViewModel(this FileSystemHeaderBlock fileSystemHeaderBlock)
        {
            return new FileSystemHeaderBlockViewModel
            {
                Size = fileSystemHeaderBlock.LoadSegBlocks.Sum(x => x.Data.Length),
                DosType = fileSystemHeaderBlock.DosType,
                DosTypeFormatted = fileSystemHeaderBlock.DosTypeFormatted,
                DosTypeHex = fileSystemHeaderBlock.DosTypeHex,
                Version = fileSystemHeaderBlock.Version,
                Revision = fileSystemHeaderBlock.Revision,
                VersionFormatted = fileSystemHeaderBlock.VersionFormatted,
                FileSystemName = fileSystemHeaderBlock.FileSystemName
            };
        }
    }
}