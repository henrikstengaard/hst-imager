namespace HstWbInstaller.Imager.Core.Commands
{
    using System.Collections.Generic;

    public class DiskInfo
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public IEnumerable<PartitionTableInfo> PartitionTables { get; set; }
        public long StartOffset { get; set; }
        public long EndOffset { get; set; }
        public string Path { get; set; }
    }
}