# FAT file system

The FAT file system was originally developed by Microsoft for the MS-DOS operating system.
It has since been adopted by various other operating systems and is widely used for removable storage devices such as USB flash drives and memory cards.

## Layout

The FAT file system consists of three main areas:
- Reserved area: Contains the boot sector and other reserved sectors.
- FAT area: File Allocation Table with cluster status and pointer to next cluster in chain.
- Data area: Contains the file and directory data.

```
|----------|--------------------------|-------------------------------------------------------------|
| Reserved |           FAT            |                            Data                             |
|   Area   |           Area           |                            Area                             |
|----------|--------------------------|-------------------------------------------------------------|
```

## Variants

The FAT file system comes in three main variants: FAT12, FAT16, and FAT32, each with different characteristics and limitations.

| FAT variant | FAT entry size | Max. number of clusters | Cluster sizes | Max. volume size  | Max. file size |
|-------------|----------------|-------------------------|---------------|-------------------|----------------|
| FAT12       | 12 bits        | 4,085                   | 512 B - 8 KB  | 32 MB             | 16 MB          |
| FAT16       | 16 bits        | 65,525                  | 512 B - 32 KB | 2 GB (up to 4 GB) | 2 GB           |
| FAT32       | 28 bits        | 268,435,445             | 512 B - 32 KB | 2 TB (up to 8 TB) | 4 GB           |

Maximum Volume Size = Number of Clusters * Cluster Size.

Maximum Volume Size = 65,536 clusters * 32KB = 2GB for FAT16.

FAT32 does not support partitions smaller than 512 MB.

Theoretically FAT32 can support volumes up to 16 TB with 64 KB cluster size, but MBR and Windows limits it to 2 TB.

The FAT type is determined by the number of clusters in the data area:
- FAT12: 0 - 4,085 clusters.
- FAT16: 4,086 - 65,525 clusters.
- FAT32: 65,526 - 268,435,445 clusters.

### FAT12 and FAT16

The FAT12 and FAT16 file systems has root directory located in the start of the data area:
```
|----------|--------------------------|-----------:-------------------------------------------------|
| Reserved |           FAT            |   Root    :                        Data                     |
|   Area   |           Area           | Directory :                        Area                     |
|----------|--------------------------|-----------:-------------------------------------------------|
```

The size of each area can be calculated in sectors using the following formulas:
- Reserved area: Number of reserved sectors.
- FAT Area: Number of FAT's * number of sectors per FAT.
- Root Directory: Number of root directory entries * size of each directory entry (32 bytes) / bytes per sector.
- Data Area: (total sectors - reserved sectors - fat sectors - root directory sectors).

### FAT32

The FAT32 file system is more flexible as the root directory located is specified in the boot sector:
```
|----------|--------------------------|---:-----------:---------------------------------------------|
| Reserved |           FAT            |   :   Root    :                      Data                   |
|   Area   |           Area           |   : Directory :                      Area                   |
|----------|--------------------------|---:-----------:---------------------------------------------|
```

The size of each area can be calculated in sectors using the following formulas:
- Reserved area: Number of reserved sectors * bytes per sector.
- FAT Area: Number of FAT's * number of sectors per FAT * bytes per sector.
- Data Area: (total sectors - reserved sectors - fat sectors).

## Boot sector

The initial bytes of the boot sector has the following structure:

| Offset | Size in bytes | Type         | Field name             | Typical value(s) | Description                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
|--------|---------------|--------------|------------------------|------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 0x00   | 3             | Byte         | Jump boot (BS_jmpBoot) | 0xEB3C90         | The first three bytes EB 3C 90 disassemble to JMP SHORT 3C NOP. (The 3C value may be different.) The reason for this is to jump over the disk format information (the BPB and EBPB). Since the first sector of the disk is loaded into ram at location 0x0000:0x7c00 and executed, without this jump, the processor would attempt to execute data that isn't code. Even for non-bootable volumes, code matching this pattern (or using the E9 jump opcode) is required to be present by both Windows and OS X. To fulfil this requirement, an infinite loop can be placed here with the bytes EB FE 90.                                                                  |
| 0x03   | 8             | ASCII String | OEM Name (BS_OEMName)     | MSWIN4.1         | OEM identifier. The first 8 Bytes (3 - 10) is the version of DOS being used. The next eight Bytes 29 3A 63 7E 2D 49 48 and 43 read out the name of the version. The official FAT Specification from Microsoft says that this field is really meaningless and is ignored by MS FAT Drivers, however it does recommend the value "MSWIN4.1" as some 3rd party drivers supposedly check it and expect it to have that value. Older versions of dos also report MSDOS5.1, linux-formatted floppy will likely to carry "mkdosfs" here, and FreeDOS formatted disks have been observed to have "FRDOS5.1" here. If the string is less than 8 bytes, it is padded with spaces.  |

