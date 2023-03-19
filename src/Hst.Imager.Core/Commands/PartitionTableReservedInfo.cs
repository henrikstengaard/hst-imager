namespace Hst.Imager.Core.Commands;

public class PartitionTableReservedInfo
{
    public long Size { get; set; }
    public long StartOffset { get; set; }
    public long EndOffset { get; set; }
    public long StartSector { get; set; }
    public long EndSector { get; set; }
    public long StartCylinder { get; set; }
    public long EndCylinder { get; set; }
}