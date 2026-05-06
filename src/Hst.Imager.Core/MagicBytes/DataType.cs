namespace Hst.Imager.Core.MagicBytes;

public enum DataType
{
    Unknown,
    Vhd,

    /// <summary>
    /// Rigid Disk Block.
    /// </summary>
    Rdb,
    Iso9660,
    Adf,

    /// <summary>
    /// Master Boot Record.
    /// </summary>
    Mbr,
    
    /// <summary>
    /// Guid Partition Table.
    /// </summary>
    Gpt,
    Lha,
    Lzx,
    Hunk,
    Lzw,
    Zip,
    Xz,
    
    /// <summary>
    /// Gzip compressed. 
    /// </summary>
    Gzip,
    Rar
}