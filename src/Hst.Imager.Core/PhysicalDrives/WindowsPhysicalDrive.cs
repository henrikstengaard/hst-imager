namespace Hst.Imager.Core.PhysicalDrives
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Apis;

    public class WindowsPhysicalDrive : GenericPhysicalDrive
    {
        public readonly string BusType;
        public readonly IEnumerable<string> DriveLetters;

        public WindowsPhysicalDrive(string path, string type, string busType, string name, long size, IEnumerable<string> driveLetters) : base(
            path, type, name, size)
        {
            this.BusType = busType;
            this.DriveLetters = driveLetters;
        }

        public override Stream Open()
        {
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

            return new SectorStream(new WindowsPhysicalDriveStream(Path, Size, Writable, dismountedDrives), byteSwap: ByteSwap, leaveOpen:true);
        }
    }
}