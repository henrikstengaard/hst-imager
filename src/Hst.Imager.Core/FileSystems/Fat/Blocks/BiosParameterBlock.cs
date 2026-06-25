namespace Hst.Imager.Core.FileSystems.Fat.Blocks;

/// <summary>
/// Bios parameter block.
/// </summary>
/// <param name="JmpBoot">Jump boot.</param>
/// <param name="OemName">OEM name. Usually name of the tool that was used to build the filesystem.</param>
/// <param name="BytesPerSector">Bytes per sector.</param>
/// <param name="SectorsPerCluster">Sectors per cluster.</param>
/// <param name="ReservedSectorCount">Reserved sector count.</param>
/// <param name="NumberOfFats"></param>
/// <param name="RootEntriesCount"></param>
/// <param name="TotalSectors">Total sectors for FAT12/FAT16. Must be zero for FAT32.</param>
/// <param name="Media"></param>
/// <param name="FatSectors">Number of sectors used by one FAT. Must be zero for FAT32.</param>
/// <param name="SectorsPerTrack">Sectors per track for interrupt 0x13.</param>
/// <param name="NumberOfHeads">Number of heads for interrupt 0x13.</param>
/// <param name="HiddenSectors">Number of hidden sectors preceding the partition that contains this FAT volume.</param>
/// <param name="TotalSectorsFat32">Total sectors for FAT32. Must be zero for FAT12/FAT16.</param>
public record BiosParameterBlock(byte[] JmpBoot, byte[] OemName, ushort BytesPerSector, byte SectorsPerCluster,
    ushort ReservedSectorCount, byte NumberOfFats, ushort RootEntriesCount, ushort TotalSectors, byte Media,
    ushort FatSectors, ushort SectorsPerTrack, ushort NumberOfHeads, uint HiddenSectors, uint TotalSectorsFat32);