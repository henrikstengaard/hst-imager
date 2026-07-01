using System;

namespace Hst.Imager.Core.FileSystems.Fat;

public class FatEntry : IFatEntry
{
    /// <summary>
    /// Fat entry offset.
    /// </summary>
    public long Offset { get; set; }
    
    /// <summary>
    /// Short name.
    /// </summary>
    public byte[] Name { get; set; }

    /// <summary>
    /// File attributes.
    /// </summary>
    public byte Attribute { get; set; }
    
    /// <summary>
    /// Reserved for use by Windows NT. Must be 0.
    /// </summary>
    public byte Reserved { get; set; }
    
    /// <summary>
    /// Date and time when file is created.
    /// </summary>
    public DateTime CreatedDate { get; set; }
    
    /// <summary>
    /// Date of time when file is last modified.
    /// This is both when created and when written to.
    /// </summary>
    public DateTime ModifiedDate { get; set; }
    
    /// <summary>
    /// Low word of entry's first cluster.
    /// </summary>
    public ushort LowFirstCluster { get; set; }
    
    /// <summary>
    /// High word of entry's first cluster. Must be 0 for FAT12/FAT16 volume.
    /// </summary>
    public ushort HighFirstCluster { get; set; }
    
    /// <summary>
    /// File size in bytes.
    /// </summary>
    public uint Size { get; set; }
    
    /// <summary>
    /// Last access date of last read or write.
    /// This doesn't contain time, only a date.
    /// When written to, this should be same as modified date.
    /// </summary>
    public DateTime? LastAccessDate { get; set; }
}