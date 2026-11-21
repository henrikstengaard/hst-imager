using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading.Tasks;

namespace Hst.Imager.Core.Compressions.Xz;

/// <summary>
/// Reads uncompressed size of xz compressed data from the indexes stored at the end of each xz stream,
/// without decompressing data.
/// See https://tukaani.org/xz/xz-file-format.txt for xz file format.
/// </summary>
public static class XzIndexReader
{
    private const int StreamHeaderSize = 12;
    private const int StreamFooterSize = 12;

    /// <summary>
    /// Maximum index size read. Each index record is at most 18 bytes, so 16 MB covers indexes with
    /// more than 900000 blocks.
    /// </summary>
    private const long MaxIndexSize = 16 * 1024 * 1024;

    private static readonly byte[] StreamHeaderMagic = [0xfd, 0x37, 0x7a, 0x58, 0x5a, 0x00];
    private static readonly byte[] StreamFooterMagic = [0x59, 0x5a];

    /// <summary>
    /// Read uncompressed size from xz compressed stream. Stream must support seek.
    /// Multiple concatenated xz streams and stream padding are supported, where uncompressed size is the sum
    /// of uncompressed size of all xz streams.
    /// </summary>
    /// <param name="stream">Seekable stream with xz compressed data.</param>
    /// <returns>Uncompressed size or null, if xz indexes are not valid.</returns>
    public static async Task<long?> ReadUncompressedSize(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return null;
        }

        var position = stream.Length;
        var uncompressedSize = 0L;
        var footerBytes = new byte[StreamFooterSize];
        var headerBytes = new byte[StreamHeaderSize];
        var paddingBytes = new byte[4];

        // read xz streams backwards from end of stream
        while (position > 0)
        {
            // stream padding and xz streams are multiples of 4 bytes
            if (position % 4 != 0)
            {
                return null;
            }

            // skip stream padding of null bytes after xz stream
            stream.Position = position - 4;
            await stream.ReadExactlyAsync(paddingBytes, 0, paddingBytes.Length);
            if (IsZeroFilled(paddingBytes))
            {
                position -= 4;
                continue;
            }

            if (position < StreamHeaderSize + StreamFooterSize)
            {
                return null;
            }

            // read stream footer
            stream.Position = position - StreamFooterSize;
            await stream.ReadExactlyAsync(footerBytes, 0, footerBytes.Length);
            if (footerBytes[10] != StreamFooterMagic[0] || footerBytes[11] != StreamFooterMagic[1])
            {
                return null;
            }

            // backward size is the size of the index
            var indexSize = ((long)BinaryPrimitives.ReadUInt32LittleEndian(footerBytes.AsSpan(4, 4)) + 1) * 4;
            var indexOffset = position - StreamFooterSize - indexSize;
            if (indexSize > MaxIndexSize || indexOffset < StreamHeaderSize)
            {
                return null;
            }

            // read index
            var indexBytes = new byte[indexSize];
            stream.Position = indexOffset;
            await stream.ReadExactlyAsync(indexBytes, 0, indexBytes.Length);

            var index = ReadIndex(indexBytes);
            if (index == null)
            {
                return null;
            }

            // read stream header and verify magic
            var streamOffset = indexOffset - index.Value.BlocksSize - StreamHeaderSize;
            if (streamOffset < 0)
            {
                return null;
            }

            stream.Position = streamOffset;
            await stream.ReadExactlyAsync(headerBytes, 0, headerBytes.Length);
            if (!headerBytes.AsSpan(0, StreamHeaderMagic.Length).SequenceEqual(StreamHeaderMagic))
            {
                return null;
            }

            uncompressedSize += index.Value.UncompressedSize;
            position = streamOffset;
        }

        return uncompressedSize;
    }

    /// <summary>
    /// Read index records and return the size of the blocks and the uncompressed size of the blocks.
    /// </summary>
    private static (long BlocksSize, long UncompressedSize)? ReadIndex(byte[] indexBytes)
    {
        // index starts with index indicator 0x00
        if (indexBytes.Length == 0 || indexBytes[0] != 0)
        {
            return null;
        }

        var offset = 1;
        if (!TryReadMultibyteInteger(indexBytes, ref offset, out var numberOfRecords))
        {
            return null;
        }

        var blocksSize = 0L;
        var uncompressedSize = 0L;
        for (var i = 0L; i < numberOfRecords; i++)
        {
            if (!TryReadMultibyteInteger(indexBytes, ref offset, out var unpaddedSize) ||
                !TryReadMultibyteInteger(indexBytes, ref offset, out var blockUncompressedSize))
            {
                return null;
            }

            // blocks are padded to a multiple of 4 bytes
            blocksSize += (unpaddedSize + 3) & ~3L;
            uncompressedSize += blockUncompressedSize;
        }

        // index padding to a multiple of 4 bytes followed by crc32 must fill the rest of the index
        var indexSize = ((offset + 3) & ~3) + 4;
        return indexSize == indexBytes.Length ? (blocksSize, uncompressedSize) : null;
    }

    /// <summary>
    /// Read multibyte integer of 7 bits per byte, least significant byte first and at most 9 bytes.
    /// </summary>
    private static bool TryReadMultibyteInteger(byte[] bytes, ref int offset, out long value)
    {
        value = 0;
        for (var i = 0; i < 9; i++)
        {
            if (offset >= bytes.Length)
            {
                return false;
            }

            var b = bytes[offset++];
            value |= (long)(b & 0x7f) << (i * 7);

            if ((b & 0x80) == 0)
            {
                // value must be stored with minimum bytes and fit in a signed 64-bit integer
                return (i == 0 || b != 0) && value >= 0;
            }
        }

        return false;
    }

    private static bool IsZeroFilled(byte[] bytes)
    {
        foreach (var b in bytes)
        {
            if (b != 0)
            {
                return false;
            }
        }

        return true;
    }
}
