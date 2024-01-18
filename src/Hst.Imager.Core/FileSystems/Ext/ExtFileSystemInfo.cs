using System;
using System.Collections.Generic;

namespace Hst.Imager.Core.FileSystems.Ext;

public class ExtFileSystemInfo
{
    [Flags]
    public enum ExtFeature : uint
    {
        /// <summary>
        /// filetype ext2, 2.2.0
        /// </summary>
        FileType,
        /// <summary>
        /// sparse_super ext2, 2.2.0
        /// </summary>
        SparseSuper,
        /// <summary>
        /// large_file ext2, 2.2.0
        /// </summary>
        LargeFile,
        /// <summary>
        /// has_journal ext3, 2.4.15
        /// </summary>
        HasJournal,
        /// <summary>
        /// ext_attr ext2/ext3, 2.6.0
        /// </summary>
        ExtAttr,
        /// <summary>
        /// dir_index ext3, 2.6.0
        /// </summary>
        DirIndex,
        /// <summary>
        /// resize_inode ext3, 2.6.10 (online resizing)
        /// </summary>
        ResizeInode,
        /// <summary>
        /// 64bit ext4, 2.6.28
        /// </summary>
        Bit64,
        /// <summary>
        /// dir_nlink ext4, 2.6.28
        /// </summary>
        DirNLink,
        /// <summary>
        /// extent ext4, 2.6.28
        /// </summary>
        Extent,
        /// <summary>
        /// extra_isize ext4, 2.6.28
        /// </summary>
        ExtraIsize,
        /// <summary>
        /// flex_bg ext4, 2.6.28
        /// </summary>
        FlexBg,
        /// <summary>
        /// huge_file ext4, 2.6.28
        /// </summary>
        HugeFile,
        /// <summary>
        /// meta_bg ext4, 2.6.28
        /// </summary>
        MetaBg,
        /// <summary>
        /// uninit_bg ext4, 2.6.28
        /// </summary>
        UninitBg,
        /// <summary>
        /// mmp ext4, 3.0
        /// </summary>
        Mmp,
        /// <summary>
        /// bigalloc ext4, 3.2
        /// </summary>
        BigAlloc,
        /// <summary>
        /// quota ext4, 3.6
        /// </summary>
        Quota,
        /// <summary>
        /// inline_data ext4, 3.8
        /// </summary>
        InlineData,
        /// <summary>
        /// sparse_super2 ext4, 3.16
        /// </summary>
        SparseSuper2,
        /// <summary>
        /// metadata_csum ext4, 3.18
        /// </summary>
        MetadataCsum,
        /// <summary>
        /// encrypt ext4, 4.1
        /// </summary>
        Encrypt,
        /// <summary>
        /// metadata_csum_seed ext4, 4.4
        /// </summary>
        MetadataCsumSeed,  
        /// <summary>
        /// project ext4, 4.5
        /// </summary>
        Project,
        /// <summary>
        /// ea_inode ext4, 4.13
        /// </summary>
        EaInode,
        /// <summary>
        /// large_dir ext4, 4.13
        /// </summary>
        LargeDir,
        /// <summary>
        /// casefold ext4, 5.2
        /// </summary>
        CaseFold,
        /// <summary>
        /// verity ext4, 5.4
        /// </summary>
        Verity,
        /// <summary>
        /// stable_inodes ext4, 5.5
        /// </summary>
        StableInodes
    }

    public enum ExtVersion
    {
        Ext2,
        Ext3,
        Ext4
    }
    
    public ExtVersion Version { get; set; }

    public IEnumerable<ExtFeature> Features { get; set; }
    public ulong Size { get; set; }
    public ulong Free { get; set; }
}