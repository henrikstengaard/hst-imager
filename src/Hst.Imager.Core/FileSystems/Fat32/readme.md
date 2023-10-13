# Fat32 formatter

FAT32 directory contains classes to format partitions with FAT32 file system.

## Blocks

FAT32 uses following blocks.

### FAT32 Boot Record Information

This information is located in the first sector of every partition.

| Offset | Description                                                                                                                                                                                                  | Size          |
|--------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------|
| 00     | Jump Code + NOP                                                                                                                                                                                              | 3 Bytes       |
| 03     | OEM Name (Probably MSWIN4.1)                                                                                                                                                                                 | 8 Bytes       |
| 0B     | Bytes Per Sector                                                                                                                                                                                             | 1 Word        |
| 0D     | Sectors Per Cluster                                                                                                                                                                                          | 1 Byte        |
| 0E     | Reserved Sectors                                                                                                                                                                                             | 1 Word        |
| 10     | Number of Copies of FAT                                                                                                                                                                                      | 1 Byte        |
| 11     | Maximum Root DirectoryEntries (N/A for FAT32)                                                                                                                                                                | 1 Word        |
| 13     | Number of Sectors in Partition Smaller than 32MB (N/A for FAT32)                                                                                                                                             | 1 Word        |
| 15     | Media Descriptor (F8h forHard Disks)                                                                                                                                                                         | 1 Byte        |
| 16     | Sectors Per FAT in Older FATSystems (N/A for FAT32)                                                                                                                                                          | 1 Word        |
| 18     | Sectors Per Track                                                                                                                                                                                            | 1 Word        |
| 1A     | Number of Heads                                                                                                                                                                                              | 1 Word        |
| 1C     | Number of Hidden Sectors inPartition                                                                                                                                                                         | 1 Double Word |
| 20     | Number of Sectors inPartition                                                                                                                                                                                | 1 Double Word |
| 24     | Number of Sectors Per FAT                                                                                                                                                                                    | 1 Double Word |
| 28     | Flags (Bits 0-4 IndicateActive FAT Copy) (Bit 7 Indicates whether FAT Mirroring is Enabled or Disabled) (If FATMirroring is Disabled, the FAT Information is only written to the copy indicated by bits 0-4) | 1 Word        |
| 2A     | Version of FAT32 Drive (HighByte = Major Version, Low Byte = Minor Version)                                                                                                                                  | 1 Word        |
| 2C     | Cluster Number of the Start of the Root Directory                                                                                                                                                            | 1 Double Word |
| 30     | Sector Number of the FileSystem Information Sector (See Structure Below)(Referenced from the Start of the Partition)                                                                                         | 1 Word        |
| 32     | Sector Number of the BackupBoot Sector (Referenced from the Start of the Partition)                                                                                                                          | 1 Word        |
| 34     | Reserved                                                                                                                                                                                                     | 12 Bytes      |
| 40     | Logical Drive Number ofPartition                                                                                                                                                                             | 1 Byte        |
| 41     | Unused (Could be High Byte of Previous Entry)                                                                                                                                                                | 1 Byte        |
| 42     | Extended Signature (29h)                                                                                                                                                                                     | 1 Byte        |
| 43     | Serial Number of Partition                                                                                                                                                                                   | 1 Double Word |
| 47     | Volume Name of Partition                                                                                                                                                                                     | 11 Bytes      |
| 52     | FAT Name (FAT32)                                                                                                                                                                                             | 8 Bytes       |
| 5A     | Executable Code                                                                                                                                                                                              | 420 Bytes     |
| 1FE    | Boot Record Signature (55hAAh)                                                                                                                                                                               | 2 Bytes       |

Word = 2 bytes

Double Word = 4 bytes

### File System Information Sector instruction

Usually, this exists a Second Sector of the partition, although since there is a reference in the Boot Sector to it. I'm assuming it can be moved around. I never got a complete picture of this one. Although I do know where the important fields are at.

