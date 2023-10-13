namespace Hst.Imager.Core.FileSystems.Fat32;

public class Fat32BootSector
{
    public byte[] SectorBytes { get; set; }
    
    // Common fields.
    public byte[] sJmpBoot = new byte[3];
    public byte[] sOEMName = new byte[8];
    public ushort wBytsPerSec;
    public byte bSecPerClus;
    public ushort wRsvdSecCnt;
    public byte bNumFATs;
    public ushort wRootEntCnt;
    public ushort wTotSec16; // if zero, use dTotSec32 instead
    public byte bMedia;
    public ushort wFATSz16;
    public ushort wSecPerTrk;
    public ushort wNumHeads;
    
    /// <summary>
    /// Count of hidden sectors preceding the partition that contains this FAT volume.
    /// This field should always be zero on media that are not partitioned.
    /// </summary>
    public uint dHiddSec;
    public uint dTotSec32;
    // Fat 32/16 only
    public uint dFATSz32;
    public ushort wExtFlags;
    public ushort wFSVer;
    public uint dRootClus;
    public ushort wFSInfo;
    public ushort wBkBootSec;
    public byte[] Reserved = new byte[12];
    public byte bDrvNum;
    public byte Reserved1;
    public byte bBootSig; // == 0x29 if next three fields are ok
    public uint dBS_VolID;
    public byte[] sVolLab = new byte[11];
    public byte[] sBS_FilSysType = new byte[8];
    public byte[] ExecutableCode = new byte[420];
    public byte[] BootRecordSignature = new byte[2];
}