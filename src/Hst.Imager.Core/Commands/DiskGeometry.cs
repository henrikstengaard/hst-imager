namespace Hst.Imager.Core.Commands;

public class DiskGeometry
{
    public long DiskSize { get; set; }
    public int Cylinders { get; set; }
    public int Heads { get; set; }
    public int Sectors { get; set; }
}