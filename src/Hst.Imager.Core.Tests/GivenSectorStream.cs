namespace HstWbInstaller.Imager.Core.Tests
{
    using System.IO;
    using Xunit;

    public class GivenSectorStream
    {
        [Theory]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(1024 * 1024)]
        public void WhenReadValidCountThenBufferIsRead(int count)
        {
            var sectorStream = new SectorStream(new MemoryStream(new byte[count]));

            var buffer = new byte[count];
            var bytesRead = sectorStream.Read(buffer, 0, count);
            
            Assert.Equal(count, bytesRead);
        }
        
        [Theory]
        [InlineData(500)]
        [InlineData(940)]
        public void WhenReadInvalidCountThenExceptionIsThrown(int count)
        {
            var sectorStream = new SectorStream(new MemoryStream(new byte[count]));

            var buffer = new byte[count];

            Assert.Throws<IOException>(() => sectorStream.Read(buffer, 0, count));
        }

        [Theory]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(1024 * 1024)]
        public void WhenWriteValidCountThenBufferIsWritten(int count)
        {
            var sectorStream = new SectorStream(new MemoryStream());

            var buffer = new byte[count];
            sectorStream.Write(buffer, 0, count);
            
            Assert.Equal(sectorStream.Length, count);
        }

        [Theory]
        [InlineData(500)]
        [InlineData(940)]
        public void WhenWriteInvalidCountThenExceptionIsThrown(int count)
        {
            var sectorStream = new SectorStream(new MemoryStream());

            var buffer = new byte[count];

            Assert.Throws<IOException>(() => sectorStream.Write(buffer, 0, count));
        }
        
        [Theory]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(1024 * 1024)]
        public void WhenSeekValidOffsetThenOffsetMatches(int offset)
        {
            var sectorStream = new SectorStream(new MemoryStream(new byte[offset]));

            var actualOffset = sectorStream.Seek(offset, SeekOrigin.Begin);
            
            Assert.Equal(offset, actualOffset);
        }

        [Theory]
        [InlineData(500)]
        [InlineData(940)]
        public void WhenSeekInvalidOffsetThenExceptionIsThrown(int offset)
        {
            var sectorStream = new SectorStream(new MemoryStream());

            Assert.Throws<IOException>(() => sectorStream.Seek(offset, SeekOrigin.Begin));
        }
    }
}