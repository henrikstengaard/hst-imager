﻿# Hst Imager

Hst Imager is an imaging tool to read and write disk images to and from physical drives. 

This tool can be used to create new blank images or create images of physical drives like hard disks, SSD, CF- and MicroSD-cards for backup and/or modification and then write them to physical drives.

## Features

Hst Imager comes with following features:
- List physical drives.
- Read information from physical drive or image file.
- Read physical drive to image file.
- Write image file to physical drive.
- Convert image file between .img/.hdf and .vhd.
- Create blank .img/.hdf and .vhd image file.
- Optimize image file size.
- Master Boot Record:
  - Read Master Boot Record information.
  - Initialize Master Boot Record.
  - Add partition to Master Boot Record.
  - Delete partition from Master Boot Record.
  - Format partition in Master Boot Record.
- Rigid Disk Block;
  - Read Rigid Disk Block information.
  - Initialize Rigid Disk Block.
  - Add file system to Rigid Disk Block.
  - Delete file system from Rigid Disk Block.
  - Export file system from Rigid Disk Block to file.
  - Import file systems from Rigid Disk Block or ADF file.
  - Update file system in Rigid Disk Block.
  - Add partition to Rigid Disk Block.
  - Copy partition from one Rigid Disk Block to another.
  - Delete partition from Rigid Disk Block.
  - Export partition from Rigid Disk Block to hard file.
  - Format partition in Rigid Disk Block.
  - Import partition from hard file to Rigid Disk Block.
  - Kill and restore partition in Rigid Disk Block.
  - Update partition in Rigid Disk Block.

**Read and write for physical drives requires administrative rights on Windows, macOS and Linux.**

## Supported operating systems

Hst Imager supports following operating systems:
- Windows 
- macOS
- Linux

## Img file format

Img file format is a raw dump of hard disks, SSD, CF- and MicroSD-cards and consists of a sector-by-sector binary copy of the source.

Creating an .img image file from a 64GB CF-card using Hst Imager will require 64GB of free disk space on the specified destination path.

## Vhd file format

Vhd file format is a virtual hard disk drive with fixed and dynamic sizes.

Fixed sized vhd file pre-allocates the requested size when created same way as .img file format.

Dynamic sized vhd file only allocates storage to store actual data. Unused or zero filled parts of vhd file are not allocated resulting in smaller image files compared to img image files.

Creating a dynamic sized vhd image file from a 64GB CF-card using Hst Imager will only require free disk space on the specified destination path matching disk space used on source physical drive. Zero filled (unused) sectors are skipped, when creating a vhd image.

## Amiga support

Hst Imager supports Amiga Rigid Disk Block (RDSK, partition table used by Amiga computers) and can initialize new Rigid Disk Block and modify existing Rigid Disk Block.

Reading an Amiga hard drive to an image files is very useful with Amiga emulators to make changes much faster than real hardware and afterwards write the modified image file back to a hard drive.

### Amiga emulators with vhd support

Following Amiga emulators support .vhd image files:
- WinUAE 4.9.0: https://www.winuae.net/
- FS-UAE v3.1.66: https://fs-uae.net/

FS-UAE might require following custom option to force RDB mode by manually changing FS-UAE configuration file (replace 0 with other hard drive number if needed):
```
hard_drive_0_type = rdb
```

## References

References used for creating Hst Imager:

- http://csharphelper.com/blog/2017/10/get-hard-drive-serial-number-c/
- https://stackoverflow.com/questions/16679331/createfile-in-kernel32-dll-returns-an-invalid-handle
- https://github.com/t00/TestCrypt/blob/master/TestCrypt/PhysicalDrive.cs
- https://stackoverflow.com/questions/327718/how-to-list-physical-disks
- https://blog.codeinside.eu/2019/09/30/enforce-administrator-mode-for-builded-dotnet-exe/