namespace Hst.Imager.Core.Apis;

public class DiskExtendsResult
{
    public uint DiskNumber { get; set; }
    public long StartingOffset { get; set; }
    public long ExtentLength { get; set; }
}