### BIOS Parameter Block (BPB)

The BIOS Parameter Block (BPB) is a data structure embedded in the boot sector of a volume formatted with the File Allocation Table (FAT) file system, providing critical metadata about the volume's geometry, layout, and parameters necessary for operating systems and bootloaders to access and manage the storage medium.

Key fields in the BPB define essential attributes, such as BPB_BytsPerSec (bytes per sector, typically 512), BPB_SecPerClus (sectors per cluster, a power of 2 from 1 to 128), BPB_NumFATs (number of FAT copies, usually 2 for redundancy), and BPB_TotSec32 (total sectors on the volume for larger drives).

For FAT32, unique extensions like BPB_FATSz32 specify the FAT size in sectors, while BPB_RootClus indicates the starting cluster of the root directory, replacing the fixed root entry count used in earlier FAT versions.

The BIOS Parameter Block (BPB) has the following structure:

| Offset | Size in bytes | Type    | Field name                          | Typical value(s)                       | Description                                                                                                                                                            |
|--------|---------------|---------|-------------------------------------|----------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 0x0B   | 2             | UInt16  | Bytes per sector (BPB_BytsPerSec)   | 512                                    | Specifies the number of bytes in each sector, defining the smallest unit for read/write operations; valid values are 512, 1024, 2048, or 4096.                         |                    
| 0x0D   | 1             | Byte () | Sectors per cluster (BPB_SecPerClus)    | 1, 2, 4, 8 (powers of 2 from 1 to 128) | Indicates the number of sectors grouped into one allocation unit (cluster), optimizing storage efficiency by reducing FAT overhead for small files.                    | 
| 0x0E   | 2             | UInt16  | Reserved sectors (BPB_RsvdSecCnt)       | 1                                      | Counts the sectors reserved at the volume's beginning, typically including only the boot sector itself, before the FAT begins; must be at least 1.                     |
| 0x10   | 1             | Byte    | Number of FATs (BPB_NumFATs)            | 2                                      | Defines the count of FAT copies on the volume for redundancy and reliability during error recovery.                                                                    |
| 0x11   | 2             | UInt16  | Root directory entries (BPB_RootEntCnt) | 512 (for FAT12/FAT16)                  | Specifies the maximum number of 32-byte entries in the root directory; the directory size is this value multiplied by 32 bytes, with 0 used in some extended variants. |
| 0x13   | 2             | UInt16  | Total sectors (BPB_TotSec16)            | -                                      | Provides a 16-bit total sector count for small volumes; set to 0 if the 32-bit version is used instead.                                                                |
| 0x15   | 1             | Byte    | Media descriptor (BPB_Media)            | 0xF8 (hard disks), 0xF0 (floppies)     | Identifies the media type, influencing FAT caching and interrupt behaviors; common values include 0xF0–0xFF for different storage media.                               |
| 0x16   | 2             | UInt16  | Sectors per FAT (BPB_FATSz16)           | -                                      | Gives the 16-bit sector count per FAT; determines the FAT's size usually for FAT12/16 and is set to 0 when using the extended 32-bit version for FAT32.                |
| 0x18   | 2             | UInt16  | Sectors per track (BPB_SecPerTrk)       | 63 (common for hard disks)             | Describes the sectors per track in the BIOS geometry model, used for legacy interrupt 13h access to the disk.                                                          |
| 0x1A   | 2             | UInt16  | Number of heads (BPB_NumHeads)          | 16 or 255 (common for hard disks)      | Indicates the number of heads in the BIOS CHS addressing scheme for compatibility with early hardware.                                                                 |
| 0x1C   | 4             | UInt32  | Hidden sectors (BPB_HiddSec)            | -                                      | Counts the sectors hidden before the volume's start, such as those in preceding partitions or track 0; 0 for non-partitioned media like floppies.                      |
| 0x20   | 4             | UInt32  | Total sectors (BPB_TotSec32)            | -                                      | Provides a 32-bit total sector count for larger volumes; used when the 16-bit field is 0, providing support for capacities beyond 32 MB.                               |

