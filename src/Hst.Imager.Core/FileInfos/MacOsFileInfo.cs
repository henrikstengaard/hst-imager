using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Hst.Imager.Core.FileInfos;

public static class MacOsFileInfo
{
    // macOS struct stat from /usr/include/sys/stat.h
    [StructLayout(LayoutKind.Sequential)]
    internal struct MacOsStat
    {
        public uint st_dev;      // Device ID (dev_t)
        public ushort st_mode;   // File mode
        public ushort st_nlink;  // Number of hard links
        public ulong st_ino;     // Inode number
        public uint st_uid;      // User ID of owner
        public uint st_gid;      // Group ID of owner
        public uint st_rdev;     // Device type (if inode device)
        public long st_size;     // Total size, in bytes
        public long st_atime;    // Last access time (seconds)
        public long st_atimensec;// Nanoseconds
        public long st_mtime;    // Last modification time (seconds)
        public long st_mtimensec;// Nanoseconds
        public long st_ctime;    // Last status change time (seconds)
        public long st_ctimensec;// Nanoseconds
        public long st_birthtime;// Creation time (seconds)
        public long st_birthtimensec; // Nanoseconds
        public long st_blksize;  // Block size for filesystem I/O
        public long st_blocks;   // Number of 512B blocks allocated
        public uint st_flags;    // User defined flags
        public uint st_gen;      // File generation number
        public int st_lspare;
        public long st_qspare1;
        public long st_qspare2;
    }

    // On macOS, stat is in libSystem
    [DllImport("libSystem.B.dylib", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int stat(string path, out MacOsStat buf);
    
    public static Stat GetStat(string path)
    {
        if (stat(path, out var statBuf) == 0)
            return new Stat(statBuf.st_dev, statBuf.st_ino, statBuf.st_nlink,
                statBuf.st_mode, statBuf.st_uid, statBuf.st_gid, statBuf.st_rdev, statBuf.st_size,
                statBuf.st_blksize, statBuf.st_blocks,
                DateTimeOffset.FromUnixTimeSeconds(statBuf.st_atime).UtcDateTime,
                DateTimeOffset.FromUnixTimeSeconds(statBuf.st_mtime).UtcDateTime,
                DateTimeOffset.FromUnixTimeSeconds(statBuf.st_ctime).UtcDateTime);

        var error = Marshal.GetLastWin32Error();
        throw new IOException("MacOS stat failed", new System.ComponentModel.Win32Exception(error));
    }
}