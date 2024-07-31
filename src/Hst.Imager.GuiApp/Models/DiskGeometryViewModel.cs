namespace Hst.Imager.GuiApp.Models;

public class DiskGeometryViewModel
{
    public int BytesPerSector { get; set; }
    public int Cylinders { get; set; }
    public long Capacity { get; set; }
    public int HeadsPerCylinder { get; set; }
    public int SectorsPerTrack { get; set; }
    public long TotalSectors { get; set; }
}