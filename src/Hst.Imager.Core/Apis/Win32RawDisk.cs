namespace Hst.Imager.Core.Apis
{
    using System;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using Microsoft.Win32.SafeHandles;

    // https://github.com/shinchiro/WinFileIO/blob/master/WinFileIO.cs
    // https://stackoverflow.com/questions/15185295/why-raw-disk-read-in-c-sharp-reads-from-slightly-shifted-offset
    // https://stackoverflow.com/questions/12081343/c-sharp-writefile-stops-writing-at-sector-242-on-usb-drives
    // https://forums.codeguru.com/showthread.php?559101-Direct-write-to-HardDisk-WriteFile-returns-5-access-denied-error
    // https://stackoverflow.com/questions/39154020/mainwindow-createfile-always-returns-1?rq=1
    // http://buiba.blogspot.com/2009/06/using-winapi-createfile-readfile.html
    // https://www.codeproject.com/Articles/1247718/Get-HDD-Serial-Number-with-Csharp
    // http://vbnet.mvps.org/index.html?code/disk/smartide.htm
    // https://stackoverflow.com/questions/4559700/how-to-get-hard-disk-serialnumber-in-c-sharp-no-wmi
    // https://stackoverflow.com/questions/37532548/deviceiocontrol-with-ioctl-volume-get-volume-disk-extents-c-sharp
    public class Win32RawDisk : IDisposable
    {
        private readonly string path;
        private readonly SafeFileHandle safeFileHandle;
        private bool disposed;
        private bool isLocked;

        public Win32RawDisk(string path, bool writeable = false, bool ignoreInvalid = false)
        {
            this.path = path;
            safeFileHandle = DeviceApi.CreateFile(path,
                writeable ? DeviceApi.GENERIC_WRITE | DeviceApi.GENERIC_READ : DeviceApi.GENERIC_READ,
                DeviceApi.FILE_SHARE_READ | DeviceApi.FILE_SHARE_WRITE,
                IntPtr.Zero,
                DeviceApi.OPEN_EXISTING,
                0, //FILE_FLAG_RANDOM_ACCESS,
                IntPtr.Zero);

            if (!ignoreInvalid)
            {
                ThrowIfInvalid();
            }
        }

        // private uint FILE_FLAG_RANDOM_ACCESS = 0x10000000;

        public bool IsInvalid()
        {
            return safeFileHandle.IsInvalid;
        }

        private void ThrowIfInvalid()
        {
            if (!safeFileHandle.IsInvalid)
            {
                return;
            }

            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Handle for path '{path}' is invalid");
        }

        public bool LockDevice()
        {
            uint intOut = 0;
            var success = DeviceApi.DeviceIoControl(safeFileHandle, DeviceApi.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero,
                0,
                ref intOut,
                IntPtr.Zero);

            if (success)
            {
                isLocked = true;
            }
            
            return success;
        }

        public bool DismountDevice()
        {
            uint intOut = 0;
            return DeviceApi.DeviceIoControl(safeFileHandle, DeviceApi.FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0,
                IntPtr.Zero, 0,
                ref intOut,
                IntPtr.Zero);
        }

        public bool UnlockDevice()
        {
            uint intOut = 0;
            var success = DeviceApi.DeviceIoControl(safeFileHandle, DeviceApi.FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero,
                0,
                ref intOut,
                IntPtr.Zero);

            if (success)
            {
                isLocked = false;
            }
            
            return success;
        }

        /// <summary>
        /// Get device number for the raw disk.
        /// </summary>
        /// <returns>Device number.</returns>
        /// <exception cref="Win32Exception">When the DeviceIoControl call fails.</exception>
        public int GetDeviceNumber()
        {
            var buffer = IntPtr.Zero;

            try
            {
                // Allocate memory for the output buffer
                var bufferSize = Marshal.SizeOf(typeof(Kernel32.STORAGE_DEVICE_NUMBER));
                buffer = Marshal.AllocHGlobal(bufferSize);

                uint bytesReturned = 0;
                if (!DeviceApi.DeviceIoControl(
                    safeFileHandle,
                    DeviceApi.IOCTL_STORAGE_GET_DEVICE_NUMBER,
                    IntPtr.Zero,
                    0,
                    buffer,
                    (uint)bufferSize,
                    ref bytesReturned,
                    IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to get device number for path '{path}'");
                }
                
                // Marshal the output buffer to the STORAGE_DEVICE_NUMBER struct
                var deviceNumber = Marshal.PtrToStructure<Kernel32.STORAGE_DEVICE_NUMBER>(buffer);

                return deviceNumber.DeviceNumber;
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        public DiskExtendsResult DiskExtends()
        {
            // Prepare to obtain disk extents.
            // NOTE: This code assumes you only have one disk!
            var vde = new Kernel32.VOLUME_DISK_EXTENTS();
            UInt32 outBufferSize = (UInt32)Marshal.SizeOf(vde);
            IntPtr outBuffer = Marshal.AllocHGlobal((int)outBufferSize);
            UInt32 bytesReturned = 0;
            if (!DeviceApi.DeviceIoControl(safeFileHandle,
                    DeviceApi.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS,
                    IntPtr.Zero,
                    0,
                    outBuffer,
                    outBufferSize,
                    ref bytesReturned,
                    IntPtr.Zero))
            {
                throw new IOException($"IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS for path '{path}' failed");
            }

            // The call succeeded, so marshal the data back to a
            // form usable from managed code.
            Marshal.PtrToStructure(outBuffer, vde);

            Marshal.FreeHGlobal(outBuffer);
            
            return new DiskExtendsResult
            {
                DiskNumber = vde.Extents.DiskNumber, // \\.\PHYSICALDRIVE{0}
                StartingOffset = vde.Extents.StartingOffset,
                ExtentLength = vde.Extents.ExtentLength
            };
        }

        /// <summary>
        /// Get disk geometry. Useful for floppy disks.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Win32Exception"></exception>
        public DiskGeometryResult DiskGeometry()
        {
            var outBufferSize = (uint)Marshal.SizeOf(typeof(Kernel32.DiskGeometry));
            var outBuffer = Marshal.AllocHGlobal((int)outBufferSize);
            uint bytesReturned = 0;
            if (!DeviceApi.DeviceIoControl(safeFileHandle,
                    DeviceApi.IOCTL_DISK_GET_DRIVE_GEOMETRY,
                    IntPtr.Zero,
                    0,
                    outBuffer,
                    outBufferSize,
                    ref bytesReturned,
                    IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed IOCTL_DISK_GET_DRIVE_GEOMETRY for path '{path}'");
            }
            
            // convert out buffer pointer to structure
            //Marshal.PtrToStructure(outBuffer, dge);
            // I/O-control has been invoked successfully, convert to DISK_GEOMETRY_EX structure 
            var diskGeometry = (Kernel32.DiskGeometry)Marshal.PtrToStructure(outBuffer, typeof(Kernel32.DiskGeometry))!;

            Marshal.FreeHGlobal(outBuffer);
            
            return new DiskGeometryResult
            {
                MediaType = diskGeometry.MediaType.ToString(),
                Cylinders = diskGeometry.Cylinders,
                TracksPerCylinder = diskGeometry.TracksPerCylinder,
                SectorsPerTrack = diskGeometry.SectorsPerTrack,
                BytesPerSector = diskGeometry.BytesPerSector
            };
        }
        
        /// <summary>
        /// Get disk geometry extended. Useful for hard disks.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Win32Exception"></exception>
        public DiskGeometryExResult DiskGeometryEx()
        {
            //var dge = new Kernel32.DiskGeometryEx();
            var outBufferSize = (uint)Marshal.SizeOf(typeof(Kernel32.DiskGeometryEx));
            var outBuffer = Marshal.AllocHGlobal((int)outBufferSize);
            uint bytesReturned = 0;
            if (!DeviceApi.DeviceIoControl(safeFileHandle,
                    DeviceApi.IOCTL_DISK_GET_DRIVE_GEOMETRY_EX,
                    IntPtr.Zero,
                    0,
                    outBuffer,
                    outBufferSize,
                    ref bytesReturned,
                    IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed IOCTL_DISK_GET_DRIVE_GEOMETRY_EX for path '{path}'");
            }
            
            // convert out buffer pointer to structure
            //Marshal.PtrToStructure(outBuffer, dge);
            // I/O-control has been invoked successfully, convert to DISK_GEOMETRY_EX structure 
            var dge = (Kernel32.DiskGeometryEx)Marshal.PtrToStructure(outBuffer, typeof(Kernel32.DiskGeometryEx))!;

            Marshal.FreeHGlobal(outBuffer);
            
            return new DiskGeometryExResult
            {
                MediaType = dge.Geometry.MediaType.ToString(),
                Cylinders = dge.Geometry.Cylinders,
                TracksPerCylinder = dge.Geometry.TracksPerCylinder,
                BytesPerSector = dge.Geometry.BytesPerSector
            };
        }

        public StoragePropertyQueryResult StoragePropertyQuery()
        {
            var query = new Kernel32.STORAGE_PROPERTY_QUERY
            {
                PropertyId = 0,
                QueryType = 0
            };
            var inputBufferSize = (uint)Marshal.SizeOf(typeof(Kernel32.STORAGE_PROPERTY_QUERY));
            var inputBuffer = Marshal.AllocHGlobal((int)inputBufferSize);
            Marshal.StructureToPtr(query, inputBuffer, true);
            var headerBufferSize = (uint)Marshal.SizeOf(typeof(Kernel32.STORAGE_DESCRIPTOR_HEADER));
            var headerBuffer = Marshal.AllocHGlobal((int)headerBufferSize);
            uint headerBytesReturned = 0;
            if (!DeviceApi.DeviceIoControl(
                    safeFileHandle,
                    DeviceApi.IOCTL_STORAGE_QUERY_PROPERTY,
                    inputBuffer,
                    inputBufferSize,
                    headerBuffer,
                    headerBufferSize,
                    ref headerBytesReturned,
                    IntPtr.Zero))
            {
                throw new IOException("IOCTL_STORAGE_QUERY_PROPERTY failed");
            }

            var header =
                (Kernel32.STORAGE_DESCRIPTOR_HEADER)Marshal.PtrToStructure(headerBuffer,
                    typeof(Kernel32.STORAGE_DESCRIPTOR_HEADER))!;
            uint descriptorBufferSize = header.Size;
            IntPtr descriptorBufferPointer = Marshal.AllocHGlobal((int)descriptorBufferSize);
            uint descriptorBytesReturned = 0;
            if (!DeviceApi.DeviceIoControl(
                    safeFileHandle,
                    DeviceApi.IOCTL_STORAGE_QUERY_PROPERTY,
                    inputBuffer,
                    inputBufferSize,
                    descriptorBufferPointer,
                    descriptorBufferSize,
                    ref descriptorBytesReturned,
                    IntPtr.Zero))
            {
                throw new IOException("IOCTL_STORAGE_QUERY_PROPERTY descriptor failed");
            }

            var descriptor = (Kernel32.STORAGE_DEVICE_DESCRIPTOR)Marshal.PtrToStructure(
                descriptorBufferPointer, typeof(Kernel32.STORAGE_DEVICE_DESCRIPTOR))!;            
            var descriptorBuffer = new byte[descriptorBufferSize];
            Marshal.Copy(descriptorBufferPointer, descriptorBuffer, 0, descriptorBuffer.Length);
            var vendorId = GetData(descriptorBuffer, (int)descriptor.VendorIdOffset);
            var productId = GetData(descriptorBuffer, (int)descriptor.ProductIdOffset);
            var productRevision = GetData(descriptorBuffer,
                (int)descriptor.ProductRevisionOffset);
            var serialNumber = GetData(descriptorBuffer, (int)descriptor.SerialNumberOffset);
            serialNumber = nonHexRegex.Replace(serialNumber, string.Empty);
            //serialNumber = HexStringToBinary(serialNumber);

            Marshal.FreeHGlobal(inputBuffer);
            Marshal.FreeHGlobal(headerBuffer);
            Marshal.FreeHGlobal(descriptorBufferPointer);
            
            return new StoragePropertyQueryResult
            {
                VendorId = vendorId,
                ProductId = productId,
                ProductRevision = productRevision,
                SerialNumber = serialNumber,
                BusType = descriptor.BusType.ToString()
            };
        }

        public bool Verify()
        {
            uint bytesReturned = 0;
            return DeviceApi.DeviceIoControl(
                    safeFileHandle,
                    DeviceApi.IOCTL_STORAGE_CHECK_VERIFY2,
                    IntPtr.Zero, 
                    0,
                    IntPtr.Zero,
                    0,
                    ref bytesReturned,
                    IntPtr.Zero);
        }

        public static string GetData(byte[] array, int index, bool reverse = false)
        {
            //index is used to get data from particular position in the byte array
            if (array == null || array.Length == 0 || index <= 0 || index >= array.Length) return "";
            int i;
            for (i = index; i < array.Length; i++)
            {
                //go until zero value in byte array which is like delimiter 
                if (array[i] == 0) break;
            }

            if (index == i) return "";
            //Now we need to create a buffer to split data buffer from main buffer	
            var valueBytes = new byte[i - index];
            Array.Copy(array, index, valueBytes, 0, valueBytes.Length);
            if (reverse) Array.Reverse(valueBytes);
            return System.Text.Encoding.ASCII.GetString(valueBytes).Trim();
        }

        private static Regex nonHexRegex = new Regex("[^0-9a-z]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        public static string HexStringToBinary(string myHex)
        {
            string str = "";
            for (int i = 0; i < myHex.Length; i += 2)
            {
                str += (char)Int16.Parse(myHex.Substring(i, 2), NumberStyles.AllowHexSpecifier);
            }

            // The serial number is encoded in HEX and with each two characters encoded swapped.
            // ER ABCD -> BADC -> '42414443'
            return Swap(str.ToCharArray());
        }

        public static string Swap(char[] array)
        {
            for (int i = 0; i <= array.Length - 2; i += 2)
            {
                char t;
                t = array[i];
                array[i] = array[i + 1];
                array[i + 1] = t;
            }

            string s = new string(array);
            return s;
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
            var offset = Position();
            throw new IOException(
                $"Failed to read data '{buffer.Length}', count '{count}' and offset '{offset}', ReadFile returned Win32 error {error}");
        }

        public uint Write(byte[] buffer, int count)
        {
            if (DeviceApi.WriteFile(safeFileHandle, buffer, Convert.ToUInt32(count), out var bytesWritten,
                    IntPtr.Zero))
            {
                return bytesWritten;
            }

            var error = Marshal.GetLastWin32Error();
            var offset = Position();
            throw new IOException(
                $"Failed to write data '{buffer.Length}', count '{count}' and offset '{offset}', WriteFile returned Win32 error {error}");
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
            throw new IOException(
                $"Failed to seek position offset '{offset}' and origin '{origin}', SetFilePointerEx returned Win32 error {error}");
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
                if (isLocked)
                {
                    UnlockDevice();
                }
                CloseDevice();
                if (!safeFileHandle.IsInvalid)
                {
                    safeFileHandle.Close();
                }
                safeFileHandle.Dispose();
            }

            disposed = true;
        }

        public void Dispose() => Dispose(true);
    }
}