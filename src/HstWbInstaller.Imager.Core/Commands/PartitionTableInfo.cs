namespace HstWbInstaller.Imager.Core.Commands
{
    using System.Collections.Generic;

    public class PartitionTableInfo
    {
        public enum PartitionTableType
        {
            MasterBootRecord,
            RigidDiskBlock
        }

        public long Size { get; set; }
        public PartitionTableType Type { get; set; }
        public IEnumerable<PartitionInfo> Partitions { get; set; }
        public long StartOffset { get; set; }
        public long EndOffset { get; set; }
    }
}