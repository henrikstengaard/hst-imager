namespace HstWbInstaller.Imager.Core.Commands
{
    using System.Collections.Generic;

    public class MbrInfo
    {
        public string Path { get; set; }
        public long DiskSize { get; set; }
        public IEnumerable<MbrPartition> Partitions { get; set; }
        public int BlockSize { get; set; }
        public long Sectors { get; set; }
    }
}