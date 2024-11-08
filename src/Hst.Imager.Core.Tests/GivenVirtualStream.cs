using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Hst.Imager.Core.Tests
{
    public class GivenVirtualStream
    {
        [Fact]
        public async Task When_ReadFromStartOfStream_Then_DataIsRead()
        {
            // arrange
            var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var memoryStream = new MemoryStream(data);
            var virtualStream = new VirtualStream(memoryStream, 0);
            var actualData = new byte[5];

            // act
            var bytesRead = await virtualStream.ReadAsync(actualData, 0, actualData.Length);

            // assert
            Assert.Equal(5, bytesRead);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, actualData);
            Assert.Equal(5, virtualStream.Position);
            Assert.Equal(5, memoryStream.Position);
        }

        [Fact]
        public async Task When_ReadFromStartOfStreamWithMaxSize_Then_DataIsReadUntilMaxSize()
        {
            // arrange
            var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var memoryStream = new MemoryStream(data);
            var virtualStream = new VirtualStream(memoryStream, 0, 3);
            var actualData = new byte[5];

            // act
            var bytesRead = await virtualStream.ReadAsync(actualData, 0, actualData.Length);

            // assert
            Assert.Equal(3, bytesRead);
            Assert.Equal(new byte[] { 1, 2, 3, 0, 0 }, actualData);
            Assert.Equal(3, virtualStream.Position);
            Assert.Equal(3, memoryStream.Position);
        }

        [Fact]
        public async Task When_ReadToEndOfStream_Then_DataIsRead()
        {
            // arrange
            var data = new byte[]{ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var memoryStream = new MemoryStream(data);
            var virtualStream = new VirtualStream(memoryStream, 5);
            var actualData = new byte[5];
            
            // act
            var bytesRead = await virtualStream.ReadAsync(actualData, 0, actualData.Length);

            // assert
            Assert.Equal(5, bytesRead);
            Assert.Equal(new byte[] { 6, 7, 8, 9, 10 }, actualData);
            Assert.Equal(5, virtualStream.Position);
            Assert.Equal(10, memoryStream.Position);
        }

        [Fact]
        public async Task When_ReadPastEndOfStream_Then_DataIsRead()
        {
            // arrange
            var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var memoryStream = new MemoryStream(data);
            var virtualStream = new VirtualStream(memoryStream, 7);
            var actualData = new byte[5];

            // act
            var bytesRead = await virtualStream.ReadAsync(actualData, 0, actualData.Length);

            // assert
            Assert.Equal(3, bytesRead);
            Assert.Equal(new byte[] { 8, 9, 10, 0, 0 }, actualData);
            Assert.Equal(3, virtualStream.Position);
            Assert.Equal(10, memoryStream.Position);
        }

        [Fact]
        public async Task When_ReadAtEndOfStream_Then_DataIsRead()
        {
            // arrange
            var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var memoryStream = new MemoryStream(data);
            var virtualStream = new VirtualStream(memoryStream, 10);
            var actualData = new byte[5];

            // act
            var bytesRead = await virtualStream.ReadAsync(actualData, 0, actualData.Length);

            // assert
            Assert.Equal(0, bytesRead);
            Assert.Equal(new byte[] { 0, 0, 0, 0, 0 }, actualData);
            Assert.Equal(0, virtualStream.Position);
            Assert.Equal(10, memoryStream.Position);
        }
        
        [Fact]
        public async Task When_WriteWithStartOffsetInStream_Then_DataIsWritten()
        {
            var memoryStream = new MemoryStream();
            var virtualStream = new VirtualStream(memoryStream, 10);
            var actualData = new byte[] { 1, 2, 3, 4, 5 };

            // act
            await virtualStream.WriteAsync(actualData, 0, actualData.Length);

            // assert
            Assert.Equal(5, virtualStream.Position);
            Assert.Equal(5, virtualStream.Length);
            Assert.Equal(15, memoryStream.Length);
            Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5 },
                memoryStream.ToArray());
        }
        
        [Theory]
        [InlineData(3)]
        [InlineData(7)]
        public async Task When_WriteAtStartOfStream_Then_DataIsWritten(int maxSize)
        {
            var memoryStream = new MemoryStream();
            var virtualStream = new VirtualStream(memoryStream, 0, maxSize);
            var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            // act
            await virtualStream.WriteAsync(data, 0, data.Length);

            // assert
            Assert.Equal(maxSize, virtualStream.Position);
            Assert.Equal(maxSize, virtualStream.Length);
            Assert.Equal(maxSize, memoryStream.Length);
            Assert.Equal(data.Take(maxSize).ToArray(), memoryStream.ToArray());
        }

        [Fact]
        public async Task When_WritePastEndOfStream_Then_DataIsWritten()
        {
            var memoryStream = new MemoryStream();
            var virtualStream = new VirtualStream(memoryStream, 0, 5);
            var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            // act
            await virtualStream.WriteAsync(data, 0, data.Length);

            // assert
            Assert.Equal(5, virtualStream.Position);
            Assert.Equal(5, virtualStream.Length);
            Assert.Equal(5, memoryStream.Length);
            Assert.Equal(data.Take(5).ToArray(), memoryStream.ToArray());
        }
    }
}
