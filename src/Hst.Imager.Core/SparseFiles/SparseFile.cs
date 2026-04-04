using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Hst.Core.Extensions;
using Microsoft.Win32.SafeHandles;

namespace Hst.Imager.Core.SparseFiles;

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
    
    private const uint FSCTL_GET_RETRIEVAL_POINTERS = 0x90073;
    private const uint FSCTL_GET_SPARSE = 0x900C4;
    private const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200;    

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

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetFileAttributes(string lpFileName);

    /// <summary>
    /// Use the GetCompressedFileSize function to obtain the actual size allocated on disk for a sparse file. This total does not include the size of the regions which were deallocated because they were filled with zeros.
    /// </summary>
    /// <param name="lpFileName"></param>
    /// <param name="lpFileSizeHigh"></param>
    /// <returns></returns>
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetCompressedFileSizeW(string lpFileName, out uint lpFileSizeHigh);
    
    public static void CreateSparseFile(string path, long size)
    {
        if (Hst.Core.OperatingSystem.IsWindows())
        {
            CreateWindowsSparseFile(path, size);
            return;
        }
        
        CreateLinuxSparseFile(path, size);
    }

    private static void CreateWindowsSparseFile(string path, long size)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        // mark file as sparse
        if (!DeviceIoControl(fileStream.SafeFileHandle, FSCTL_SET_SPARSE, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
        {
            throw new IOException("Failed to set sparse attribute. Error: " + Marshal.GetLastWin32Error());
        }
        
        fileStream.SetLength(size);
    }

    public static void CreateLinuxSparseFile(string path, long size)
    {
        "truncate".RunProcess($"-s {size} \"{path}\"");
    }
    
    public static bool IsSparseFile(string path) => 
        Hst.Core.OperatingSystem.IsWindows() ? IsWindowsSparseFile(path) : IsLinuxSparseFile(path);
    
    public static bool IsWindowsSparseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File not found", path);
        }

        var fileAttributes = GetFileAttributes(path);
        if (fileAttributes == 0xFFFFFFFF) // INVALID_FILE_ATTRIBUTES
        {
            throw new IOException("Unable to get file attributes. Error: " + Marshal.GetLastWin32Error());
        }

        return (fileAttributes & FILE_ATTRIBUTE_SPARSE_FILE) != 0;
    }
    
    public static bool IsLinuxSparseFile(string path)
    {
        var fileSize = new FileInfo(path).Length;
        var sparseFileSize = GetLinuxSparseFileSize(path);

        // return true, if the sparse file size is less than file size.
        // the file is then considered sparse.
        return sparseFileSize < fileSize;
    }

    public static long GetSparseFileSize(string path) =>
        Hst.Core.OperatingSystem.IsWindows() ? GetWindowsSparseFileSize(path) : GetLinuxSparseFileSize(path);

    /// <summary>
    /// Gets the actual disk space used by a file (including sparse files).
    /// </summary>
    public static long GetWindowsSparseFileSize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            
            throw new ArgumentException("File path cannot be null or empty.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File not found.", path);
        }

        uint high;
        var low = GetCompressedFileSizeW(path, out high);

        if (low == 0xFFFFFFFF)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != 0) // 0 means no error
            {
                throw new IOException($"Error getting compressed file size. Win32 Error: {error}");
            }
        }

        return ((long)high << 32) + low;
    }

    private static readonly Regex DuRegex = new(@"^(\d+)\s");
    
    public static long GetLinuxSparseFileSize(string path)
    {
        var output = "du".RunProcess($"\"{path}\"").Trim();

        var duMatch = DuRegex.Match(output);
        if (!duMatch.Success)
        {
            throw new IOException($"Unexpected output from 'du' command: '{output}'");
        }

        if (!long.TryParse(duMatch.Groups[1].Value, out var size))
        {
            throw new IOException($"Unable to parse size from 'du' output: '{output}'");
        }
        
        return size;
    }
}