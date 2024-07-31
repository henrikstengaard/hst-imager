namespace Hst.Imager.GuiApp.Models;

using Core.Commands;

public class PartViewModel
{
    public string FileSystem { get; set; }
    public int? PartitionNumber { get; set; }
    public PartitionTableType PartitionTableType { get; set; }
    public PartType PartType { get; set; }
    public long Size { get; set; }
    public long StartOffset { get; set; }
    public long EndOffset { get; set; }
    public long StartSector { get; set; }
    public long EndSector { get; set; }
    public long StartCylinder { get; set; }
    public long EndCylinder { get; set; }
    public double PercentSize { get; set; }
    public string PartitionType { get; set; }
    public ChsAddress StartChs { get; set; }
    public ChsAddress EndChs { get; set; }
    public string BiosType { get; set; }
    public string GuidType { get; set; }
    public bool IsActive { get; set; }
    public bool IsPrimary { get; set; }
}