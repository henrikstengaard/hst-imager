namespace Hst.Imager.Core.PhysicalDrives
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Apis;

    public class WindowsPhysicalDrive : GenericPhysicalDrive
    {
        public readonly IEnumerable<string> DriveLetters;

        public WindowsPhysicalDrive(string path, string type, string name, long size, IEnumerable<string> driveLetters) : base(
            path, type, name, size)
        {
            this.DriveLetters = driveLetters;
        }

        public override Stream Open()
        {
            var driveLetters = DriveLetters.Select(driveLetter => @"\\.\" + driveLetter + @"").ToList();

            var dismountedDrives = new List<Win32RawDisk>();
            
            foreach (var driveLetter in driveLetters)
            {
                // open win32 disk for each drive letter, lock device and dismount
                var win32RawDisk = new Win32RawDisk(driveLetter, true);
                if (!win32RawDisk.LockDevice())
                {
                    win32RawDisk.Dispose();
                    throw new IOException($"Failed to lock device '{driveLetter}'");
                }

                if (!win32RawDisk.DismountDevice())
                {
                    win32RawDisk.UnlockDevice();
                    win32RawDisk.Dispose();
                    throw new IOException($"Failed to dismount device '{driveLetter}'");
                }
                
                // add win32 raw disk to dismounted drives for unlocking then disposing windows physical drive stream
                dismountedDrives.Add(win32RawDisk);
            }

            return new SectorStream(new WindowsPhysicalDriveStream(Path, Size, Writable, dismountedDrives));
        }
    }
}