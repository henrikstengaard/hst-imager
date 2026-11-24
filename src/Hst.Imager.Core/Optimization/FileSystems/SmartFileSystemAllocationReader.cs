using System.IO;
using System.Threading.Tasks;
using Hst.Core.Converters;

namespace Hst.Imager.Core.Optimization.FileSystems;

/// <summary>
/// Reads free blocks from Amiga Smart File System (SFS) using bitmap blocks located by the root block.
/// Root blocks, admin space and bitmap blocks are marked used in the bitmap by the file system.
/// </summary>
public static class SmartFileSystemAllocationReader
{
    private const uint RootBlockId = 0x53465300; // SFS\0
    private const uint BitmapBlockId = 0x42544d50; // BTMP
    private const int BlockHeaderSize = 12;
    private const int RootTotalBlocksOffset = 48;
    private const int RootBlockSizeOffset = 52;
    private const int RootBitmapBaseOffset = 96;

    /// <summary>
    /// Read free ranges of smart file system in volume.
    /// </summary>
    /// <param name="stream">Stream to read from.</param>
    /// <param name="volumeOffset">Absolute offset of volume.</param>
    /// <param name="volumeSize">Size of volume in bytes.</param>
    /// <returns>File system allocation or null, if volume is not a smart file system.</returns>
    public static async Task<FileSystemAllocation> Read(Stream stream, long volumeOffset, long volumeSize)
    {
        var rootBlock = await StreamReadHelper.ReadBytesAt(stream, volumeOffset, 512);
        if (BigEndianConverter.ConvertBytesToUInt32(rootBlock) != RootBlockId)
        {
            return null;
        }

        var ownBlock = BigEndianConverter.ConvertBytesToUInt32(rootBlock, 8);
        var totalBlocks = (long)BigEndianConverter.ConvertBytesToUInt32(rootBlock, RootTotalBlocksOffset);
        var blockSize = BigEndianConverter.ConvertBytesToUInt32(rootBlock, RootBlockSizeOffset);
        var bitmapBase = (long)BigEndianConverter.ConvertBytesToUInt32(rootBlock, RootBitmapBaseOffset);

        if (ownBlock != 0 || blockSize < 512 || blockSize % 512 != 0 || totalBlocks == 0 ||
            totalBlocks * blockSize > volumeSize || bitmapBase == 0 || bitmapBase >= totalBlocks)
        {
            throw new IOException("Invalid SFS root block");
        }

        // bitmap blocks are consecutive blocks starting at bitmap base
        var longsPerBitmapBlock = (int)(blockSize - BlockHeaderSize) / 4;
        var blocksPerBitmapBlock = (long)longsPerBitmapBlock * 32;
        var bitmapBlocksCount = (totalBlocks + blocksPerBitmapBlock - 1) / blocksPerBitmapBlock;
        if (bitmapBase + bitmapBlocksCount > totalBlocks)
        {
            throw new IOException("Invalid SFS bitmap base");
        }

        var builder = new FreeRangeBuilder();
        long block = 0;

        for (long i = 0; i < bitmapBlocksCount; i++)
        {
            var bitmapBlockNr = bitmapBase + i;
            var bitmapBlock = await StreamReadHelper.ReadBytesAt(stream, volumeOffset + bitmapBlockNr * blockSize,
                (int)blockSize);
            var id = BigEndianConverter.ConvertBytesToUInt32(bitmapBlock);
            var bitmapOwnBlock = BigEndianConverter.ConvertBytesToUInt32(bitmapBlock, 8);
            if (id != BitmapBlockId || bitmapOwnBlock != bitmapBlockNr)
            {
                throw new IOException($"Invalid SFS bitmap block at block number {bitmapBlockNr}");
            }

            // bit set to 1 is a free block, most significant bit first
            for (var l = 0; l < longsPerBitmapBlock && block < totalBlocks; l++)
            {
                var map = BigEndianConverter.ConvertBytesToUInt32(bitmapBlock, BlockHeaderSize + l * 4);
                for (var bit = 31; bit >= 0 && block < totalBlocks; bit--, block++)
                {
                    if ((map & (1U << bit)) != 0)
                    {
                        builder.Add(volumeOffset + block * blockSize, blockSize);
                    }
                }
            }
        }

        return new FileSystemAllocation("SFS", builder.Build());
    }
}
