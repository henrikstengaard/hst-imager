namespace Hst.Imager.Core.FileSystems.Fat;

public class FatLongEntry : IFatEntry
{
    /// <summary>
    /// Fat entry offset.
    /// </summary>
    public long Offset { get; set; }

    /// <summary>
    /// The order of the entry in the sequence of long directory entry set.
    /// If it's masked with 0x40 (LAST_LONG_ENTRY) it indicates the entry is the last long directory entry of a set.
    /// A valid set of long directory entries must begin with an entry having this mask.
    /// </summary>
    public byte Order { get; set; }

    /// <summary>
    /// Unicode name characters 1-5 of long directory entry.
    /// </summary>
    public byte[] Name1 { get; set; }

    /// <summary>
    /// Attribute. Must be ATTR_LONG_NAME
    /// </summary>
    public byte Attribute { get; set; }
    
    /// <summary>
    /// Type of directory entry. Zero, if directory entry, non-zero for other directory entry types. 
    /// </summary>
    public byte Type { get; set; }
    
    /// <summary>
    /// Checksum of the short dir entry at the end of long dir set.
    /// </summary>
    public byte Checksum { get; set; }

    /// <summary>
    /// Unicode name characters 6-11 of long directory entry.
    /// </summary>
    public byte[] Name2 { get; set; }

    /// <summary>
    /// Low first cluster. Must be 0 and present for disk utility compatibility.
    /// </summary>
    public ushort LowFirstCluster { get; set; }

    /// <summary>
    /// Unicode name characters 12-13 of long directory entry.
    /// </summary>
    public byte[] Name3 { get; set; }
}