### Extended Bios Parameter Block (EBPB)

The Extended BIOS Parameter Block (EBPB) extends the core BPB by adding optional fields beginning at byte offset 36 within the boot sector of a volume, providing support for advanced features such as volume identification, drive-specific metadata, and compatibility with larger storage media.

Introduced in DOS 3.4 in 1988, the EBPB adds 26 bytes for FAT12 and FAT16 volumes (extending the boot sector up to offset 61) and up to 54 bytes for FAT32 volumes (up to offset 89), enhancing volume portability by including unique identifiers that help operating systems distinguish between similar media and prevent accidental swaps of identical volumes.

The Extended Bios Parameter Block (EBPB) for FAT12/FAT16 has the following structure:

| Offset | Size in bytes | Type         | Field name                           | Typical value(s)                               | Description                                                                                                                                                                                         |
|--------|---------------|--------------|--------------------------------------|------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 0x24   | 1             | Byte         | Physical drive number (BS_DrvNum)    | 0x80 (first hard disk)                         | Indicates the physical drive, such as 0x80 for the first hard drive or 0x00 for a floppy; used by boot code to identify the boot device during interrupt 13h calls.                                 |
| 0x25   | 1             | Byte         | Reserved    (BS_Reserved1)           | 0                                              | Windows-specific field, functioning as a flag related to bootable partitions and volume integrity checks; typically set to 0x00 during volume creation.                                             |
| 0x26   | 1             | Byte         | Extended boot signature (BS_BootSig) | 0x29 (indicates presence of next three fields) | Indicates the presence of the following three fields (Volume Serial Number, Volume Label, and File System Type) when set to 0x29; if not set, these fields may be absent or contain arbitrary data. |
| 0x27   | 4             | UInt32       | Volume serial number (BS_VolID)      | -                                              | Unique identifier generated by combining the current timestamp (date and time of formatting) with a pseudo-random element to ensure volume uniqueness across systems.                               |
| 0x2B   | 11            | ASCII String | Volume label (BS_VolLab)             | "NO NAME    " (default)                        | ASCII string representing the volume name, padded with spaces if shorter than 11 characters; defaults to "NO NAME " if no label is specified, matching the root directory entry.                    |
| 0x36   | 8             | ASCII String | File system type (BS_FilSysType)     | "FAT12   ", "FAT16   " (for FAT12/FAT16)       | ASCII string identifying the file system, such as "FAT12 " or "FAT "; not used by Microsoft drivers to determine the actual FAT type but aids in compatibility.                                     |   

For FAT32 volumes, the EBPB incorporates additional fields starting at offset 36 to accommodate larger structures, shifting the common fields (drive number, reserved, signature, etc.) to offsets 64–89; the BPB_FATSz16 field must be set to 0 to indicate FAT32 usage.

The Extended Bios Parameter Block (EBPB) for FAT32 has the following structure:

