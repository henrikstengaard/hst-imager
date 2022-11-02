namespace Hst.Imager.Core.PhysicalDrives
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Apis;

    public class WindowsPhysicalDrive : GenericPhysicalDrive
    {
        public readonly IEnumerable<string> DriveLetters;

        public WindowsPhysicalDrive(string path, string type, string model, long size, IEnumerable<string> driveLetters) : base(
            path, type, model, size)
        {
            this.DriveLetters = driveLetters;
        }

        public override Stream Open()
        {
            var driveLetters = DriveLetters.Select(driveLetter => @"\\.\" + driveLetter + @"").ToList();

            var dismountedDrives = new List<Win32RawDisk>();
            
            foreach (var driveLetter in driveLetters)
            {
                var win32RawDisk = new Win32RawDisk(driveLetter, true);
                if (!win32RawDisk.LockDevice())
                {
                    win32RawDisk.CloseDevice();
                    throw new IOException($"Failed to lock device '{driveLetter}'");
                }

                if (!win32RawDisk.DismountDevice())
                {
                    win32RawDisk.UnlockDevice();
                    win32RawDisk.CloseDevice();
                    throw new IOException($"Failed to dismount device '{driveLetter}'");
                }
                
                dismountedDrives.Add(win32RawDisk);
            }

            return new SectorStream(new WindowsPhysicalDriveStream(Path, Size, Writable, dismountedDrives), true, true);
        }
    }
}