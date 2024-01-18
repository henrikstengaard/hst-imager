using System;

namespace Hst.Imager.Core.FileSystems.Ext;

/// <summary>
/// Incompatible feature set. If the kernel or e2fsck doesn't understand one of these bits, it will refuse to mount or attempt to repair the filesystem.
/// </summary>
[Flags]
public enum FeatureIncompatFlags
{
    /// <summary>
    /// Compression. Not implemented.
    /// </summary>
    INCOMPAT_COMPRESSION = 0x1,
    /// <summary>
    /// Directory entries record the file type. See ext4_dir_entry_2 below.
    /// </summary>
    INCOMPAT_FILETYPE = 0x2,
    /// <summary>
    /// Filesystem needs journal recovery.
    /// </summary>
    INCOMPAT_RECOVER = 0x4,
    /// <summary>
    /// Filesystem has a separate journal device.
    /// </summary>
    INCOMPAT_JOURNAL_DEV = 0x8,
    /// <summary>
    /// Meta block groups. See the earlier discussion of this feature.
    /// </summary>
    INCOMPAT_META_BG = 0x10,
    /// <summary>
    /// Files in this filesystem use extents.
    /// </summary>
    INCOMPAT_EXTENTS = 0x40,
    /// <summary>
    /// Enable a filesystem size over 2^32 blocks.
    /// </summary>
    INCOMPAT_64BIT = 0x80,
    /// <summary>
    /// Multiple mount protection. Prevent multiple hosts from mounting the filesystem concurrently by updating a reserved block periodically while mounted and checking this at mount time to determine if the filesystem is in use on another host.
    /// </summary>
    INCOMPAT_MMP = 0x100,
    /// <summary>
    /// Flexible block groups. See the earlier discussion of this feature.
    /// </summary>
    INCOMPAT_FLEX_BG = 0x200,
    /// <summary>
    /// Inodes can be used to store large extended attribute values.
    /// </summary>
    INCOMPAT_EA_INODE = 0x400,
    /// <summary>
    /// Data in directory entry. Allow additional data fields to be stored in each dirent, after struct ext4_dirent. The presence of extra data is indicated by flags in the high bits of ext4_dirent file type flags (above EXT4_FT_MAX). The flag EXT4_DIRENT_LUFID = 0x10 is used to store a 128-bit File Identifier for Lustre. The flag EXT4_DIRENT_IO64 = 0x20 is used to store the high word of 64-bit inode numbers. Feature still in development. (INCOMPAT_DIRDATA).
    /// </summary>
    INCOMPAT_DIRDATA = 0x1000,
    INCOMPAT_CSUM_SEED = 0x2000,
    INCOMPAT_LARGEDIR = 0x4000,
    INCOMPAT_INLINE_DATA = 0x8000,
    INCOMPAT_ENCRYPT = 0x10000,
}