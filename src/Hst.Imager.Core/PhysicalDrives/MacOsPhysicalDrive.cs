using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.PhysicalDrives
{
    using System.Collections.Generic;
    using System.IO;
    using Hst.Core.Extensions;

    public class MacOsPhysicalDrive : GenericPhysicalDrive
    {
        public readonly IEnumerable<string> PartitionDevices;

        public MacOsPhysicalDrive(string path, string type, string name, long size, bool removable,
            bool systemDrive, IEnumerable<string> partitionDevices)
            : base(path, type, name, size, removable: removable, systemDrive: systemDrive)
        {
            PartitionDevices = partitionDevices;
        }

        public override Stream Open(bool useCache, CacheType cacheType, int blockSize)
        {
            if (SystemDrive)
            {
                throw new IOException($"Access to system drive path '{Path}' is not supported!");
            }
            
            // use diskutil to unmount disk, force required if disk has multiple mounted partitions
            "diskutil".RunProcess($"unmountDisk force {Path}");
            
            return new MacOsMediaStream(File.Open(Path, FileMode.Open, Writable ? FileAccess.ReadWrite : FileAccess.Read),
                Path, Size);
        }
    }
}