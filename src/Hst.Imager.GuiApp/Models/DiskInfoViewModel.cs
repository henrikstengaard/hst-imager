namespace Hst.Imager.GuiApp.Models;

using System.Collections.Generic;
using Core.Commands;

public class DiskInfoViewModel
{
    public string Name { get; set; }
    public long Size { get; set; }
    public IEnumerable<PartitionTableInfo> PartitionTables { get; set; }
    public long StartOffset { get; set; }
    public long EndOffset { get; set; }
    public string Path { get; set; }
    public RigidDiskBlockViewModel RigidDiskBlock { get; set; }
    public IEnumerable<PartViewModel> DiskParts { get; set; }
    public PartitionTablePartViewModel GptPartitionTablePart { get; set; }
    public PartitionTablePartViewModel MbrPartitionTablePart { get; set; }
    public PartitionTablePartViewModel RdbPartitionTablePart { get; set; }

    public DiskInfoViewModel()
    {
        DiskParts = new List<PartViewModel>();
    }
}