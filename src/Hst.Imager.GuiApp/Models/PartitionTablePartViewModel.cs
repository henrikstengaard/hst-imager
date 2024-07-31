namespace Hst.Imager.GuiApp.Models;

using System.Collections.Generic;
using Core.Commands;

public class PartitionTablePartViewModel
{
    public string Path { get; set; }
    public DiskGeometryViewModel DiskGeometry { get; set; }
    public PartitionTableType PartitionTableType { get; set; }
    public long Size { get; set; }
    public long Sectors { get; set; }
    public long Cylinders { get; set; }
    public IEnumerable<PartViewModel> Parts { get; set; }
}