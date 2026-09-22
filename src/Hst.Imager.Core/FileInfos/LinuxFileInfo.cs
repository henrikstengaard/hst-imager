using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Hst.Imager.Core.FileInfos;

public static class LinuxFileInfo
{
    private const string LibC = "libc";

    /// <summary>
    /// Candidate libc libraries. DllImport("libc") alone is not reliable on
    /// distributions like Ubuntu, where "libc.so" is a linker script that
    /// dlopen can not load, and on musl based distributions like Alpine.
    /// </summary>
    private static readonly string[] LibCNames =
    {
        "libc.so.6",
        "libc.musl-x86_64.so.1",
        "libc.musl-aarch64.so.1",
        "libc.so"
    };

    static LinuxFileInfo()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        NativeLibrary.SetDllImportResolver(typeof(LinuxFileInfo).Assembly, (libraryName, assembly, searchPath) =>
        {
            if (!libraryName.Equals(LibC, StringComparison.Ordinal))
            {
                return IntPtr.Zero;
            }

            foreach (var name in LibCNames)
            {
                if (NativeLibrary.TryLoad(name, assembly, searchPath, out var handle))
                {
                    return handle;
                }
            }

            return IntPtr.Zero;
        });
    }

    // Buffers are passed as byte arrays instead of a marshalled struct, so an
    // incorrect struct layout can never cause the kernel to write past the
    // end of the buffer and corrupt the stack or heap.
    [DllImport(LibC, EntryPoint = "stat", SetLastError = true)]
    private static extern int sys_stat(byte[] path, byte[] buf);

    // glibc older than 2.33 (eg. Ubuntu 20.04) does not export "stat" as a
    // symbol, it's an inline function calling __xstat.
    [DllImport(LibC, EntryPoint = "__xstat", SetLastError = true)]
    private static extern int sys_xstat(int version, byte[] path, byte[] buf);

    private static bool? useXstat;

    /// <summary>
    /// _STAT_VER used by __xstat: 1 on x86-64, 0 on other architectures.
    /// </summary>
    private static int StatVersion =>
        RuntimeInformation.ProcessArchitecture == Architecture.X64 ? 1 : 0;

    private static int Stat(byte[] path, byte[] buffer)
    {
        if (useXstat == false)
        {
            return sys_stat(path, buffer);
        }

        if (useXstat == true)
        {
            return sys_xstat(StatVersion, path, buffer);
        }

        try
        {
            var result = sys_stat(path, buffer);
            useXstat = false;
            return result;
        }
        catch (EntryPointNotFoundException)
        {
            useXstat = true;
            return sys_xstat(StatVersion, path, buffer);
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

        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Linux stat is only supported on Linux");
        }

        var pathBytes = NativeStatHelper.ToNullTerminatedUtf8(path);
        var buffer = new byte[NativeStatHelper.StatBufferSize];

        if (Stat(pathBytes, buffer) != 0)
        {
            errorCode = Marshal.GetLastWin32Error();
            return false;
        }

        stat = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => ParseX64(buffer),
            Architecture.Arm64 => ParseArm64(buffer),
            var architecture => throw new PlatformNotSupportedException(
                $"Linux stat is not supported on architecture '{architecture}'")
        };

        return true;
    }

    // struct stat layout for x86-64 glibc, see
    // /usr/include/x86_64-linux-gnu/bits/stat.h (144 bytes).
    private static Stat ParseX64(byte[] buffer) =>
        new(
            NativeStatHelper.ReadUInt64(buffer, 0),    // st_dev
            NativeStatHelper.ReadUInt64(buffer, 8),    // st_ino
            NativeStatHelper.ReadUInt64(buffer, 16),   // st_nlink
            NativeStatHelper.ReadUInt32(buffer, 24),   // st_mode
            NativeStatHelper.ReadUInt32(buffer, 28),   // st_uid
            NativeStatHelper.ReadUInt32(buffer, 32),   // st_gid
            // 36: __pad0
            NativeStatHelper.ReadUInt64(buffer, 40),   // st_rdev
            NativeStatHelper.ReadInt64(buffer, 48),    // st_size
            NativeStatHelper.ReadInt64(buffer, 56),    // st_blksize
            NativeStatHelper.ReadInt64(buffer, 64),    // st_blocks
            NativeStatHelper.ToUtcDateTime(NativeStatHelper.ReadInt64(buffer, 72)),  // st_atim.tv_sec
            NativeStatHelper.ToUtcDateTime(NativeStatHelper.ReadInt64(buffer, 88)),  // st_mtim.tv_sec
            NativeStatHelper.ToUtcDateTime(NativeStatHelper.ReadInt64(buffer, 104))  // st_ctim.tv_sec
        );

    // struct stat layout for aarch64 glibc, see
    // /usr/include/aarch64-linux-gnu/bits/stat.h (128 bytes).
    private static Stat ParseArm64(byte[] buffer) =>
        new(
            NativeStatHelper.ReadUInt64(buffer, 0),    // st_dev
            NativeStatHelper.ReadUInt64(buffer, 8),    // st_ino
            NativeStatHelper.ReadUInt32(buffer, 20),   // st_nlink
            NativeStatHelper.ReadUInt32(buffer, 16),   // st_mode
            NativeStatHelper.ReadUInt32(buffer, 24),   // st_uid
            NativeStatHelper.ReadUInt32(buffer, 28),   // st_gid
            NativeStatHelper.ReadUInt64(buffer, 32),   // st_rdev
            // 40: __pad1
            NativeStatHelper.ReadInt64(buffer, 48),    // st_size
            NativeStatHelper.ReadInt32(buffer, 56),    // st_blksize
            // 60: __pad2
            NativeStatHelper.ReadInt64(buffer, 64),    // st_blocks
            NativeStatHelper.ToUtcDateTime(NativeStatHelper.ReadInt64(buffer, 72)),  // st_atim.tv_sec
            NativeStatHelper.ToUtcDateTime(NativeStatHelper.ReadInt64(buffer, 88)),  // st_mtim.tv_sec
            NativeStatHelper.ToUtcDateTime(NativeStatHelper.ReadInt64(buffer, 104))  // st_ctim.tv_sec
        );
}
