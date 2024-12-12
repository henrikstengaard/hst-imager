using Hst.Core.Extensions;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Hst.Imager.Core.Tests.StreamCopierTests
{
    public class GivenStreamCopierCopyFromAndToSameStreamWithOverlappingSrcAndDest
    {
        private const int SectorSize = 512;

        [Theory]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(1536)]
        [InlineData(2048)]
        [InlineData(4096)]
        [InlineData(1024 * 1024)]
        public async Task When_CopyDataFromStartOfStreamToEndOfStreamWith5SectorImage_Then_DataIsCopied(int bufferSize)
        {
            // arrange - image data bytes
            var imageSize = 5 * SectorSize;
            var data = new byte[imageSize];
            for (var sector = 0; sector < imageSize / SectorSize; sector++)
            {
                Array.Fill(data, (byte)(sector + 1), sector * SectorSize, SectorSize);
            }

            // arrange - source, destination offsets and size to copy
            var srcOffset = 0;
            var destOffset = 2 * SectorSize;
            var copySize = 3 * SectorSize;

            // arrange - stream with data to copy
            using var stream = new MemoryStream(data.Length);
            await stream.WriteAsync(data, 0, data.Length);

            // act - copy from and to same stream
            var streamCopier = new StreamCopier(bufferSize);
            await streamCopier.Copy(CancellationToken.None, stream, stream, copySize, srcOffset, destOffset);

            // assert - expected data is equal to actual data copied
            var expectedData = new byte[data.Length];
            Array.Copy(data, 0, expectedData, 0, data.Length);
            Array.Copy(data, srcOffset, expectedData, destOffset, copySize);
            var actualData = stream.ToArray();
            Assert.Equal(expectedData, actualData);
        }

        [Theory]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(1536)]
        [InlineData(2048)]
        [InlineData(4096)]
        [InlineData(1024 * 1024)]
        public async Task When_CopyDataFromEndOfStreamToStartOfStreamWith5SectorImage_Then_DataIsCopied(int bufferSize)
        {
            // arrange - image data bytes
            var imageSize = 5 * SectorSize;
            var data = new byte[imageSize];
            for (var sector = 0; sector < 5; sector++)
            {
                Array.Fill(data, (byte)(sector + 1), sector * SectorSize, SectorSize);
            }

            // arrange - source, destination offsets and size to copy
            var srcOffset = 2 * SectorSize;
            var destOffset = 0;
            var copySize = 3 * SectorSize;

            // arrange - stream with data to copy
            using var stream = new MemoryStream(data.Length);
            await stream.WriteAsync(data, 0, data.Length);

            // act - copy from and to same stream
            var streamCopier = new StreamCopier(bufferSize);
            await streamCopier.Copy(CancellationToken.None, stream, stream, copySize, srcOffset, destOffset);

            // assert - expected data is equal to actual data copied
            var expectedData = new byte[data.Length];
            Array.Copy(data, 0, expectedData, 0, data.Length);
            Array.Copy(data, srcOffset, expectedData, destOffset, copySize);
            var actualData = stream.ToArray();
            Assert.Equal(expectedData, actualData);
        }

        [Theory]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(1536)]
        [InlineData(2048)]
        [InlineData(4096)]
        [InlineData(1024 * 1024)]
        public async Task When_CopyDataFromStartOfStreamToEndOfStreamWith10MbImage_Then_DataIsCopied(int bufferSize)
        {
            // arrange - image data bytes
            var imageSize = 10.MB().ToSectorSize();
            var data = new byte[imageSize];
            for (var sector = 0; sector < imageSize / SectorSize; sector++)
            {
                Array.Fill(data, (byte)(sector + 1), sector * SectorSize, SectorSize);
            }

            // arrange - source, destination offsets and size to copy
            var copySize = (int)(6.MB().ToSectorSize());
            var srcOffset = 0;
            var destOffset = data.Length - copySize;

            // arrange - stream with data to copy
            using var stream = new MemoryStream(data.Length);
            await stream.WriteAsync(data, 0, data.Length);

            // act - copy from and to same stream
            var streamCopier = new StreamCopier(bufferSize);
            await streamCopier.Copy(CancellationToken.None, stream, stream, copySize, srcOffset, destOffset);

            // assert - expected data is equal to actual data copied
            var expectedData = new byte[data.Length];
            Array.Copy(data, 0, expectedData, 0, data.Length);
            Array.Copy(data, srcOffset, expectedData, destOffset, copySize);
            var actualData = stream.ToArray();
            Assert.Equal(expectedData, actualData);
        }

        [Theory]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(1536)]
        [InlineData(2048)]
        [InlineData(4096)]
        [InlineData(1024 * 1024)]
        public async Task When_CopyDataFromEndOfStreamToStartOfStreamWith10MbImage_Then_DataIsCopied(int bufferSize)
        {
            // arrange - image data bytes
            var imageSize = 10.MB().ToSectorSize();
            var data = new byte[imageSize];
            for (var sector = 0; sector < imageSize / SectorSize; sector++)
            {
                Array.Fill(data, (byte)(sector + 1), sector * SectorSize, SectorSize);
            }

            // arrange - source, destination offsets and size to copy
            var copySize = (int)(6.MB().ToSectorSize());
            var srcOffset = data.Length - copySize;
            var destOffset = 0;

            // arrange - stream with data to copy
            using var stream = new MemoryStream(data.Length);
            await stream.WriteAsync(data, 0, data.Length);

            // act - copy from and to same stream
            var streamCopier = new StreamCopier(bufferSize);
            await streamCopier.Copy(CancellationToken.None, stream, stream, copySize, srcOffset, destOffset);

            // assert - expected data is equal to actual data copied
            var expectedData = new byte[data.Length];
            Array.Copy(data, 0, expectedData, 0, data.Length);
            Array.Copy(data, srcOffset, expectedData, destOffset, copySize);
            var actualData = stream.ToArray();
            Assert.Equal(expectedData, actualData);
        }

        [Fact]
        public async Task When_CopyDataWithSizeZero_Then_ExceptionIsThrown()
        {
            // arrange - image data bytes
            const int imageSize = 1024;
            var data = new byte[imageSize];

            // arrange - source, destination offsets and size to copy
            const int copySize = 0;
            const int srcOffset = 0;
            const int destOffset = 0;

            // arrange - stream with data to copy
            using var stream = new MemoryStream(data.Length);
            await stream.WriteAsync(data, 0, data.Length);

            // act - copy from and to same stream
            var streamCopier = new StreamCopier();
            await Assert.ThrowsAsync<IOException>(async () =>
                await streamCopier.Copy(CancellationToken.None, stream, stream, copySize, srcOffset, destOffset));
        }

        [Fact]
        public async Task When_CopyDataWithUnseekableStream_Then_ExceptionIsThrown()
        {
            // arrange - image data bytes
            const int imageSize = 1024;
            var data = new byte[imageSize];

            // arrange - source, destination offsets and size to copy
            const int copySize = 1024;
            const int srcOffset = 0;
            const int destOffset = 0;

            // arrange - stream with data to copy
            using var stream = new UnseekableStream(new MemoryStream(data.Length));
            await stream.WriteAsync(data, 0, data.Length);

            // act - copy from and to same stream
            var streamCopier = new StreamCopier();
            await Assert.ThrowsAsync<IOException>(async () =>
                await streamCopier.Copy(CancellationToken.None, stream, stream, copySize, srcOffset, destOffset));
        }

        private class UnseekableStream : Stream
        {
            private readonly Stream _stream;

            public UnseekableStream(Stream stream) => _stream = stream;

            public override bool CanRead => _stream.CanRead;

            public override bool CanSeek => false;

            public override bool CanWrite => _stream.CanWrite;

            public override long Length => _stream.Length;

            public override long Position
            {
                get => _stream.Position;
                set => _stream.Position = value;
            }

            public override void Flush()
            {
                _stream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _stream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _stream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                _stream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _stream.Write(buffer, offset, count);
            }
        }
    }
}