
using System;

namespace Hst.Imager.Core.Tests.FileSystemTests;

public static class ExtTestData
{
    public static byte[] CreateExt2PartitionBytes()
    {
        var partitionBytes = new byte[2048];
        CreateExt2PartitionBytes(partitionBytes);
        return partitionBytes;
    }
    
    public static byte[] CreateExt3PartitionBytes()
    {
        var partitionBytes = new byte[2048];
        CreateExt3PartitionBytes(partitionBytes);
        return partitionBytes;
    }
    
    public static byte[] CreateExt4PartitionBytes()
    {
        var partitionBytes = new byte[2048];
        CreateExt4PartitionBytes(partitionBytes);
        return partitionBytes;
    }

    public static void CreateExt2PartitionBytes(byte[] partitionBytes)
    {
        if (partitionBytes.Length < 2048)
        {
            throw new ArgumentException("Invalid data length, shorter than 2048 bytes", nameof(partitionBytes));
        }
        
        // zero fill first 2048 bytes (boot block and super block)
        Array.Fill<byte>(partitionBytes, 0, 0, 2048);
        
        // super block, s_inodes_count
        partitionBytes[1024 + 0x0] = 0x80;
        partitionBytes[1024 + 0x1] = 0x0;
        partitionBytes[1024 + 0x2] = 0x0;
        partitionBytes[1024 + 0x3] = 0x0;

        // super block, s_blocks_count_lo
        partitionBytes[1024 + 0x4] = 0xd0;
        partitionBytes[1024 + 0x5] = 0x3;
        partitionBytes[1024 + 0x6] = 0x0;
        partitionBytes[1024 + 0x7] = 0x0;

        // super block, s_r_blocks_count_lo
        partitionBytes[1024 + 0x8] = 0x30;
        partitionBytes[1024 + 0x9] = 0x0;
        partitionBytes[1024 + 0xa] = 0x0;
        partitionBytes[1024 + 0xb] = 0x0;

        // super block, s_free_blocks_count_lo
        partitionBytes[1024 + 0xc] = 0xaa;
        partitionBytes[1024 + 0xd] = 0x3;
        partitionBytes[1024 + 0xe] = 0x0;
        partitionBytes[1024 + 0xf] = 0x0;

        // super block, s_free_inodes_count
        partitionBytes[1024 + 0x10] = 0x75;
        partitionBytes[1024 + 0x11] = 0x0;
        partitionBytes[1024 + 0x12] = 0x0;
        partitionBytes[1024 + 0x13] = 0x0;

        // super block, s_first_data_block
        partitionBytes[1024 + 0x14] = 0x1;
        partitionBytes[1024 + 0x15] = 0x0;
        partitionBytes[1024 + 0x16] = 0x0;
        partitionBytes[1024 + 0x17] = 0x0;

        // super block, s_log_block_size
        partitionBytes[1024 + 0x18] = 0x0;
        partitionBytes[1024 + 0x19] = 0x0;
        partitionBytes[1024 + 0x1a] = 0x0;
        partitionBytes[1024 + 0x1b] = 0x0;

        // super block, s_magic
        partitionBytes[1024 + 0x38] = 0x53;
        partitionBytes[1024 + 0x39] = 0xef;
        
        // super block, s_feature_compat
        partitionBytes[1024 + 0x5c] = 0x38;
        partitionBytes[1024 + 0x5d] = 0x0;
        partitionBytes[1024 + 0x5e] = 0x0;
        partitionBytes[1024 + 0x5f] = 0x0;
        
        // super block, s_feature_incompat
        partitionBytes[1024 + 0x60] = 0x2;
        partitionBytes[1024 + 0x61] = 0x0;
        partitionBytes[1024 + 0x62] = 0x0;
        partitionBytes[1024 + 0x63] = 0x0;

        // super block, s_feature_ro_compat
        partitionBytes[1024 + 0x64] = 0x3;
        partitionBytes[1024 + 0x65] = 0x0;
        partitionBytes[1024 + 0x66] = 0x0;
        partitionBytes[1024 + 0x67] = 0x0;
    }
    
