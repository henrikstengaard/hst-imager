namespace Hst.Imager.Core.PhysicalDrives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Apis;
    using Hst.Imager.Core.Commands;

    public class WindowsPhysicalDrive : GenericPhysicalDrive
    {
        public readonly string BusType;
        public readonly List<string> DriveLetters;
        private bool scanDriveLetters;

        public WindowsPhysicalDrive(string path, string type, string busType, string name, long size, bool removable,
            IEnumerable<string> driveLetters) : base(path, type, name, size, removable)
        {
            this.BusType = busType;
            this.DriveLetters = driveLetters.ToList();
            this.scanDriveLetters = false;
        }

        public override Stream Open()
        {
            if (scanDriveLetters)
            {
                ScanDriveLetters();
            }

            var driveLetters = DriveLetters.Select(driveLetter => @"\\.\" + driveLetter + @"").ToList();

            var dismountedDrives = new List<Win32RawDisk>();
            
            foreach (var driveLetter in driveLetters)
            {
                // open win32 disk for each drive letter
                var win32RawDisk = new Win32RawDisk(driveLetter, true);
                
                // lock device (ignored, if fails)
                win32RawDisk.LockDevice();

                // dismount device
                if (!win32RawDisk.DismountDevice())
                {
                    win32RawDisk.UnlockDevice();
                    win32RawDisk.Dispose();
                    throw new IOException($"Failed to dismount device '{driveLetter}'");
                }
                
                // add win32 raw disk to dismounted drives for unlocking then disposing windows physical drive stream
                dismountedDrives.Add(win32RawDisk);
            }

            driveLetters.Clear();

            // physical drive is closed and has possible changes to drive letters, if it has been partitioned and formatted
            scanDriveLetters = true;

            return new SectorStream(new WindowsPhysicalDriveStream(Path, Size, Writable, dismountedDrives), byteSwap: ByteSwap, leaveOpen: true);
        }

        private void ScanDriveLetters()
        {
            var physicalDrivePathMatch = Regexs.PhysicalDrivePathRegex.Match(Path);

            if (!physicalDrivePathMatch.Success)
            {
                return;
            }

            var physicalDriveNumber = uint.TryParse(physicalDrivePathMatch.Groups[2].Value, out var parsedDriveNumber)
                ? parsedDriveNumber
                : uint.MaxValue;

            DriveLetters.Clear();

            var drives = DriveInfo.GetDrives().ToList();
            foreach (var drive in drives)
            {
                if (drive.DriveType == DriveType.CDRom)
                {
                    continue;
                }

                try
                {
                    var driveName = drive.Name[..2];
                    var drivePath = $"\\\\.\\{driveName}";

                    using var win32RawDisk = new Win32RawDisk(drivePath, false, true);
                    if (win32RawDisk.IsInvalid())
                    {
                        continue;
                    }

                    var diskExtendsResult = win32RawDisk.DiskExtends();

                    if (physicalDriveNumber != diskExtendsResult.DiskNumber)
                    {
                        continue;
                    }

                    DriveLetters.Add(driveName);
                }
                catch (Exception)
                {
                    // ignore
                }
            }
        }
    }
}