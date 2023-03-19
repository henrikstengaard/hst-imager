namespace Hst.Imager.Core.Commands;

public class PartInfo
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
}