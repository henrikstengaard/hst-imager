namespace Hst.Imager.Core.PhysicalDrives
{
    using System.Collections.Generic;
    using System.IO;
    using Hst.Core.Extensions;

    public class MacOsPhysicalDrive : GenericPhysicalDrive
    {
        public readonly IEnumerable<string> PartitionDevices;

        public MacOsPhysicalDrive(string path, string type, string name, long size, bool removable,
            IEnumerable<string> partitionDevices) : base(path, type, name, size, removable)
        {
            this.PartitionDevices = partitionDevices;
        }

        public override Stream Open()
        {
            // use diskutil to unmount disk, force required if disk has multiple mounted partitions
            "diskutil".RunProcess($"unmountDisk force {Path}");
            
            return new MacOsMediaStream(File.Open(Path, FileMode.Open, Writable ? FileAccess.ReadWrite : FileAccess.Read),
                Path, Size);
        }
    }
}