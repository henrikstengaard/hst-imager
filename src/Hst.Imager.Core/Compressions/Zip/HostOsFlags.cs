using System;

namespace Hst.Imager.Core.Compressions.Zip
{
    /// <summary>
    /// Host OS (20-255 unused)
    /// </summary>
    [Flags]
    public enum HostOsFlags : int
    {
        /// <summary>
        /// MS-DOS and OS/2 (FAT / VFAT / FAT32 file systems)
        /// </summary>
        MsDos = 0,
        /// <summary>
        /// Amiga
        /// </summary>
        Amiga = 1,
        /// <summary>
        /// OpenVMS
        /// </summary>
        OpenVms = 2,
        /// <summary>
        /// UNIX
        /// </summary>
        Unix = 3,
        /// <summary>
        /// VM/CMS
        /// </summary>
        VmCms = 4,
        /// <summary>
        /// Atari ST
        /// </summary>
        AtariSt = 5,
        /// <summary>
        /// OS/2 H.P.F.S.
        /// </summary>
        Os2 = 6,
        /// <summary>
        /// Macintosh
        /// </summary>
        Macintosh = 7,
        /// <summary>
        /// Z-System
        /// </summary>
        ZSystem = 8,
        /// <summary>
        /// CP/M
        /// </summary>
        Cpm = 9,
        /// <summary>
        /// Windows NTFS
        /// </summary>
        WindowsNtfs = 10,
        /// <summary>
        /// MVS (OS/390 - Z/OS)
        /// </summary>
        Mvs = 11,
        /// <summary>
        /// VSE
        /// </summary>
        Vse = 12,
        /// <summary>
        /// Acorn Risc
        /// </summary>
        AcornRisc = 13,
        /// <summary>
        /// VFAT
        /// </summary>
        Vfat = 14,
        /// <summary>
        /// Alternate MVS
        /// </summary>
        AlternateMvs = 15,
        /// <summary>
        /// BeOS
        /// </summary>
        BeOs = 16,
        /// <summary>
        /// Tandem
        /// </summary>
        Tandem = 17,
        /// <summary>
        /// OS/400
        /// </summary>
        Os400 = 18,
        /// <summary>
        /// OS/X (Darwin)
        /// </summary>
        Osx = 19
    }
}
