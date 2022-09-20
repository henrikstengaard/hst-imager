namespace Hst.Imager.Core.Commands
{
    public class MbrPartition
    {
        public string Type { get; set; }
        public long FirstSector { get; set; }
        public long LastSector { get; set; }
        public long PartitionSize { get; set; }
    }
}