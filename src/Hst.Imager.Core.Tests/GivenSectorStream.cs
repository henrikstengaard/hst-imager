namespace Hst.Imager.Core.Tests
{
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
        public void WhenReadValidCountThenBufferIsRead(int count)
        {
            var sectorStream = new SectorStream(new MemoryStream(bytes), false, false);

            var buffer = new byte[count];
            var bytesRead = sectorStream.Read(buffer, 0, count);
            
            Assert.Equal(count, bytesRead);
        }
        
        [Theory]
        [InlineData(500)]
        [InlineData(940)]
        public void WhenReadInvalidCountThenExceptionIsThrown(int count)
        {
            var sectorStream = new SectorStream(new MemoryStream(bytes), false, false);

            var buffer = new byte[count];

            Assert.Throws<IOException>(() => sectorStream.Read(buffer, 0, count));
        }
        
        [Theory]
        [InlineData(500)]
        [InlineData(940)]
        public void WhenReadInvalidCountWithReadFillTrueThenBufferIsRead(int count)
        {
            var sectorStream = new SectorStream(new MemoryStream(bytes), true, false);

            var buffer = new byte[count];
            var bytesRead = sectorStream.Read(buffer, 0, count);
            
            Assert.Equal(count, bytesRead);
            Assert.Equal(bytes.Take(count), buffer);
        }

        [Theory]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(1024 * 1024)]
        public void WhenWriteValidCountThenBufferIsWritten(int count)
        {
            var sectorStream = new SectorStream(new MemoryStream(), false, false);

            var buffer = new byte[count];
            sectorStream.Write(buffer, 0, count);
            
            Assert.Equal(sectorStream.Length, count);
        }

        [Theory]
        [InlineData(500)]
        [InlineData(940)]
        public void WhenWriteInvalidCountThenExceptionIsThrown(int count)
        {
            var sectorStream = new SectorStream(new MemoryStream(), false, false);

            var buffer = new byte[count];

            Assert.Throws<IOException>(() => sectorStream.Write(buffer, 0, count));
        }

        [Theory]
        [InlineData(500, 512)]
        [InlineData(940, 1024)]
        public void WhenWriteInvalidCountWithWriteFillTrueThenBufferIsWritten(int count, int expected)
        {
            var sectorStream = new SectorStream(new MemoryStream(), false, true);

            var buffer = new byte[count];
            sectorStream.Write(buffer, 0, count);
            
            Assert.Equal(expected, sectorStream.Length);
        }
        
        [Theory]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(1024 * 1024)]
        public void WhenSeekValidOffsetThenOffsetMatches(int offset)
        {
            var sectorStream = new SectorStream(new MemoryStream(bytes), false, false);

            var actualOffset = sectorStream.Seek(offset, SeekOrigin.Begin);
            
            Assert.Equal(offset, actualOffset);
        }

        [Theory]
        [InlineData(500)]
        [InlineData(940)]
        public void WhenSeekInvalidOffsetThenExceptionIsThrown(int offset)
        {
            var sectorStream = new SectorStream(new MemoryStream(), false, false);

            Assert.Throws<IOException>(() => sectorStream.Seek(offset, SeekOrigin.Begin));
        }
    }
}