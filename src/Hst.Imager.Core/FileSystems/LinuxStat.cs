using System.IO;
using System.Runtime.InteropServices;

namespace Hst.Imager.Core.FileSystems;

public static class LinuxStat
{
    [DllImport("libc", SetLastError = true)]
    private static extern int statx(
        int dirfd,
        string pathname,
        int flags,
        uint mask,
        out Statx statxbuf
    );

    private const int AT_STATX_SYNC_AS_STAT = 0x0000;
    private const uint STATX_ALL = 0x00000fffU;

    [StructLayout(LayoutKind.Sequential)]
    private struct StatxTimestamp
    {
        public long Seconds;
        public uint Nanoseconds;
        public int Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Statx
    {
        public uint Mask;
        public uint BlkSize;
        public ulong Attributes;
        public uint Nlink;
        public uint UID;
        public uint GID;
        public ushort Mode;
        public ushort Padding1;
        public ulong Inode;
        public ulong Size;
        public ulong Blocks;
        public StatxTimestamp Access;
        public StatxTimestamp Modified;
        public StatxTimestamp Changed;
        public StatxTimestamp Birth;
        public uint Extra1;
        public uint Extra2;
        public uint Extra3;
        public uint Extra4;
        public uint Extra5;
        public uint Extra6;
    }

    public static GenericFileInfo GetStat(string path)
    {
        if (statx(0, path, AT_STATX_SYNC_AS_STAT, STATX_ALL, out var info) != 0)
        {
            throw new IOException($"statx failed (errno={Marshal.GetLastWin32Error()})");
        }
        
        return new GenericFileInfo(path, (long)info.Size, info.Mode);
    }
}