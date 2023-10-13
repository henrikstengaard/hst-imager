namespace Hst.Imager.Core.FileSystems.Fat32;

public class FatFsInfo
{
    public byte[] SectorBytes { get; set; }
    
    public uint dLeadSig;         // 0x41615252
    public byte[] sReserved1 = new byte[480];   // zeros
    public uint dStrucSig;        // 0x61417272
    public uint dFree_Count;      // 0xFFFFFFFF
    public uint dNxt_Free;        // 0xFFFFFFFF
    public byte[] sReserved2 = new byte[12];    // zeros
    public uint dTrailSig;     // 0xAA550000
    public byte[] BootRecordSignature = new byte[2];
}