using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Hst.Imager.Core.FileInfos;

public static class MacOsFileInfo
{
    private const string LibSystem = "libSystem.B.dylib";

    // Buffers are passed as byte arrays instead of a marshalled struct, so an
    // incorrect struct layout can never cause the kernel to write past the
    // end of the buffer and corrupt the stack or heap.

    // On x86-64 macOS, the 64-bit inode variant of stat is exported as
    // "stat$INODE64". On arm64 macOS, only "stat" exists.
    [DllImport(LibSystem, EntryPoint = "stat$INODE64", SetLastError = true)]
    private static extern int sys_stat_inode64(byte[] path, byte[] buf);

    [DllImport(LibSystem, EntryPoint = "stat", SetLastError = true)]
    private static extern int sys_stat(byte[] path, byte[] buf);

    private static bool? useInode64;

    private static int Stat(byte[] path, byte[] buffer)
    {
        if (useInode64 == false)
        {
            return sys_stat(path, buffer);
        }

        if (useInode64 == true)
        {
            return sys_stat_inode64(path, buffer);
        }

        try
        {
            var result = sys_stat_inode64(path, buffer);
            useInode64 = true;
            return result;
        }
        catch (EntryPointNotFoundException)
        {
            useInode64 = false;
            return sys_stat(path, buffer);
        }
    }

    /// <summary>
    /// Gets stat for path. Returns false instead of throwing, if stat fails.
    /// </summary>
    public static bool TryGetStat(string path, out Stat stat) => TryGetStat(path, out stat, out _);

    private static bool TryGetStat(string path, out Stat stat, out int errorCode)
    {
        stat = null;
        errorCode = 0;

        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("MacOS stat is only supported on macOS");
        }

        var pathBytes = NativeStatHelper.ToNullTerminatedUtf8(path);
        var buffer = new byte[NativeStatHelper.StatBufferSize];

        if (Stat(pathBytes, buffer) != 0)
        {
            errorCode = Marshal.GetLastWin32Error();
            return false;
        }

        stat = Parse(buffer);

        return true;
    }

    // struct stat layout for 64-bit macOS, see /usr/include/sys/stat.h,
    // __DARWIN_STRUCT_STAT64 (144 bytes). Identical on x86-64 and arm64.
    private static Stat Parse(byte[] buffer) =>
        new(
            NativeStatHelper.ReadUInt32(buffer, 0),    // st_dev (int32)
            NativeStatHelper.ReadUInt64(buffer, 8),    // st_ino
            NativeStatHelper.ReadUInt16(buffer, 6),    // st_nlink
            NativeStatHelper.ReadUInt16(buffer, 4),    // st_mode
            NativeStatHelper.ReadUInt32(buffer, 16),   // st_uid
            NativeStatHelper.ReadUInt32(buffer, 20),   // st_gid
            NativeStatHelper.ReadUInt32(buffer, 24),   // st_rdev (int32)
            // 28: padding
            NativeStatHelper.ReadInt64(buffer, 96),    // st_size
            NativeStatHelper.ReadInt32(buffer, 112),   // st_blksize
            NativeStatHelper.ReadInt64(buffer, 104),   // st_blocks
            NativeStatHelper.ToUtcDateTime(NativeStatHelper.ReadInt64(buffer, 32)), // st_atimespec.tv_sec
            NativeStatHelper.ToUtcDateTime(NativeStatHelper.ReadInt64(buffer, 48)), // st_mtimespec.tv_sec
            NativeStatHelper.ToUtcDateTime(NativeStatHelper.ReadInt64(buffer, 64))  // st_ctimespec.tv_sec
        );
}
