# Release notes

## v2.0

Hst Imager Gui:
- Add compact and examine file systems options to optimize page.
- Show original file size, optimized or compacted file size and percent reduced when optimize completes.
- Change size units in blank, format, optimize and media selection to binary units (1 GB = 1024 MB) like console app and sizes displayed.
- Show bytes processed and elapsed time without total size and ETA when writing, converting or comparing gzip compressed images, as uncompressed size is unknown.
- Add Smart File System (SFS0) to format page with option to download SmartFilesystem from aminet.net.
- Add formatting Smart File System (SFS0) partitions in Rigid Disk Block in partition page with option to download SmartFilesystem from aminet.net.
- Add recovery page for scanning and repairing partition tables (RDB, GPT and MBR scan) and Amiga file systems (FFS, PFS3 and SFS) and salvaging directories and files from Amiga file systems to a directory with UAE metadata. A detailed log is written next to the image file or in the home directory for physical disks.

Hst Imager console:
- Add original file size, optimized or compacted file size and percent reduced to optimize command output.
- Add compact option to optimize command copying only used sectors to a new sparse image file (sparse img/hdf or dynamic vhd), which replaces the image file. Zero filled sectors are not written. Vhd image files are always compacted when optimized.
- Add file systems option to optimize command examining FAT12/16/32, exFAT, OFS/FFS (DOS\0-7), PFS3 and SFS file systems in Rigid Disk Block, Master Boot Record and Guid Partition Table partitions, so free sectors are not copied when compacting.
- Read uncompressed size of xz compressed images from xz indexes instead of decompressing entire image when opening it.
- Gzip compressed images are no longer decompressed to determine uncompressed size when opening them. Size is unknown and data is processed until end of image, so progress shows bytes processed and elapsed time only. Converting to vhd decompresses the gzip compressed image once to determine the vhd size.
- Add Smart File System (SFS0) support for reading and writing files with fs commands and formatting with rdb part format and format commands. SFS2 (large file support) is not supported.
- Format command asks to download SmartFilesystem from aminet.net, if file system path is not set for SFS0.
- File systems in amiga hunk format (68k) are preferred when finding file systems in media containing file systems for multiple platforms, e.g. SFS.lha with SmartFilesystem for AmigaOS 3.x and AmigaOS 4.
- Add recovery command with partition table scan and repair (RDB, GPT and MBR scan) and file system scan, repair and salvage (FFS, PFS3 and SFS) sub commands. A detailed log is written next to the image file or in the home directory for physical drives. Repair asks to confirm repair, unless --yes is set. Salvage writes UAEFSDB or UAE metafiles.
- Update Hst.Amiga to v1.0.291, Hst.Core and hst.compression to v1.0.104.

## v1.0

Hst Imager Gui:
- Update overview and details displayed when selecting image file or physical drive in compare, convert, info, optimize, read and write pages:
  - Reads GPT, MBR and RDB partition tables.
  - Better overview of partition tables, partitions and unallocated parts.
- Add force option, number of retries and size option to read, write and compare pages.
- Add size option to optimize page.
- Fix physical drive selection in compare, info, read and write pages to auto select first physical drive, if available.
- Fix Windows querying physical drives by replacing wmic with kernel32.dll for more accurate disk size and faster querying.
- Fix Windows raw disk access locking drives properly before reading or writing.
- Fix Windows raw disk access ignoring/skipping physical drives reporting "not ready error", which occurs with some USB card readers.

Hst Imager console:
- Add file system commands supporting local directories, image files and physical drives:
  - Dir command: List files in local directories, image files, physical drives, .zip, .lha, .adf and .iso files.
  - Copy command: Copy files between local directories, image files and physical drives supporting file systems FAT32, EXT2-4, Fast File System (DOS\0-7) and Professional File System (PFS\3 and PDS\3).
  - Extract command: Extract files from .zip, .lha, .lzx, .adf and .iso to local directories, image files and physical drives.