namespace Hst.Imager.Core.Commands
{
    public class PartitionInfo
    {
        public int PartitionNumber { get; set; }
        public string PartitionType { get; set; }
        public string FileSystem { get; set; }
        public long Size { get; set; }
        public long StartOffset { get; set; }
        public long EndOffset { get; set; }
        public long StartSector { get; set; }
        public long EndSector { get; set; }
        public long StartCylinder { get; set; }
        public long EndCylinder { get; set; }
        public string BiosType { get; set; }
        public string GuidType { get; set; }
    }
}