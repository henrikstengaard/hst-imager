using System;

namespace Hst.Imager.Core.FileSystems.Ext;

/// <summary>
/// Compatible feature set flags. Kernel can still read/write this fs even if it doesn't understand a flag; e2fsck will not attempt to fix a filesystem with any unknown COMPAT flags.
/// </summary>
[Flags]
public enum FeatureCompatFlags
{
    /// <summary>
    /// Directory preallocation
    /// </summary>
    COMPAT_DIR_PREALLOC = 0x1,
    /// <summary>
    /// "imagic inodes". Used by AFS to indicate inodes that are not linked into the directory namespace. Inodes marked with this flag will not be added to lost+found by e2fsck.
    /// </summary>
    COMPAT_IMAGIC_INODES = 0x2,
    /// <summary>
    /// Has a journal
    /// </summary>
    COMPAT_HAS_JOURNAL = 0x4,
    /// <summary>
    /// Supports extended attributes
    /// </summary>
    COMPAT_EXT_ATTR = 0x8,
    /// <summary>
    /// Has reserved GDT blocks for filesystem expansion. Requires RO_COMPAT_SPARSE_SUPER.
    /// </summary>
    COMPAT_RESIZE_INODE = 0x10,
    /// <summary>
    /// Has indexed directories.
    /// </summary>
    COMPAT_DIR_INDEX = 0x20,
    /// <summary>
    /// "Lazy BG". Not in Linux kernel, seems to have been for uninitialized block groups? (uninit_bg)
    /// </summary>
    COMPAT_LAZY_BG = 0x40,
    /// <summary>
    /// "Exclude inode". Intended for filesystem snapshot feature, but not used.
    /// </summary>
    COMPAT_EXCLUDE_INODE = 0x80,
    /// <summary>
    /// "Exclude bitmap". Seems to be used to indicate the presence of snapshot-related exclude bitmaps? Not defined in kernel or used in e2fsprogs.
    /// </summary>
    COMPAT_EXCLUDE_BITMAP = 0x100,
    /// <summary>
    /// Sparse Super Block, v2. If this flag is set, the SB field s_backup_bgs points to the two block groups that contain backup superblocks.
    /// </summary>
    COMPAT_SPARSE_SUPER2 = 0x200
}