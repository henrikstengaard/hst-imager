namespace Hst.Imager.Core.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using Core;
    using Xunit;

    public class GivenSectorStream
    {
        private readonly byte[] bytes;

        public GivenSectorStream()
        {
            bytes = new byte[1024 * 1024];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(i % 255);
            }
        }
        
        [Theory]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(1024 * 1024)]
        public void WhenReadCountThenBufferIsRead(int count)
        {
            var sectorStream = new SectorStream(new MemoryStream(bytes));

            var buffer = new byte[count];
            var bytesRead = sectorStream.Read(buffer, 0, count);
            
            Assert.Equal(count, bytesRead);
        }
        
        [Fact]
        public void WhenReadOffsetNotZeroThenExceptionIsThrown()
        {
            var sectorStream = new SectorStream(new MemoryStream(bytes));

            var buffer = new byte[512];

            Assert.Throws<ArgumentOutOfRangeException>(() => sectorStream.Read(buffer, 1, buffer.Length));
        }
        
        [Theory]
        [InlineData(500)]
        [InlineData(940)]
        public void WhenReadCountNotDividableBySectorSizeTrueThenBufferIsRead(int count)
        {
            var sectorStream = new SectorStream(new MemoryStream(bytes));

            var buffer = new byte[count];
            var bytesRead = sectorStream.Read(buffer, 0, count);
            
            Assert.Equal(count, bytesRead);
            Assert.Equal(bytes.Take(count), buffer);
        }

        [Theory]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(1024 * 1024)]
        public void WhenWriteCountThenBufferIsWritten(int count)
        {
            var sectorStream = new SectorStream(new MemoryStream());

            var buffer = new byte[count];
            sectorStream.Write(buffer, 0, count);
            
            Assert.Equal(sectorStream.Length, count);
        }

        [Theory]
        [InlineData(500, 512)]
        [InlineData(940, 1024)]
        public void WhenWriteCountNotDividableBySectorSizeThenBufferIsWritten(int count, int expected)
        {
            var sectorStream = new SectorStream(new MemoryStream());

            var buffer = new byte[count];
            sectorStream.Write(buffer, 0, count);
            
            Assert.Equal(expected, sectorStream.Length);
        }
        
        [Theory]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(1024 * 1024)]
        public void WhenSeekOffsetThenOffsetMatches(int offset)
        {
            var sectorStream = new SectorStream(new MemoryStream(bytes));

            var actualOffset = sectorStream.Seek(offset, SeekOrigin.Begin);
            
            Assert.Equal(offset, actualOffset);
        }

        [Theory]
        [InlineData(500)]
        [InlineData(940)]
        public void WhenSeekNotDividableBySectorSizeThenExceptionIsThrown(int offset)
        {
            var sectorStream = new SectorStream(new MemoryStream());

            Assert.Throws<ArgumentOutOfRangeException>(() => sectorStream.Seek(offset, SeekOrigin.Begin));
        }
        
        [Fact]
        public void WhenWriteLessThanSectorSizeThenSectorIsZeroFilled()
        {
            var sectorStream = new SectorStream(new MemoryStream(bytes));

            // arrange - create 10 data bytes of 1'es
            var dataBytes = new byte[10];
            Array.Fill<byte>(dataBytes, 1);

            // act - write data bytes at sector 0
            sectorStream.Seek(0, SeekOrigin.Begin);
            sectorStream.Write(dataBytes, 0, dataBytes.Length);

            // arrange - create expected sector bytes containing data bytes and zero filled to sector size
            var expectedSectorBytes = new byte[512];
            Array.Fill<byte>(expectedSectorBytes, 1, 0, 10);
            
            // assert - read sector 0 matches expected sector bytes
            var actualSectorBytes = new byte[512];
            sectorStream.Seek(0, SeekOrigin.Begin);
            var bytesRead = sectorStream.Read(actualSectorBytes, 0, actualSectorBytes.Length);
            Assert.Equal(expectedSectorBytes.Length, bytesRead);
            Assert.Equal(expectedSectorBytes, actualSectorBytes);
        }
    }
}