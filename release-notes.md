# Release notes

## v2.0

Hst Imager Gui:
- Add compact and examine file systems options to optimize page.

Hst Imager console:
- Add compact option to optimize command copying only used sectors to a new sparse image file (sparse img/hdf or dynamic vhd), which replaces the image file. Zero filled sectors are not written. Vhd image files are always compacted when optimized.
- Add file systems option to optimize command examining FAT12/16/32, exFAT, OFS/FFS (DOS\0-7) and PFS3 file systems in Rigid Disk Block, Master Boot Record and Guid Partition Table partitions, so free sectors are not copied when compacting.

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