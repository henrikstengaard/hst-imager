using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Compressions.Xz;
using Xunit;

namespace Hst.Imager.Core.Tests.CompressionTests;

public class GivenXzIndexReader
{
    [Fact]
    public async Task WhenReadUncompressedSizeFromXzCompressedImgThenSizeIsReadFromIndexWithoutReadingAllData()
    {
        // arrange - xz compressed img
        var xzPath = Path.Combine("TestData", "compressed-images", "1gb.img.xz");
        await using var stream = new ReadCountingStream(new MemoryStream(await File.ReadAllBytesAsync(xzPath)));

        // act - read uncompressed size
        var uncompressedSize = await XzIndexReader.ReadUncompressedSize(stream);

        // assert - uncompressed size matches uncompressed img size
        Assert.Equal(1020055040, uncompressedSize);

        // assert - only stream header, index and stream footer are read and not compressed data
        Assert.True(stream.BytesRead < 1024, $"Bytes read {stream.BytesRead} is not less than 1024");
    }

    [Fact]
    public async Task WhenReadUncompressedSizeFromConcatenatedXzStreamsWithPaddingThenSizeIsSumOfStreams()
    {
        // arrange - two concatenated xz streams with 6 blocks and stream padding between and after streams
        var xzPath = Path.Combine("TestData", "Xz", "multi-stream.bin.xz");
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(xzPath));

        // act - read uncompressed size
        var uncompressedSize = await XzIndexReader.ReadUncompressedSize(stream);

        // assert - uncompressed size is sum of uncompressed size of 300000 and 1000 bytes in xz streams
        Assert.Equal(301000, uncompressedSize);
    }

    [Fact]
    public async Task WhenReadUncompressedSizeFromTruncatedXzDataThenNullIsReturned()
    {
        // arrange - xz compressed img truncated, so stream footer is missing
        var xzPath = Path.Combine("TestData", "compressed-images", "1gb.img.xz");
        var data = await File.ReadAllBytesAsync(xzPath);
        await using var stream = new MemoryStream(data, 0, data.Length - 100);

        // act - read uncompressed size
        var uncompressedSize = await XzIndexReader.ReadUncompressedSize(stream);

        // assert - uncompressed size is unknown
        Assert.Null(uncompressedSize);
    }

    [Fact]
    public async Task WhenReadUncompressedSizeFromNonXzDataThenNullIsReturned()
    {
        // arrange - gzip compressed data
        var gzPath = Path.Combine("TestData", "compressed-images", "1gb.img.gz");
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(gzPath));

        // act - read uncompressed size
        var uncompressedSize = await XzIndexReader.ReadUncompressedSize(stream);

        // assert - uncompressed size is unknown
        Assert.Null(uncompressedSize);
    }

    /// <summary>
    /// Stream counting bytes read from base stream.
    /// </summary>
    private class ReadCountingStream(Stream baseStream) : Stream
    {
        public long BytesRead { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = baseStream.Read(buffer, offset, count);
            BytesRead += bytesRead;
            return bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            var bytesRead = await baseStream.ReadAsync(buffer, offset, count, cancellationToken);
            BytesRead += bytesRead;
            return bytesRead;
        }

        public override async ValueTask<int> ReadAsync(System.Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var bytesRead = await baseStream.ReadAsync(buffer, cancellationToken);
            BytesRead += bytesRead;
            return bytesRead;
        }

        public override void Flush() => baseStream.Flush();
        public override long Seek(long offset, SeekOrigin origin) => baseStream.Seek(offset, origin);
        public override void SetLength(long value) => baseStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => baseStream.Write(buffer, offset, count);
        public override bool CanRead => baseStream.CanRead;
        public override bool CanSeek => baseStream.CanSeek;
        public override bool CanWrite => baseStream.CanWrite;
        public override long Length => baseStream.Length;

        public override long Position
        {
            get => baseStream.Position;
            set => baseStream.Position = value;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                baseStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
