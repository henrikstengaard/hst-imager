using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hst.Core.Converters;

namespace Hst.Imager.Core.Optimization.FileSystems;

/// <summary>
/// Reads free blocks from Amiga Professional File System 3 (PFS3) using bitmap index and bitmap blocks.
/// Reserved area (boot blocks, root block and reserved blocks) is always considered used.
/// </summary>
public static class Pfs3AllocationReader
{
    private const uint RootBlock = 2;
    private const int IdPfsDisk = 0x50465301; // PFS\1
    private const int IdPfs2Disk = 0x50465302; // PFS\2
    private const ushort BitmapIndexBlockId = 0x4d49; // MI
    private const ushort BitmapBlockId = 0x424d; // BM
    private const int ModeHarddisk = 1;
    private const int ModeSuperIndex = 128;
    private const int MaxSmallBitmapIndex = 4;
    private const int MaxBitmapIndex = 103;
    private const int BlockHeaderSize = 12;

    /// <summary>
    /// Read free ranges of pfs3 file system in volume.
    /// </summary>
    /// <param name="stream">Stream to read from.</param>
    /// <param name="volumeOffset">Absolute offset of volume.</param>
    /// <param name="volumeSize">Size of volume in bytes.</param>
    /// <param name="blockSize">Block size in bytes (sector size).</param>
    /// <returns>File system allocation or null, if volume is not a pfs3 file system.</returns>
    public static async Task<FileSystemAllocation> Read(Stream stream, long volumeOffset, long volumeSize,
        int blockSize)
    {
        var rootBlock = await ReadBlocks(stream, volumeOffset, RootBlock, blockSize, 512);
        var diskType = BigEndianConverter.ConvertBytesToInt32(rootBlock);
        if (diskType != IdPfsDisk && diskType != IdPfs2Disk)
        {
            return null;
        }

        var options = BigEndianConverter.ConvertBytesToInt32(rootBlock, 4);
        var lastReserved = BigEndianConverter.ConvertBytesToUInt32(rootBlock, 0x34);
        var reservedBlockSize = (int)BigEndianConverter.ConvertBytesToUInt16(rootBlock, 0x40);
        var diskSize = (long)BigEndianConverter.ConvertBytesToUInt32(rootBlock, 0x54);

        if ((options & ModeHarddisk) == 0)
        {
            throw new IOException("PFS3 volume is not in harddisk mode");
        }

        var volumeBlocks = volumeSize / blockSize;
        if (reservedBlockSize < 512 || reservedBlockSize % blockSize != 0 || diskSize == 0 ||
            diskSize > volumeBlocks || lastReserved >= diskSize)
        {
            throw new IOException("Invalid PFS3 root block");
        }

        var superMode = (options & ModeSuperIndex) != 0;
        var bitmapIndexCount = (superMode ? MaxBitmapIndex : MaxSmallBitmapIndex) + 1;
        var longsPerBitmapBlock = reservedBlockSize / 4 - 3;
        var indexPerBlock = (reservedBlockSize - BlockHeaderSize) / 4;
        var bitmapStart = (long)lastReserved + 1;
        var bitmapLongs = (diskSize - bitmapStart + 31) / 32;
        var bitmapBlocksCount = (bitmapLongs + longsPerBitmapBlock - 1) / longsPerBitmapBlock;

        var bitmapIndexBlocks = new Dictionary<long, byte[]>();
        var builder = new FreeRangeBuilder();
        var block = bitmapStart;

        for (long seqNr = 0; seqNr < bitmapBlocksCount; seqNr++)
        {
            // get bitmap index block
            var indexNr = seqNr / indexPerBlock;
            if (indexNr >= bitmapIndexCount)
            {
                throw new IOException($"PFS3 bitmap index {indexNr} exceeds max bitmap index");
            }

            if (!bitmapIndexBlocks.TryGetValue(indexNr, out var indexBlock))
            {
                var indexBlockNr = BigEndianConverter.ConvertBytesToUInt32(rootBlock, 0x60 + (int)indexNr * 4);
                indexBlock = await ReadReservedBlock(stream, volumeOffset, indexBlockNr, blockSize,
                    reservedBlockSize, diskSize, BitmapIndexBlockId, indexNr);
                bitmapIndexBlocks[indexNr] = indexBlock;
            }

            // get bitmap block
            var bitmapBlockNr = BigEndianConverter.ConvertBytesToUInt32(indexBlock,
                BlockHeaderSize + (int)(seqNr % indexPerBlock) * 4);
            var bitmapBlock = await ReadReservedBlock(stream, volumeOffset, bitmapBlockNr, blockSize,
                reservedBlockSize, diskSize, BitmapBlockId, seqNr);

            // bit set to 1 is a free block, most significant bit first
            for (var i = 0; i < longsPerBitmapBlock && block < diskSize; i++)
            {
                var map = BigEndianConverter.ConvertBytesToUInt32(bitmapBlock, BlockHeaderSize + i * 4);
                for (var bit = 31; bit >= 0 && block < diskSize; bit--, block++)
                {
                    if ((map & (1U << bit)) != 0)
                    {
                        builder.Add(volumeOffset + block * blockSize, blockSize);
                    }
                }
            }
        }

        return new FileSystemAllocation("PFS3", builder.Build());
    }

    private static async Task<byte[]> ReadReservedBlock(Stream stream, long volumeOffset, uint blockNr,
        int blockSize, int reservedBlockSize, long diskSize, ushort expectedId, long expectedSeqNr)
    {
        if (blockNr == 0 || blockNr >= diskSize)
        {
            throw new IOException($"Invalid PFS3 block number {blockNr}");
        }

        var blockBytes = await ReadBlocks(stream, volumeOffset, blockNr, blockSize, reservedBlockSize);
        var id = BigEndianConverter.ConvertBytesToUInt16(blockBytes);
        var seqNr = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 8);
        if (id != expectedId || seqNr != expectedSeqNr)
        {
            throw new IOException(
                $"Invalid PFS3 block at block number {blockNr}, expected id 0x{expectedId:x4} and seqnr {expectedSeqNr}");
        }

        return blockBytes;
    }

    private static Task<byte[]> ReadBlocks(Stream stream, long volumeOffset, long blockNr, int blockSize,
        int count) =>
        StreamReadHelper.ReadBytesAt(stream, volumeOffset + blockNr * blockSize, count);
}
