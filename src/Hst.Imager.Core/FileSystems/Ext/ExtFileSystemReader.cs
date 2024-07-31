using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hst.Core.Extensions;

namespace Hst.Imager.Core.FileSystems.Ext;

public static class ExtFileSystemReader
{
    public static async Task<ExtFileSystemInfo> Read(Stream stream)
    {
        // seek super block offset at 1024
        stream.Seek(1024, SeekOrigin.Begin);

        // read super block bytes
        var superBlockBytes = await stream.ReadBytes(512);

        // read super block
        var superBlock = SuperBlockReader.Read(superBlockBytes);
        
        var compatibleFeatureFlags = (FeatureCompatFlags)superBlock.SFeatureCompat;
        var incompatibleFeatureFlags = (FeatureIncompatFlags)superBlock.SFeatureIncompat;
        var readOnlyFeatureFlags = (FeatureRoCompatFlags)superBlock.SFeatureRoCompat;

        var blocksCount = (ulong)superBlock.SBlocksCountLo;
        var freeBlocksCount = (ulong)superBlock.SFreeBlocksCountLo;

        if (incompatibleFeatureFlags.HasFlag(FeatureIncompatFlags.INCOMPAT_64BIT))
        {
            blocksCount += (ulong)superBlock.SBlocksCountHi << 32; 
            freeBlocksCount += (ulong)superBlock.SFreeBlocksCountHi << 32; 
        }
        
        var blockSize = (uint)Math.Pow(2, 10 + superBlock.SLogBlockSize);

        var sizeBytes = blocksCount * blockSize;
        var freeBytes = freeBlocksCount * blockSize;

        var version = ExtFileSystemInfo.ExtVersion.Ext2;
        var features = new List<ExtFileSystemInfo.ExtFeature>();

        // ext2 features
        if (incompatibleFeatureFlags.HasFlag(FeatureIncompatFlags.INCOMPAT_FILETYPE))
        {
            features.Add(ExtFileSystemInfo.ExtFeature.FileType);
        }
        if (compatibleFeatureFlags.HasFlag(FeatureCompatFlags.COMPAT_EXT_ATTR))
        {
            features.Add(ExtFileSystemInfo.ExtFeature.ExtAttr);
        }
        if (readOnlyFeatureFlags.HasFlag(FeatureRoCompatFlags.RO_COMPAT_SPARSE_SUPER))
        {
            features.Add(ExtFileSystemInfo.ExtFeature.SparseSuper);
        }
        if (readOnlyFeatureFlags.HasFlag(FeatureRoCompatFlags.RO_COMPAT_LARGE_FILE))
        {
            features.Add(ExtFileSystemInfo.ExtFeature.LargeFile);
        }
        if (compatibleFeatureFlags.HasFlag(FeatureCompatFlags.COMPAT_DIR_INDEX))
        {
            features.Add(ExtFileSystemInfo.ExtFeature.DirIndex);
        }
        if (compatibleFeatureFlags.HasFlag(FeatureCompatFlags.COMPAT_RESIZE_INODE))
        {
            features.Add(ExtFileSystemInfo.ExtFeature.ResizeInode);
        }

        // ext3 features
        if (compatibleFeatureFlags.HasFlag(FeatureCompatFlags.COMPAT_HAS_JOURNAL))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext3;
            features.Add(ExtFileSystemInfo.ExtFeature.HasJournal);
        }
        
