using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Hst.Imager.Core.FileInfos;

public static class LinuxFileInfo
{
    // Define struct stat for Linux (64-bit)
    // This matches the layout from /usr/include/x86_64-linux-gnu/bits/stat.h
    [StructLayout(LayoutKind.Sequential)]
    internal struct LinuxStat
    {
        public ulong st_dev;     // Device ID
        public ulong st_ino;     // Inode number
        public ulong st_nlink;   // Number of hard links
        public uint st_mode;     // File mode
        public uint st_uid;      // User ID of owner
        public uint st_gid;      // Group ID of owner
        public uint __pad0;
        public ulong st_rdev;    // Device type (if inode device)
        public long st_size;     // Total size, in bytes
        public long st_blksize;  // Block size for filesystem I/O
        public long st_blocks;   // Number of 512B blocks allocated

        public TimeSpec st_atim; // Last access time
        public TimeSpec st_mtim; // Last modification time
        public TimeSpec st_ctim; // Last status change time

        [StructLayout(LayoutKind.Sequential)]
        internal struct TimeSpec
        {
            public long tv_sec;   // Seconds
            public long tv_nsec;  // Nanoseconds
        }
    }

    // Import stat from libc
    [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int stat(string path, out LinuxStat buf);

    public static Stat GetStat(string path)
    {
        if (stat(path, out var statBuf) == 0)
            return new Stat(statBuf.st_dev, statBuf.st_ino, statBuf.st_nlink,
                statBuf.st_mode, statBuf.st_uid, statBuf.st_gid, statBuf.st_rdev, statBuf.st_size,
                statBuf.st_blksize, statBuf.st_blocks,
                DateTimeOffset.FromUnixTimeSeconds(statBuf.st_atim.tv_sec).UtcDateTime,
                DateTimeOffset.FromUnixTimeSeconds(statBuf.st_mtim.tv_sec).UtcDateTime,
                DateTimeOffset.FromUnixTimeSeconds(statBuf.st_ctim.tv_sec).UtcDateTime);

        var error = Marshal.GetLastWin32Error();
        throw new IOException("Linux stat failed", new System.ComponentModel.Win32Exception(error));
    }
}