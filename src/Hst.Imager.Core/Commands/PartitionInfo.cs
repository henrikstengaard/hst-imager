using System;

namespace Hst.Imager.Core.Commands
{
    public class PartitionInfo
    {
        public bool IsActive { get; set; }
        public bool IsPrimary { get; set; }
        public int PartitionNumber { get; set; }
        public string PartitionType { get; set; }
        public string FileSystem { get; set; }
        public long Size { get; set; }
        public long StartOffset { get; set; }
        public long EndOffset { get; set; }
        public long StartSector { get; set; }
        public long EndSector { get; set; }
        public ChsAddressInfo StartChs { get; set; }
        public ChsAddressInfo EndChs { get; set; }
        public long StartCylinder { get; set; }
        public long EndCylinder { get; set; }
        public int? BiosType { get; set; }
        public Guid? GuidType { get; set; }
        public long? VolumeSize { get; set; }
        public long? VolumeFree { get; set; }
    }
}