using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Hst.Imager.Core;

/// <summary>
/// Creates a sparse file.
/// A sparse file doesn't allocate any disk space for zero filled gaps.
/// For example creating a sparse file and changing it's size to 16GB will not allocate 16GB of disk space.
/// Sparse files are only supported by NTFS file system.
/// </summary>
public static class SparseFile
{
    private const uint FSCTL_SET_SPARSE = 0x900C4;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint CREATE_ALWAYS = 2;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    public static void Create(string path)
    {
        // create or overwrite file
        var handle = CreateFile(
            path,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            CREATE_ALWAYS,
            FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        // mark file as sparse
        if (!DeviceIoControl(handle, FSCTL_SET_SPARSE, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
        {
            throw new IOException("Failed to set sparse attribute. Error: " + Marshal.GetLastWin32Error());
        }
    }
}