| Offset | Size in bytes | Type         | Field name                           | Typical value(s)                               | Description                                                                                                                                                                                                                                     |
|--------|---------------|--------------|--------------------------------------|------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 0x24   | 4             | UInt32       | Sectors per FAT (BPB_FATSz32)        | -                                              | Provides the 32-bit sector count per FAT for FAT32 volumes, determining the FAT's size; this field is used instead of the 16-bit version found in FAT12/16.                                                                                     |
| 0x28   | 2             | UInt16       | FAT Flags (BPB_ExtFlags)             | 0                                              | Indicating the active FAT (for non-mirrored setups) and mirroring behavior; bit 7 set to 1 disables mirroring.                                                                                                                                  |
| 0x2A   | 2             | UInt16       | File System Version (BPB_FSVer)      | 0                                              | Indicating the file system version (high byte: major, low byte: minor; typically 0:0 for compatibility).                                                                                                                                        |
| 0x2C   | 4             | UInt32       | Root Cluster (BPB_RootClus)          | 2 (default)                                    | Specifies the starting cluster number of the root directory for FAT32 volumes; typically set to 2, meaning the root directory starts at the first cluster of the data area.                                                                     |
| 0x30   | 2             | UInt16       | FSInfo Sector (BPB_FSInfo)           | 1 (default)                                    | Indicates the sector number of the FSInfo structure for FAT32 volumes, which contains information about free clusters and the next free cluster; typically set to 1, meaning the FSInfo structure is located immediately after the boot sector. |
| 0x32   | 2             | UInt16       | Backup Boot Sector (BPB_BkBootSec)   | 6 (default)                                    | Specifies the sector number of the backup boot sector for FAT32 volumes, which serves as a backup copy of the boot sector; typically set to 6, meaning the backup boot sector is located at sector 6 of the volume.                             |     
| 0x34   | 12            | Byte         | Reserved (BPB_Reserved)              | -                                              | Reserved for future use; all bytes must be set to 0.                                                                                                                                                                                            |     
| 0x40   | 1             | Byte         | Physical drive number (BS_DrvNum)    | 0x80 (first hard disk)                         | This field has the same definition as it does for FAT12 and FAT16 media. The only difference for FAT32 media is that the field is at a different offset in the boot sector.                                                                     |
| 0x41   | 1             | Byte         | Reserved (BS_Reserved1)              | 0                                              | This field has the same definition as it does for FAT12 and FAT16 media. The only difference for FAT32 media is that the field is at a different offset in the boot sector.                                                                     |
| 0x42   | 1             | Byte         | Extended boot signature (BS_BootSig) | 0x29 (indicates presence of next three fields) | This field has the same definition as it does for FAT12 and FAT16 media. The only difference for FAT32 media is that the field is at a different offset in the boot sector.                                                                     |
| 0x43   | 4             | UInt32       | Volume serial number (BS_VolID)      | -                                              | This field has the same definition as it does for FAT12 and FAT16 media. The only difference for FAT32 media is that the field is at a different offset in the boot sector.                                                                     |
| 0x47   | 11            | ASCII String | Volume label (BS_VolLab)             | "NO NAME    " (default)                        | This field has the same definition as it does for FAT12 and FAT16 media. The only difference for FAT32 media is that the field is at a different offset in the boot sector.                                                                     |
| 0x52   | 8             | ASCII String | File system type (BS_FilSysType)     | "FAT32   " (for FAT32)                         | This field has the same definition as it does for FAT12 and FAT16 media. The only difference for FAT32 media is that the field is at a different offset in the boot sector.                                                                     |  

## Calculations

Number of sectors in FAT area is determined by the following formula:
```
If(BPB_FATSz16 != 0)
    FATSz = BPB_FATSz16;
Else
    FATSz = BPB_FATSz32;
```

First data sector of the volume is calculated as follows:
```
FirstDataSector = BPB_ResvdSecCnt + (BPB_NumFATs * FATSz) + RootDirSectors;
```

First sector of cluster N is calculated as follows:
```
FirstSectorofCluster = ((N – 2) * BPB_SecPerClus) + FirstDataSector;
```

Total number of sectors in the volume is calculated as follows:
```
If(BPB_TotSec16 != 0)
    TotSec = BPB_TotSec16;
Else
    TotSec = BPB_TotSec32;
```

Number of data sectors is calculated as follows:
```
DataSec = TotSec – (BPB_ResvdSecCnt + (BPB_NumFATs * FATSz) + RootDirSectors);
```

Count of clusters in the data region is calculated as follows (computation rounds down):

```
CountofClusters = DataSec / BPB_SecPerClus;
```

FAT type is determined by the number of clusters in the data area:

```
If(CountofClusters < 4085) {
    /* Volume is FAT12 */
} else if(CountofClusters < 65525) {
    /* Volume is FAT16 */
} else {
    /* Volume is FAT32 */
}
```