| Offset | Description                                                | Size          |
|--------|------------------------------------------------------------|---------------|
| 00     | First Signature (52h 52h 61h 41h)                          | 1 Double Word |
| 04     | Unknown, Currently (Mightjust be Null)                     | 480 Bytes     |
| 1E4    | Signature of FSInfo Sector(72h 72h 41h 61h)                | 1 Double Word |
| 1E8    | Number of Free Clusters (Setto -1 if Unknown)              | 1 Double Word |
| 1EC    | Cluster Number of Clusterthat was Most Recently Allocated. | 1 Double Word |
| 1F0    | Reserved                                                   | 12 Bytes      |
| 1FC    | Unknown or Null                                            | 2 Bytes       |
| 1FE    | Boot Record Signature (55hAAh)                             | 2 Bytes       |

## FAT32 partition structure

        // Write boot sector, fats
        // Sector 0 Boot Sector
        // Sector 1 FSInfo 
        // Sector 2 More boot code - we write zeros here
        // Sector 3 unused
        // Sector 4 unused
        // Sector 5 unused
        // Sector 6 Backup boot sector
        // Sector 7 Backup FSInfo sector
        // Sector 8 Backup 'more boot code'
        // zero'd sectors upto ReservedSectCount
        // FAT1  ReservedSectCount to ReservedSectCount + FatSize
        // ...
        // FATn  ReservedSectCount to ReservedSectCount + FatSize
        // RootDir - allocated to cluster2

Bytes per sector = 512.

Reserved sectors = 32.

Number of fats = 2.

Fat element size = 4 bytes.

Cluster size = 4096 bytes.

Sectors per cluster = cluster size / bytes per sector, E.g. 4096 / 512 = 8.

Sector count = disk size in bytes / bytes per sector. E.g. 262144000 / 512 = 512000.

Fat size = (fat element size * (sector count - reserved sectors)) / ((sectors per cluster * bytes per sector) + (fat element size * number of fats)). E.g. (4 * (512000 - 32)) / ((8 * 512) + (4 * 2)) = 499

Cluster 0 and 1 are reserved for FAT32.

Cluster 2 until end of partition are data area.

| Sector                                                     | Description                                 |
|------------------------------------------------------------|---------------------------------------------|
| 0                                                          | Boot sector                                 |
| 1                                                          | Filesystem info                             |
| 2                                                          | Boot code, if present. Otherwise zero bytes |
| 3 - 5                                                      | Unused, zero bytes                          |
| 6                                                          | Backup boot sector                          |
| 7                                                          | Backup filesystem info                      |
| 8                                                          | Backup boot code, if present. Otherwise zero bytes                            |
| 9 - reserved sectors                                       | Unused, zero bytes                                       |
| Reserved sectors - (Reserved sectors + Fat Size)           | Fat Tables                                  |
| Reserved sectors + (# of Sectors Per FAT * number of fats) | Data Area (Starts with Cluster #2)          |

## Cluster Meaning

A Cluster is a Group of Sectors on the Hard Drive that have information in them. A 4K Cluster has 8 Sectors in it (512*8=4096). Each Cluster is given a spot in the FAT Table. When you look at an Entry in the FAT, the number there tells you whether or not that cluster has data in it, and if so, if it is the end of the data or there is another cluster after it. All Data on a Partition starts with Cluster #2. If the FAT Entry is 0, then there is no data in that cluster. If the FAT Entry is 0FFFFFFFh, then it is the last entry in the chain.

Calculate the maximum valid cluster in a partition with this formula:

( (# of Sectors in Partition) - (# of Sectors per Fat * 2) - (# of Reserved Sectors) ) / (# of Sectors per Cluster)

If there is any remainder in the answer to that formula, it just means that there were a few extra clusters at the end of the partition (probably not enough to make another cluster), so you can just get rid of anything after the decimal point.

## Directory Table

Another aspect when looking at a File System at Low Level is the Directory Table. The Directory Table is what stores all of the File and Directory Entries. Basically, there is only one difference between the Directory Table of FAT16 and FAT32. The Difference is: the Reserved OS/2 Byte (Offset 20 [14h]) in the Short Filename Structure is replaced with the High Word of the Cluster Number (since it's now 4 bytes instead of 2).

## File Allocation Table

Footnotes

1 - LBA = Logical Block Addressing - Uses the Int 13h Extensions built into newer BIOS's to access data above the 8GB barrier, or to access strickly in LBA mode, instead of CHS (Cylinder, Head, Sector)
