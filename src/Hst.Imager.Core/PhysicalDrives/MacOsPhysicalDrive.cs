namespace Hst.Imager.Core.PhysicalDrives
{
    using System.Collections.Generic;
    using System.IO;
    using Hst.Core.Extensions;

    public class MacOsPhysicalDrive : GenericPhysicalDrive
    {
        public readonly IEnumerable<string> PartitionDevices;

        public MacOsPhysicalDrive(string path, string type, string name, long size, IEnumerable<string> partitionDevices) : base(
            path, type, name, size)
        {
            this.PartitionDevices = partitionDevices;
        }

        public override Stream Open()
        {
            "diskutil".RunProcess($"unmountDisk {Path}");
            return new MacOsMediaStream(File.Open(Path, FileMode.Open, Writable ? FileAccess.ReadWrite : FileAccess.Read),
                Path, Size);
        }
    }
}