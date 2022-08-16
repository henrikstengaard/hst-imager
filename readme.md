# Hst Imager

Hst Imager is an imaging tool to read and write disk images to and from physical drives. 

This tool can be used to create new blank images or create images of physical drives like hard disks, SSD, CF- and MicroSD-cards for backup and/or modification and then write them to physical drives.

## Features

Hst Imager comes with following features:
- List physical drives (*).
- Display information about physical drive or image file (*).
- Read physical drive to image file (*).
- Write image file to physical drive (*).
- Convert image file between .img/.hdf and .vhd.
- Create blank .img/.hdf and .vhd image file.
- Optimize image file.

(*) requires administrative rights on Windows, macOS and Linux.

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

 with Amiga emulators much faster than real Amiga hardware
 with support for Amiga rigid disk block (RDSK, partition table used by Amiga computers).

Hst Imager supports Amiga rigid disk block (RDSK, partition table used by Amiga computers) by reading first 16 blocks (512 bytes * 16) from source physical drive or image file.
When creating an image file from a physical drive, Hst Imager uses Amiga rigid disk block to define the size of the image file to create.
E.g. if a 120GB SSD contains a 16GB Amiga rigid disk block, Hst Imager will only read the 16GB used and not the entire 120GB.

Image files are much faster to use with Amiga emulators on modern computers than t.

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