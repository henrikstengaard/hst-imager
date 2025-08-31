using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Hst.Imager.Core.Apis;

public static class WindowsDiskManager
{
    /// <summary>
    /// Get list of all mounted volumes on the system.
    /// </summary>
    /// <returns>List of mounted volumes \\\.\Volume{GUID}\.</returns>
    /// <exception cref="Win32Exception">Win32 error when trying to close the volume handle.</exception>
    public static string[] GetVolumes()
    {
        var volumes = new List<string>(10);
            
        const int bufferLength = 1024; // Buffer size for volume name
        var volumeName = new StringBuilder(bufferLength);

        var findHandle = Kernel32.FindFirstVolume(volumeName, bufferLength);
        if (findHandle == IntPtr.Zero)
        {
            // return empty array if no volumes found, if find first volume failed
            return [];
        }

        try
        {
            do
            {
                volumes.Add(volumeName.ToString());

                volumeName.Clear();
            } while (Kernel32.FindNextVolume(findHandle, volumeName, bufferLength));
        }
        finally
        {
            if (!Kernel32.FindVolumeClose(findHandle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to close the volume handle");
            }
        }
            
        return volumes.ToArray();
    }
    
    /// <summary>
    /// Get device instance from physical device number.
    /// </summary>
    /// <param name="physicalDeviceNumber">Physical device number to get the instance for.</param>
    /// <returns>Device instance identifier (DevInst) if found, otherwise 0.</returns>
    public static uint GetDeviceInstanceFromDeviceNumber(int physicalDeviceNumber)
    {
        var deviceInfoSet = IntPtr.Zero;

        try
        {
            var guid = SetupApi.GUID_DEVINTERFACE_DISK;

            // Get a handle to the device information set
            deviceInfoSet = SetupApi.SetupDiGetClassDevs(
                ref guid,
                IntPtr.Zero,
                IntPtr.Zero,
                (int)(SetupApi.DiGetClassFlags.DIGCF_PRESENT | SetupApi.DiGetClassFlags.DIGCF_DEVICEINTERFACE));

            if (deviceInfoSet == IntPtr.Zero)
            {
                //Console.WriteLine("Failed to get device information set. Error: " + Marshal.GetLastWin32Error());
                return 0;
            }

            // Enumerate device interfaces
            var deviceInterfaceData = new SetupApi.SP_DEVICE_INTERFACE_DATA();
            deviceInterfaceData.cbSize = Marshal.SizeOf(deviceInterfaceData);

            var memberIndex = 0;
            while (SetupApi.SetupDiEnumDeviceInterfaces(
                       deviceInfoSet,
                       IntPtr.Zero,
                       ref guid,
                       memberIndex,
                       ref deviceInterfaceData))
            {
                SetupApi.SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet,
                    ref deviceInterfaceData,
                    IntPtr.Zero,
                    0,
                    out var requiredSize,
                    IntPtr.Zero);

                if (requiredSize == 0)
                {
                    memberIndex++;
                    continue;
                }

                // Get the required size for the detail data
                var deviceInfoData = new SetupApi.SP_DEVINFO_DATA();
                deviceInfoData.cbSize = Marshal.SizeOf(deviceInfoData);

                var structSize = Marshal.SystemDefaultCharSize;
                if (IntPtr.Size == 8)
                    structSize += 6; // 64-bit systems, with 8-byte packing
                else
                    structSize += 4; // 32-bit systems, with byte packing

                // write the size of the structure to the buffer position 0 location of cbSize field
                var detailDataBuffer = Marshal.AllocHGlobal(requiredSize + structSize);
                Marshal.WriteInt32(detailDataBuffer, structSize);

                // Get the device interface detail using device interface data buffer
                var result = SetupApi.SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet,
                    ref deviceInterfaceData,
                    detailDataBuffer,
                    (uint)requiredSize,
                    out _,
                    ref deviceInfoData);

                if (!result)
                {
                    Marshal.FreeHGlobal(detailDataBuffer);

                    Console.WriteLine("Failed to get device interface detail. Error: " + Marshal.GetLastWin32Error() +
                                      ", requiredSize: " + requiredSize);

                    memberIndex++;
                    continue;
                }

                var devicePath = Marshal.PtrToStringAnsi(new IntPtr(detailDataBuffer.ToInt64() + 4));

                Marshal.FreeHGlobal(detailDataBuffer);

                //Console.WriteLine("devicePath: " + devicePath);
                
                int currentDriveDeviceNumber;
                using (var driveHandle = new Win32RawDisk(devicePath))
                {
                    if (driveHandle.IsInvalid())
                    {
                        memberIndex++;
                        continue;
                    }

                    currentDriveDeviceNumber = driveHandle.GetDeviceNumber();

                    if (currentDriveDeviceNumber == -1)
                    {
                        Console.WriteLine("Failed to get device number for device path: " + devicePath);
                        memberIndex++;
                        continue;
                    }
                }

                if (physicalDeviceNumber == currentDriveDeviceNumber)
                {
                    return deviceInfoData.DevInst;
                }

                memberIndex++;
            }

            if (Marshal.GetLastWin32Error() != 259) // ERROR_NO_MORE_ITEMS
            {
                Console.WriteLine("Failed to enumerate device interfaces. Error: " + Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            if (deviceInfoSet != IntPtr.Zero)
            {
                SetupApi.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
        }

        return 0;
    }
    
    /// <summary>
    /// Ejects a device by its device instance identifier (DevInst).
    /// </summary>
    /// <param name="devInst">Device instance identifier (DevInst) of the device to eject.</param>
    /// <returns>True if the device was successfully ejected, otherwise false.</returns>
    public static bool EjectDevice(uint devInst)
    {
        // get drives's parent, e.g. the USB bridge, the SATA port, an IDE channel with two drives!
        var devInstParent = 0;
        SetupApi.CM_Get_Parent( ref devInstParent, ( int ) devInst, 0 );

        //Console.WriteLine("Ejecting drive with DevInst: " + devInst + ", Parent: " + devInstParent);

        // eject device with 5 attempts, waiting 500ms between each attempt
        for (var i = 1; i <= 5; i++)
        {
            var success = SetupApi.CM_Request_Device_Eject_NoUi( devInstParent, IntPtr.Zero, null, 0, 0 );
            //Console.WriteLine("result of CM_Request_Device_Eject_NoUi: " + success);
            
            if (success == 0)
            {
                return true;
            }
            
            Thread.Sleep(500);
        }

        return false;
    }

    public static bool RescanDrives()
    {
        try
        {
            var processInfo = new ProcessStartInfo("diskpart.exe", "/s test.txt")
            {
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(processInfo);

            if (process == null)
            {
                return false;
            }
            
            process.StandardInput.WriteLine("rescan");
            process.StandardInput.WriteLine("exit");
            
            return true;
        }
        catch (Exception e)
        {
            throw new IOException("Failed to rescan drives", e);
        }
    }
}