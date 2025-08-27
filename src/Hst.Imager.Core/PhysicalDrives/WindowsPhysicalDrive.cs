namespace Hst.Imager.Core.PhysicalDrives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Apis;
    using Commands;

    public class WindowsPhysicalDrive(
        int physicalDriveNumber,
        string path,
        string type,
        string busType,
        string name,
        long size,
        bool removable,
        bool systemDrive,
        string[] driveLetters)
        : GenericPhysicalDrive(path, type, name, size, removable: removable,
            systemDrive: systemDrive)
    {
        public readonly int PhysicalDriveNumber = physicalDriveNumber;
        public readonly string BusType = busType;

        public override Stream Open()
        {
            if (SystemDrive)
            {
                throw new IOException($"Access to system drive path '{Path}' is not supported!");
            }

            var dismountedDrives = new List<Win32RawDisk>();

            var physicalDeviceNumber = GetPhysicalDriveNumber(Path);

            dismountedDrives.AddRange(LockAndDismountVolumes(physicalDeviceNumber));
            dismountedDrives.AddRange(LockAndDismountDriveLetters(physicalDeviceNumber, driveLetters));

            return new SectorStream(new WindowsPhysicalDriveStream(Path, Size, Writable, dismountedDrives),
                byteSwap: ByteSwap, leaveOpen: false);
        }

        private static int GetPhysicalDriveNumber(string path)
        {
            var physicalDrivePathMatch = Regexs.PhysicalDrivePathRegex.Match(path);

            if (!physicalDrivePathMatch.Success ||
                !int.TryParse(physicalDrivePathMatch.Groups[2].Value, out var physicalDriveNumber))
            {
                throw new IOException($"Invalid physical drive path '{path}'");
            }

            return physicalDriveNumber;
        }

        /// <summary>
        /// Lock and dismount all volumes for given physical drive number.
        /// </summary>
        /// <param name="physicalDriveNumber">Physical drive number.</param>
        /// <returns>List of dismounted Win32RawDisk volumes.</returns>
        private static IList<Win32RawDisk> LockAndDismountVolumes(int physicalDriveNumber)
        {
            var dismountedDrives = new List<Win32RawDisk>(10);
            
            var volumes = WindowsDiskManager.GetVolumes();

            foreach (var volume in volumes)
            {
                Win32RawDisk win32RawDisk = null;

                try
                {
                    win32RawDisk = new Win32RawDisk(volume.TrimEnd('\\'), true);
                    if (win32RawDisk.IsInvalid())
                    {
                        continue;
                    }

                    var deviceNumber = win32RawDisk.GetDeviceNumber();
                    
                    if (deviceNumber != physicalDriveNumber)
                    {
                        // dispose and continue, if device number doesn't match physical drive number
                        win32RawDisk.Dispose();
                        continue;
                    }

                    win32RawDisk.LockDevice();

                    if (!win32RawDisk.DismountDevice())
                    {
                        win32RawDisk.UnlockDevice();
                    }
                
                    dismountedDrives.Add(win32RawDisk);
                }
                catch (Exception)
                {
                    // dispose, if lock and dismount drive fails
                    win32RawDisk?.Dispose();
                }
            }

            return dismountedDrives;
        }

        /// <summary>
        /// Lock and dismount all given drive letters for given physical drive number.
        /// </summary>
        /// <param name="physicalDriveNumber">Physical drive number.</param>
        /// <param name="driveLetters">Drive letters to lock and dismount, e.g. C:, D:.</param>
        /// <returns>List of dismounted Win32RawDisk drive letters.</returns>
        private static IList<Win32RawDisk> LockAndDismountDriveLetters(int physicalDriveNumber, string[] driveLetters)
        {
            var dismountedDrives = new List<Win32RawDisk>(10);
            
            foreach (var driveLetter in driveLetters)
            {
                var drivePath = $"\\\\.\\{driveLetter}";
                Win32RawDisk win32RawDisk = null;

                try
                {
                    win32RawDisk = new Win32RawDisk(drivePath, false, true);
                    if (win32RawDisk.IsInvalid())
                    {
                        continue;
                    }

                    var deviceNumber = win32RawDisk.GetDeviceNumber();

                    if (deviceNumber != physicalDriveNumber)
                    {
                        win32RawDisk.Dispose();
                        continue;
                    }

                    win32RawDisk.LockDevice();

                    if (!win32RawDisk.DismountDevice())
                    {
                        win32RawDisk.UnlockDevice();
                    }
                    
                    dismountedDrives.Add(win32RawDisk);
                }
                catch (Exception)
                {
                    // dispose, if lock and dismount drive fails
                    win32RawDisk?.Dispose();
                }
            }

            return dismountedDrives;
        }
    }
}