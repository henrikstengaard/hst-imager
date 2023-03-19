namespace Hst.Imager.Core.Commands
{
    using System.Collections.Generic;
    using Amiga.RigidDiskBlocks;

    public class DiskInfo
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public IEnumerable<PartitionTableInfo> PartitionTables { get; set; }
        public long StartOffset { get; set; }
        public long EndOffset { get; set; }
        public string Path { get; set; }
        public RigidDiskBlock RigidDiskBlock { get; set; }
        public IEnumerable<PartInfo> DiskParts { get; set; }
        public PartitionTablePart MbrPartitionTablePart { get; set; }
        public PartitionTablePart RdbPartitionTablePart { get; set; }
        public PartitionTablePart GptPartitionTablePart { get; set; }
    }
}