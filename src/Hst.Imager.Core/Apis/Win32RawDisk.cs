﻿namespace Hst.Imager.Core.Apis
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;

    // https://github.com/shinchiro/WinFileIO/blob/master/WinFileIO.cs
    // https://stackoverflow.com/questions/15185295/why-raw-disk-read-in-c-sharp-reads-from-slightly-shifted-offset
    // https://stackoverflow.com/questions/12081343/c-sharp-writefile-stops-writing-at-sector-242-on-usb-drives
    // https://forums.codeguru.com/showthread.php?559101-Direct-write-to-HardDisk-WriteFile-returns-5-access-denied-error
    // https://stackoverflow.com/questions/39154020/mainwindow-createfile-always-returns-1?rq=1
    // http://buiba.blogspot.com/2009/06/using-winapi-createfile-readfile.html
    public class Win32RawDisk : IDisposable
    {
        private readonly SafeFileHandle safeFileHandle;
        private bool disposed;

        public Win32RawDisk(string path, bool writeable = false)
        {
            safeFileHandle = DeviceApi.CreateFile(path,
                writeable ? DeviceApi.GENERIC_WRITE : DeviceApi.GENERIC_READ,
                DeviceApi.FILE_SHARE_READ | DeviceApi.FILE_SHARE_WRITE,
                IntPtr.Zero,
                DeviceApi.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (safeFileHandle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                
                throw new IOException($"Handle for path '{path}' is invalid, win32 error code {error}");
            }
        }

        public bool LockDevice()
        {
            uint intOut = 0;
            return DeviceApi.DeviceIoControl(safeFileHandle, DeviceApi.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0,
                ref intOut,
                IntPtr.Zero);
        }

        public bool DismountDevice()
        {
            uint intOut = 0;
            return DeviceApi.DeviceIoControl(safeFileHandle, DeviceApi.FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0,
                ref intOut,
                IntPtr.Zero);
        }
        
        public bool UnlockDevice()
        {
            uint intOut = 0;
            return DeviceApi.DeviceIoControl(safeFileHandle, DeviceApi.FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0,
                ref intOut,
                IntPtr.Zero);
        }

        public void CloseDevice()
        {
            DeviceApi.CloseHandle(safeFileHandle);
        }

        public uint Read(byte[] buffer, int count)
        {
            if (DeviceApi.ReadFile(safeFileHandle, buffer, Convert.ToUInt32(count), out var bytesRead,
                    IntPtr.Zero))
            {
                return bytesRead;
            }
            
            var error = Marshal.GetLastWin32Error();
            throw new IOException($"Failed to read data '{buffer.Length}' and count '{count}', ReadFile returned Win32 error {error}");
        }
        
        public uint Write(byte[] buffer, int count)
        {
            if (DeviceApi.WriteFile(safeFileHandle, buffer, Convert.ToUInt32(count), out var bytesWritten,
                    IntPtr.Zero))
            {
                return bytesWritten;
            }
            
            var error = Marshal.GetLastWin32Error();
            throw new IOException($"Failed to write data '{buffer.Length}' and count '{count}', WriteFile returned Win32 error {error}");
        }
        
        public long Seek(long offset, SeekOrigin origin)
        {
            if (!Enum.TryParse<DeviceApi.EMoveMethod>(origin.ToString(), out var moveMethod))
            {
                throw new ArgumentOutOfRangeException(nameof(origin));
            }

            if (DeviceApi.SetFilePointerEx(safeFileHandle, offset, out var newOffset, moveMethod))
            {
                return newOffset;
            }
            var error = Marshal.GetLastWin32Error();
            throw new IOException($"Failed to seek position offset '{offset}' and origin '{origin}', SetFilePointerEx returned Win32 error {error}");
        }        

        public long Position()
        {
            if (DeviceApi.SetFilePointerEx(safeFileHandle, 0, out var offset, DeviceApi.EMoveMethod.Current))
            {
                return offset;
            }
            var error = Marshal.GetLastWin32Error();
            throw new IOException($"Failed to get position, SetFilePointerEx returned Win32 error {error}");
        }        

        public long Size()
        {
            var diskGeometry = Kernel32.GetDiskGeometryEx(safeFileHandle);
            return diskGeometry.DiskSize;
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                safeFileHandle.Close();
                safeFileHandle.Dispose();
            }

            disposed = true;
        }

        public void Dispose() => Dispose(true);        
    }
}