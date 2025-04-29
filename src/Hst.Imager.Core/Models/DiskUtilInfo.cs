namespace Hst.Imager.Core.Models
{
    using System.Collections.Generic;

    public class DiskUtilInfo
    {
        public enum DiskTypeEnum
        {
            Unknown,
            Physical,
            Virtual,
            Generic
        }
        
        public long DeviceBlockSize { get; set; }
        public string BusProtocol { get; set; }
        public string IoRegistryEntryName { get; set; }
        public long Size { get; set; }
        public string DeviceNode { get; set; }
        public string ParentWholeDisk { get; internal set; }
        public string MediaType { get; set; }
        public DiskTypeEnum DiskType { get; set; }
    }

    public class DiskUtilDisk
    {
        public string Content { get; set;}
        public string DeviceIdentifier { get; set; }
        public long Size { get; set; }
        public IEnumerable<DiskUtilPartition> Partitions { get; set; }

        public ApfsPhysicalStores ApfsPhysicalStores { get; set; }

        public DiskUtilDisk()
        {
            Partitions = new List<DiskUtilPartition>();
        }
    }

    public class ApfsPhysicalStores
    {
        public string DeviceIdentifier { get; set; }
    }
}