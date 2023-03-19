namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;

public class PartitionTablePart
{
    public string Path { get; set; }
    public PartitionTableType PartitionTableType { get; set; }
    public long Size { get; set; }
    public long Sectors { get; set; }
    public long Cylinders { get; set; }
    public IEnumerable<PartInfo> Parts { get; set; }
}