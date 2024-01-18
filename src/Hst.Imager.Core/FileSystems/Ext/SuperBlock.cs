namespace Hst.Imager.Core.FileSystems.Ext;

public class SuperBlock
{
    /// <summary>
    /// Inodes count (le32, s_inodes_count, 0x0)
    /// </summary>
    public uint SInodesCount { get; set; }

    /// <summary>
    /// Blocks count (le32, s_blocks_count_lo, 0x4)
    /// </summary>
    public uint SBlocksCountLo { get; set; }

    /// <summary>
    /// Reserved blocks count (le32, s_r_blocks_count_lo, 0x8)
    /// </summary>
    public uint SrBlocksCountLo { get; set; }

    /// <summary>
    /// Free blocks count (le32, s_free_blocks_count_lo, 0xc)
    /// </summary>
    public uint SFreeBlocksCountLo { get; set; }

    /// <summary>
    /// Free inodes count (le32, s_free_inodes_count, 0x10)
    /// </summary>
    public uint SFreeInodesCount { get; set; }

    /// <summary>
    /// First Data Block (le32, s_first_data_block, 0x14)
    /// </summary>
    public uint SFirstDataBlock { get; set; }
    
    /// <summary>
    /// Block size (le32, s_log_block_size, 0x18) 
    /// </summary>
    public uint SLogBlockSize { get; set; }
    
    /// <summary>
    /// Magic signature 0xEF53 (le16, s_magic, 0x38)
    /// </summary>
    public ushort Magic { get; set; }

    /// <summary>
    /// Compatible feature set (le32, s_feature_compat, 0x5c)
    /// </summary>
    public uint SFeatureCompat { get; set; }
    
    /// <summary>
    /// Incompatible feature set (le32, s_feature_incompat, 0x60)
    /// </summary>
    public uint SFeatureIncompat { get; set; }

    /// <summary>
    /// Readonly-compatible feature set (le32, s_feature_ro_compat, 0x64)
    /// </summary>
    public uint SFeatureRoCompat { get; set; }
    
    /// <summary>
    /// Blocks count 64-bit (le32, s_blocks_count_hi, 0x150)
    /// </summary>
    public uint SBlocksCountHi { get; set; }

    /// <summary>
    /// Reserved blocks count 64-bit (le32, s_r_blocks_count_hi, 0x154)
    /// </summary>
    public uint SrBlocksCountHi { get; set; }

    /// <summary>
    /// Free blocks count 64-bit (le32, s_free_blocks_count_hi, 0x158)
    /// </summary>
    public uint SFreeBlocksCountHi { get; set; }
}