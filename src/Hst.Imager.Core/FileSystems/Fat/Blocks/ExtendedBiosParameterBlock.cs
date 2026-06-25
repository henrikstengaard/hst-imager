namespace Hst.Imager.Core.FileSystems.Fat.Blocks;

/// <summary>
/// Extended bios parameter block.
/// </summary>
/// <param name="FatSectorsFat32">[FAT32] Number of sectors used by one FAT.</param>
/// <param name="ExtFlags">[FAT32] Extended flags.</param>
/// <param name="FileSystemVersion">[FAT32] File system version for FAT32 volume.</param>
/// <param name="RootCluster">[FAT32] Root cluster with first cluster of the root directory. Usually 2.</param>
/// <param name="FsInfo">[FAT32] Sector number of FsInfo in reserved area. Usually 1.</param>
/// <param name="BackupBootSector">[FAT32] Sector number of backup boot sector in reserved area. Usually 6.</param>
/// <param name="Reserved">[FAT32] Reserved for future expansion. Should be set to zeroes.</param>
/// <param name="DriveNumber">Drive number for interrupt 0x13.</param>
/// <param name="Reserved1">Reserved (used by Windows NT). Must be zero for code formatting FAT volumes.</param>
/// <param name="BootSignature">Extended boot signature. Value 0x29 indicates the following fields volume id, volume label and volume serial number are present.</param>
/// <param name="VolumeId">Volume serial number.</param>
/// <param name="VolumeLabel">Volume label.</param>
/// <param name="FileSystemType">File system type. This string is only informational and is not used to determine FAT type.</param>
public record ExtendedBiosParameterBlock(uint FatSectorsFat32, ushort ExtFlags, ushort FileSystemVersion,
    uint RootCluster, ushort FsInfo, ushort BackupBootSector, byte[] Reserved, byte DriveNumber, byte Reserved1,
    byte BootSignature, uint VolumeId, byte[] VolumeLabel, byte[] FileSystemType);