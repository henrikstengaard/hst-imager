namespace Hst.Imager.Core.Commands;

public class DiskGeometryInfo
{
    public long Capacity { get; set; }
    public long TotalSectors { get; set; }
    public int BytesPerSector { get; set; }
    public int HeadsPerCylinder { get; set; }
    public int Cylinders { get; set; }
    public int SectorsPerTrack { get; set; }
}