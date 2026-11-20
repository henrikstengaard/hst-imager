using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hst.Core.Converters;

namespace Hst.Imager.Core.Optimization.FileSystems;

/// <summary>
/// Reads free blocks from Amiga Original File System and Fast File System (DOS0 - DOS7) using the root block bitmap.
/// </summary>
public static class FastFileSystemAllocationReader
{
    private const int TypeHeader = 2;
    private const int SecondaryTypeRoot = 1;
    private const uint BitmapValid = 0xffffffff;
    private const int BitmapPagesCount = 25;

    /// <summary>
    /// Read free ranges of fast file system in volume.
    /// </summary>
    /// <param name="stream">Stream to read from.</param>
    /// <param name="volumeOffset">Absolute offset of volume.</param>
    /// <param name="volumeSize">Size of volume in bytes.</param>
    /// <param name="reserved">Number of reserved blocks at start of volume (usually 2 boot blocks).</param>
    /// <param name="blockSize">File system block size in bytes.</param>
    /// <param name="rootBlockOffset">Root block number, 0 calculates root block number from volume size.</param>
    /// <returns>File system allocation or null, if volume is not a fast file system.</returns>
    public static async Task<FileSystemAllocation> Read(Stream stream, long volumeOffset, long volumeSize,
        uint reserved, int blockSize, uint rootBlockOffset = 0)
    {
        var bootBlock = await StreamReadHelper.ReadBytesAt(stream, volumeOffset, 4);
        if (bootBlock[0] != 'D' || bootBlock[1] != 'O' || bootBlock[2] != 'S' || bootBlock[3] > 7)
        {
            return null;
        }

        var fileSystem = $"DOS{bootBlock[3]}";

        if (blockSize < 512 || blockSize % 512 != 0)
        {
            throw new IOException($"Invalid block size {blockSize}");
        }

        var blocks = volumeSize / blockSize;
        if (blocks <= reserved)
        {
            throw new IOException("Volume is too small");
        }

        // root block candidates: defined root block or calculated from volume size
        // using hst amiga formatter formula and amigados formula
        var sectors = volumeSize / 512;
        var rootBlockCandidates = rootBlockOffset != 0
            ? new[] { (long)rootBlockOffset }
            : new[] { sectors / 2 / (blockSize / 512), (reserved + (blocks - 1)) / 2 };

        byte[] rootBlock = null;
        foreach (var candidate in rootBlockCandidates)
        {
            if (candidate <= 0 || candidate >= blocks)
            {
                continue;
            }

            var candidateBlock = await ReadBlock(stream, volumeOffset, candidate, blockSize);
            if (BigEndianConverter.ConvertBytesToInt32(candidateBlock) == TypeHeader &&
                BigEndianConverter.ConvertBytesToInt32(candidateBlock, blockSize - 4) == SecondaryTypeRoot &&
                IsChecksumValid(candidateBlock))
            {
                rootBlock = candidateBlock;
                break;
            }
        }

        if (rootBlock == null)
        {
            throw new IOException($"Root block not found at block {string.Join(" or ", rootBlockCandidates)}");
        }

        if (BigEndianConverter.ConvertBytesToUInt32(rootBlock, blockSize - 200) != BitmapValid)
        {
            throw new IOException("Bitmap is not valid, volume needs to be validated");
        }

        var longsPerBitmapBlock = blockSize / 4 - 1;
        var blocksPerBitmapBlock = (long)longsPerBitmapBlock * 32;
        var bitmapBlocksCount = (int)((blocks - reserved + blocksPerBitmapBlock - 1) / blocksPerBitmapBlock);

        // read bitmap block pointers from root block and bitmap extension blocks
        var bitmapBlockPointers = new List<uint>();
        for (var i = 0; i < BitmapPagesCount && bitmapBlockPointers.Count < bitmapBlocksCount; i++)
        {
            var pointer = BigEndianConverter.ConvertBytesToUInt32(rootBlock, blockSize - 196 + i * 4);
            if (pointer == 0)
            {
                break;
            }

            bitmapBlockPointers.Add(pointer);
        }

        var extensionPointer = BigEndianConverter.ConvertBytesToUInt32(rootBlock, blockSize - 96);
        var visitedExtensionBlocks = new HashSet<uint>();
        while (extensionPointer != 0 && bitmapBlockPointers.Count < bitmapBlocksCount)
        {
            if (extensionPointer >= blocks || !visitedExtensionBlocks.Add(extensionPointer))
            {
                throw new IOException($"Invalid bitmap extension block pointer {extensionPointer}");
            }

            var extensionBlock = await ReadBlock(stream, volumeOffset, extensionPointer, blockSize);
            for (var i = 0; i < longsPerBitmapBlock && bitmapBlockPointers.Count < bitmapBlocksCount; i++)
            {
                bitmapBlockPointers.Add(BigEndianConverter.ConvertBytesToUInt32(extensionBlock, i * 4));
            }

            extensionPointer = BigEndianConverter.ConvertBytesToUInt32(extensionBlock, blockSize - 4);
        }

        if (bitmapBlockPointers.Count < bitmapBlocksCount)
        {
            throw new IOException(
                $"Bitmap is incomplete, found {bitmapBlockPointers.Count} of {bitmapBlocksCount} bitmap blocks");
        }

        // bit set to 1 is a free block. first bit in bitmap is block after reserved blocks
        var builder = new FreeRangeBuilder();
        long block = reserved;
        foreach (var bitmapBlockPointer in bitmapBlockPointers)
        {
            if (bitmapBlockPointer == 0 || bitmapBlockPointer >= blocks)
            {
                throw new IOException($"Invalid bitmap block pointer {bitmapBlockPointer}");
            }

            var bitmapBlock = await ReadBlock(stream, volumeOffset, bitmapBlockPointer, blockSize);
            if (!IsChecksumValid(bitmapBlock))
            {
                throw new IOException($"Invalid checksum in bitmap block {bitmapBlockPointer}");
            }

            for (var i = 0; i < longsPerBitmapBlock && block < blocks; i++)
            {
                var map = BigEndianConverter.ConvertBytesToUInt32(bitmapBlock, 4 + i * 4);
                for (var bit = 0; bit < 32 && block < blocks; bit++, block++)
                {
                    if ((map & (1U << bit)) != 0)
                    {
                        builder.Add(volumeOffset + block * blockSize, blockSize);
                    }
                }
            }
        }

        return new FileSystemAllocation(fileSystem, builder.Build());
    }

    private static Task<byte[]> ReadBlock(Stream stream, long volumeOffset, long block, int blockSize) =>
        StreamReadHelper.ReadBytesAt(stream, volumeOffset + block * blockSize, blockSize);

    /// <summary>
    /// Validate amiga block checksum. Sum of all longs including checksum must be zero.
    /// </summary>
    private static bool IsChecksumValid(byte[] block)
    {
        uint sum = 0;
        for (var offset = 0; offset < block.Length; offset += 4)
        {
            sum += BigEndianConverter.ConvertBytesToUInt32(block, offset);
        }

        return sum == 0;
    }
}
