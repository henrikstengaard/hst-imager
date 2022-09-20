namespace Hst.Imager.Core.Commands
{
    public class PartitionInfo
    {
        public int PartitionNumber { get; set; }
        public string Type { get; set; }
        public long Size { get; set; }
        public long StartOffset { get; set; }
        public long EndOffset { get; set; }
    }
}