        // ext4 features
        if (incompatibleFeatureFlags.HasFlag(FeatureIncompatFlags.INCOMPAT_64BIT))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.Bit64);
        }
        if (readOnlyFeatureFlags.HasFlag(FeatureRoCompatFlags.RO_COMPAT_DIR_NLINK))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.DirNLink);
        }
        if (incompatibleFeatureFlags.HasFlag(FeatureIncompatFlags.INCOMPAT_EXTENTS))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.Extent);
        }
        if (readOnlyFeatureFlags.HasFlag(FeatureRoCompatFlags.RO_COMPAT_EXTRA_ISIZE))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.ExtraIsize);
        }
        if (incompatibleFeatureFlags.HasFlag(FeatureIncompatFlags.INCOMPAT_FLEX_BG))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.FlexBg);
        }
        if (readOnlyFeatureFlags.HasFlag(FeatureRoCompatFlags.RO_COMPAT_HUGE_FILE))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.HugeFile);
        }
        if (incompatibleFeatureFlags.HasFlag(FeatureIncompatFlags.INCOMPAT_META_BG))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.MetaBg);
        }
        if (compatibleFeatureFlags.HasFlag(FeatureCompatFlags.COMPAT_LAZY_BG))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.UninitBg);
        }
        if (incompatibleFeatureFlags.HasFlag(FeatureIncompatFlags.INCOMPAT_MMP))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.Mmp);
        }
        if (readOnlyFeatureFlags.HasFlag(FeatureRoCompatFlags.RO_COMPAT_BIGALLOC))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.BigAlloc);
        }
        if (readOnlyFeatureFlags.HasFlag(FeatureRoCompatFlags.RO_COMPAT_QUOTA))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.Quota);
        }
        if (compatibleFeatureFlags.HasFlag(FeatureCompatFlags.COMPAT_SPARSE_SUPER2))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.SparseSuper2);
        }
        if (readOnlyFeatureFlags.HasFlag(FeatureRoCompatFlags.RO_COMPAT_METADATA_CSUM))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.MetadataCsum);
        }
        if (incompatibleFeatureFlags.HasFlag(FeatureIncompatFlags.INCOMPAT_ENCRYPT))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.Encrypt);
        }
        if (readOnlyFeatureFlags.HasFlag(FeatureRoCompatFlags.RO_COMPAT_GDT_CSUM))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            if (!features.Contains(ExtFileSystemInfo.ExtFeature.MetadataCsum))
            {
                features.Add(ExtFileSystemInfo.ExtFeature.MetadataCsum);
            }
        }
        if (readOnlyFeatureFlags.HasFlag(FeatureRoCompatFlags.RO_COMPAT_PROJECT))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.Project);
        }
        if (incompatibleFeatureFlags.HasFlag(FeatureIncompatFlags.INCOMPAT_EA_INODE))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.EaInode);
        }
        if (incompatibleFeatureFlags.HasFlag(FeatureIncompatFlags.INCOMPAT_LARGEDIR))
        {
            version = ExtFileSystemInfo.ExtVersion.Ext4;
            features.Add(ExtFileSystemInfo.ExtFeature.LargeDir);
        }
        
        // inline_data         ext4, 3.8 (EXT4_INLINE_DATA_FL, part of index nodes, struct ext4_inode)
        // casefold            ext4, 5.2 (EXT4_CASEFOLD_FL, )
        // verity              ext4, 5.4 (EXT4_VERITY_FL, part of index nodes, struct ext4_inode)
        // stable_inodes       ext4, 5.5
        // ref: https://www.kernel.org/doc/html/latest/filesystems/ext4/inodes.html?highlight=inode
        
        // inode flag definitions
        // ref: https://elixir.bootlin.com/linux/v6.7/source/fs/ext4/ext4.h#L500
        // #define EXT4_VERITY_FL			0x00100000 /* Verity protected inode */
        // #define EXT4_INLINE_DATA_FL		0x10000000 /* Inode has inline data. */
        // #define EXT4_CASEFOLD_FL		0x40000000 /* Casefolded directory */
        // inode feature set definitions
        // #define EXT4_FEATURE_COMPAT_STABLE_INODES	0x0800
        
        return new ExtFileSystemInfo
        {
            Version = version,
            Features = features,
            Size = sizeBytes,
            Free = freeBytes,
            VolumeName = superBlock.VolumeName
        };
    }
}