namespace Hst.Imager.Core.Apis;

public class DiskGeometryResult
{
    public string MediaType { get; set; }
    public long Cylinders { get; set; }
    public uint TracksPerCylinder { get; set; }
    public uint SectorsPerTrack { get; set; }
    public uint BytesPerSector { get; set; }
}