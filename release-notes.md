# Release notes

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