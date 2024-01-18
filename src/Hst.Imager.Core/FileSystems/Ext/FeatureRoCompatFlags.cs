using System;

namespace Hst.Imager.Core.FileSystems.Ext;

/// <summary>
/// Readonly-compatible feature set. If the kernel doesn't understand one of these bits, it can still mount read-only, but e2fsck will refuse to modify the filesystem.
/// </summary>
[Flags]
public enum FeatureRoCompatFlags
{
    /// <summary>
    /// Sparse superblocks. See the earlier discussion of this feature
    /// </summary>
    RO_COMPAT_SPARSE_SUPER = 0x1,
    /// <summary>
    /// Allow storing files larger than 2GiB
    /// </summary>
    RO_COMPAT_LARGE_FILE = 0x2,
    /// <summary>
    /// Was intended for use with htree directories, but was not needed. Not used in kernel or e2fsprogs
    /// </summary>
    RO_COMPAT_BTREE_DIR = 0x4,
    /// <summary>
    /// This filesystem has files whose space usage is stored in i_blocks in units of filesystem blocks, not 512-byte sectors. Inodes using this feature will be marked with EXT4_INODE_HUGE_FILE.
    /// </summary>
    RO_COMPAT_HUGE_FILE = 0x8,
    /// <summary>
    /// Group descriptors have checksums. In addition to detecting corruption, this is useful for lazy formatting with uninitialized groups
    /// </summary>
    RO_COMPAT_GDT_CSUM = 0x10,
    /// <summary>
    /// Indicates that the old ext3 32,000 subdirectory limit no longer applies. A directory's i_links_count will be set to 1 if it is incremented past 64,999.
    /// </summary>
    RO_COMPAT_DIR_NLINK = 0x20,
    /// <summary>
    /// Indicates that large inodes exist on this filesystem, storing extra fields after EXT2_GOOD_OLD_INODE_SIZE
    /// </summary>
    RO_COMPAT_EXTRA_ISIZE = 0x40,
    /// <summary>
    /// This filesystem has a snapshot. Not implemented in ext4.
    /// </summary>
    RO_COMPAT_HAS_SNAPSHOT = 0x80,
    /// <summary>
    /// Quota is handled transactionally with the journal
    /// </summary>
    RO_COMPAT_QUOTA = 0x100,
    /// <summary>
    /// This filesystem supports "bigalloc", which means that filesystem block allocation bitmaps are tracked in units of clusters (of blocks) instead of blocks
    /// </summary>
    RO_COMPAT_BIGALLOC = 0x200,
    /// <summary>
    /// This filesystem supports metadata checksumming. (RO_COMPAT_METADATA_CSUM; implies RO_COMPAT_GDT_CSUM, though GDT_CSUM must not be set)
    /// </summary>
    RO_COMPAT_METADATA_CSUM = 400,
    /// <summary>
    /// Filesystem supports replicas. This feature is neither in the kernel nor e2fsprogs.
    /// </summary>
    RO_COMPAT_REPLICA = 0x800,
    /// <summary>
    /// Read-only filesystem image; the kernel will not mount this image read-write and most tools will refuse to write to the image.
    /// </summary>
    RO_COMPAT_READONLY = 0x1000,
    /// <summary>
    /// Filesystem tracks project quotas.
    /// </summary>
    RO_COMPAT_PROJECT = 0x2000
}