    public static void CreateExt3PartitionBytes(byte[] partitionBytes)
    {
        if (partitionBytes.Length < 2048)
        {
            throw new ArgumentException("Invalid data length, shorter than 2048 bytes", nameof(partitionBytes));
        }
        
        // zero fill first 2048 bytes (boot block and super block)
        Array.Fill<byte>(partitionBytes, 0, 0, 2048);
        
        // super block, s_inodes_count
        partitionBytes[1024 + 0x0] = 0x70;
        partitionBytes[1024 + 0x1] = 0x1;
        partitionBytes[1024 + 0x2] = 0x0;
        partitionBytes[1024 + 0x3] = 0x0;

        // super block, s_blocks_count_lo
        partitionBytes[1024 + 0x4] = 0x70;
        partitionBytes[1024 + 0x5] = 0xb;
        partitionBytes[1024 + 0x6] = 0x0;
        partitionBytes[1024 + 0x7] = 0x0;

        // super block, s_r_blocks_count_lo
        partitionBytes[1024 + 0x8] = 0x92;
        partitionBytes[1024 + 0x9] = 0x0;
        partitionBytes[1024 + 0xa] = 0x0;
        partitionBytes[1024 + 0xb] = 0x0;

        // super block, s_free_blocks_count_lo
        partitionBytes[1024 + 0xc] = 0x1f;
        partitionBytes[1024 + 0xd] = 0x07;
        partitionBytes[1024 + 0xe] = 0x0;
        partitionBytes[1024 + 0xf] = 0x0;

        // super block, s_free_inodes_count
        partitionBytes[1024 + 0x10] = 0x65;
        partitionBytes[1024 + 0x11] = 0x1;
        partitionBytes[1024 + 0x12] = 0x0;
        partitionBytes[1024 + 0x13] = 0x0;

        // super block, s_first_data_block
        partitionBytes[1024 + 0x14] = 0x1;
        partitionBytes[1024 + 0x15] = 0x0;
        partitionBytes[1024 + 0x16] = 0x0;
        partitionBytes[1024 + 0x17] = 0x0;

        // super block, s_log_block_size
        partitionBytes[1024 + 0x18] = 0x0;
        partitionBytes[1024 + 0x19] = 0x0;
        partitionBytes[1024 + 0x1a] = 0x0;
        partitionBytes[1024 + 0x1b] = 0x0;

        // super block, s_magic
        partitionBytes[1024 + 0x38] = 0x53;
        partitionBytes[1024 + 0x39] = 0xef;
        
        // super block, s_feature_compat
        partitionBytes[1024 + 0x5c] = 0x3c;
        partitionBytes[1024 + 0x5d] = 0x0;
        partitionBytes[1024 + 0x5e] = 0x0;
        partitionBytes[1024 + 0x5f] = 0x0;
        
        // super block, s_feature_incompat
        partitionBytes[1024 + 0x60] = 0x02;
        partitionBytes[1024 + 0x61] = 0x0;
        partitionBytes[1024 + 0x62] = 0x0;
        partitionBytes[1024 + 0x63] = 0x0;

        // super block, s_feature_ro_compat
        partitionBytes[1024 + 0x64] = 0x3;
        partitionBytes[1024 + 0x65] = 0x0;
        partitionBytes[1024 + 0x66] = 0x0;
        partitionBytes[1024 + 0x67] = 0x0;
    }
        
    public static void CreateExt4PartitionBytes(byte[] partitionBytes)
    {
        if (partitionBytes.Length < 2048)
        {
            throw new ArgumentException("Invalid data length, shorter than 2048 bytes", nameof(partitionBytes));
        }
        
        // zero fill first 2048 bytes (boot block and super block)
        Array.Fill<byte>(partitionBytes, 0, 0, 2048);
        
        // super block, s_inodes_count
        partitionBytes[1024 + 0x0] = 0x70;
        partitionBytes[1024 + 0x1] = 0x1;
        partitionBytes[1024 + 0x2] = 0x0;
        partitionBytes[1024 + 0x3] = 0x0;

        // super block, s_blocks_count_lo
        partitionBytes[1024 + 0x4] = 0x70;
        partitionBytes[1024 + 0x5] = 0xb;
        partitionBytes[1024 + 0x6] = 0x0;
        partitionBytes[1024 + 0x7] = 0x0;

        // super block, s_r_blocks_count_lo
        partitionBytes[1024 + 0x8] = 0x92;
        partitionBytes[1024 + 0x9] = 0x0;
        partitionBytes[1024 + 0xa] = 0x0;
        partitionBytes[1024 + 0xb] = 0x0;

        // super block, s_free_blocks_count_lo
        partitionBytes[1024 + 0xc] = 0x19;
        partitionBytes[1024 + 0xd] = 0x7;
        partitionBytes[1024 + 0xe] = 0x0;
        partitionBytes[1024 + 0xf] = 0x0;

        // super block, s_free_inodes_count
        partitionBytes[1024 + 0x10] = 0x65;
        partitionBytes[1024 + 0x11] = 0x1;
        partitionBytes[1024 + 0x12] = 0x0;
        partitionBytes[1024 + 0x13] = 0x0;

        // super block, s_first_data_block
        partitionBytes[1024 + 0x14] = 0x1;
        partitionBytes[1024 + 0x15] = 0x0;
        partitionBytes[1024 + 0x16] = 0x0;
        partitionBytes[1024 + 0x17] = 0x0;

        // super block, s_log_block_size
        partitionBytes[1024 + 0x18] = 0x0;
        partitionBytes[1024 + 0x19] = 0x0;
        partitionBytes[1024 + 0x1a] = 0x0;
        partitionBytes[1024 + 0x1b] = 0x0;

        // super block, s_magic
        partitionBytes[1024 + 0x38] = 0x53;
        partitionBytes[1024 + 0x39] = 0xef;
        
        // super block, s_feature_compat
        partitionBytes[1024 + 0x5c] = 0x3c;
        partitionBytes[1024 + 0x5d] = 0x0;
        partitionBytes[1024 + 0x5e] = 0x0;
        partitionBytes[1024 + 0x5f] = 0x0;
        
        // super block, s_feature_incompat
        partitionBytes[1024 + 0x60] = 0xc2;
        partitionBytes[1024 + 0x61] = 0x2;
        partitionBytes[1024 + 0x62] = 0x0;
        partitionBytes[1024 + 0x63] = 0x0;

        // super block, s_feature_ro_compat
        partitionBytes[1024 + 0x64] = 0x6b;
        partitionBytes[1024 + 0x65] = 0x4;
        partitionBytes[1024 + 0x66] = 0x0;
        partitionBytes[1024 + 0x67] = 0x0;
    }
}