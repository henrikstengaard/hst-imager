namespace HstWbInstaller.Imager.Core.Apis
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Win32.SafeHandles;
    using LPSECURITY_ATTRIBUTES = System.IntPtr;
    using LPOVERLAPPED = System.IntPtr;
    using LPVOID = System.IntPtr;
    using HANDLE = System.IntPtr;
    using LARGE_INTEGER = System.Int64;
    using DWORD = System.UInt32;
    using LPCTSTR = System.String;

    // https://stackoverflow.com/questions/15051660/physical-disk-size-not-correct-ioctldiskgetdrivegeometry
    public static class Kernel32
    {
        public const DWORD
            DISK_BASE = 0x00000007,
            METHOD_BUFFERED = 0,
            FILE_ANY_ACCESS = 0;

        public const DWORD
            GENERIC_READ = 0x80000000,
            FILE_SHARE_WRITE = 0x2,
            FILE_SHARE_READ = 0x1,
            OPEN_EXISTING = 0x3;

        public enum MEDIA_TYPE : int
        {
            Unknown = 0,
            F5_1Pt2_512 = 1,
            F3_1Pt44_512 = 2,
            F3_2Pt88_512 = 3,
            F3_20Pt8_512 = 4,
            F3_720_512 = 5,
            F5_360_512 = 6,
            F5_320_512 = 7,
            F5_320_1024 = 8,
            F5_180_512 = 9,
            F5_160_512 = 10,
            RemovableMedia = 11,
            FixedMedia = 12,
            F3_120M_512 = 13,
            F3_640_512 = 14,
            F5_640_512 = 15,
            F5_720_512 = 16,
            F3_1Pt2_512 = 17,
            F3_1Pt23_1024 = 18,
            F5_1Pt23_1024 = 19,
            F3_128Mb_512 = 20,
            F3_230Mb_512 = 21,
            F8_256_128 = 22,
            F3_200Mb_512 = 23,
            F3_240M_512 = 24,
            F3_32M_512 = 25
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DiskGeometry
        {
            public LARGE_INTEGER Cylinders;
            public MEDIA_TYPE MediaType;
            public DWORD TracksPerCylinder;
            public DWORD SectorsPerTrack;
            public DWORD BytesPerSector;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DiskGeometryEx
        {
            public DiskGeometry Geometry;
            public LARGE_INTEGER DiskSize;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public byte[] Data;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern DWORD DeviceIoControl(
            SafeFileHandle hDevice,
            DWORD dwIoControlCode,
            LPVOID lpInBuffer,
            DWORD nInBufferSize,
            LPVOID lpOutBuffer,
            int nOutBufferSize,
            ref DWORD lpBytesReturned,
            LPOVERLAPPED lpOverlapped
        );

        public static DWORD CTL_CODE(DWORD DeviceType, DWORD Function, DWORD Method, DWORD Access)
        {
            return (((DeviceType) << 16) | ((Access) << 14) | ((Function) << 2) | (Method));
        }

        public static readonly DWORD DISK_GET_DRIVE_GEOMETRY_EX =
            CTL_CODE(DISK_BASE, 0x0028, METHOD_BUFFERED, FILE_ANY_ACCESS);

        public static readonly DWORD DISK_GET_DRIVE_GEOMETRY =
            CTL_CODE(DISK_BASE, 0, METHOD_BUFFERED, FILE_ANY_ACCESS);

        public static DiskGeometryEx GetDiskGeometryEx(SafeFileHandle hDevice)
        {
            if (null == hDevice || hDevice.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var dwIoControlCode = DISK_GET_DRIVE_GEOMETRY_EX;

            var nOutBufferSize = Marshal.SizeOf(typeof(DiskGeometryEx));
            var lpOutBuffer = Marshal.AllocHGlobal(nOutBufferSize);
            var lpBytesReturned = default(DWORD);
            var nullValue = LPSECURITY_ATTRIBUTES.Zero;

            var result =
                DeviceIoControl(
                    hDevice, dwIoControlCode,
                    nullValue, 0,
                    lpOutBuffer, nOutBufferSize,
                    ref lpBytesReturned, nullValue
                );

            if (0 == result)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var diskGeometryEx = (DiskGeometryEx)Marshal.PtrToStructure(lpOutBuffer, typeof(DiskGeometryEx));
            Marshal.FreeHGlobal(lpOutBuffer);

            return diskGeometryEx;
        }
        
        [DllImport("kernel32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetVolumePathNamesForVolumeNameW([MarshalAs(UnmanagedType.LPWStr)] string lpszVolumeName,
            [MarshalAs(UnmanagedType.LPWStr)] [Out] StringBuilder lpszVolumeNamePaths, uint cchBuferLength, 
            ref UInt32 lpcchReturnLength);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern LPSECURITY_ATTRIBUTES FindFirstVolume([Out] StringBuilder lpszVolumeName,
            uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FindNextVolume(IntPtr hFindVolume, [Out] StringBuilder lpszVolumeName, uint cchBufferLength);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint QueryDosDeviceW(string lpDeviceName, [Out] char[] lpTargetPath, uint ucchMax);

        public static void Volumes()
        {
            uint lpcchReturnLength = 0;
            var Max = 65535;
            var sbVolumeName = new StringBuilder(Max, Max);
            var sbPathName = new StringBuilder(Max, Max);
            var sbMountPoint = new StringBuilder(Max, Max);
            var volumeHandle = FindFirstVolume(sbVolumeName, (uint)Max);
            
            do {
                var volume = sbVolumeName.ToString();
                var unused = GetVolumePathNamesForVolumeNameW(volume, sbMountPoint, (uint)Max, ref lpcchReturnLength);
                var ReturnLength = QueryDosDevice(volume.Substring(4, volume.Length - 1 - 4), sbPathName, Max);
                if (ReturnLength > 0)
                {
                    var driveMapping = new
                    {
                        DriveLetter = sbMountPoint.ToString(),
                        VolumeName = volume,
                        DevicePath = sbPathName.ToString()
                    };

                    //Write-Output (New-Object PSObject -Property $DriveMapping)
                }
                else {
                    // No mountpoint found for: " + $volume
                } 
            } while (FindNextVolume(volumeHandle, sbVolumeName, (uint)Max));            
        }
        
    }
}