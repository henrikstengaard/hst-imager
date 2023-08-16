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
        [InlineData(500, 0)]
        [InlineData(940, 512)]
        [InlineData(1100, 1024)]
        public void WhenWriteBytesLessThanSectorSizeThenSectorsAreWrittenAndRemainingDataIsBuffered(int chunkSize,
            int expectedSize)
        {
            var sectorStream = new SectorStream(new MemoryStream());

            var buffer = new byte[chunkSize];
            sectorStream.Write(buffer, 0, chunkSize);
            
            Assert.Equal(expectedSize, sectorStream.Length);
            Assert.Equal(chunkSize % 512, sectorStream.SectorBufferPosition);
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
        public void WhenWriteLessThanSectorSizeAndFlushThenSectorBufferIsZeroFilledAndWritten()
        {
            var baseStream = new MemoryStream(bytes);
            var sectorStream = new SectorStream(baseStream);
            baseStream.Position = 0;

            // arrange - create chunk: 10 bytes of value 1
            var chunkBytes = new byte[10];
            Array.Fill<byte>(chunkBytes, 1);

            // act - write chunk at sector 0
            sectorStream.Seek(0, SeekOrigin.Begin);
            sectorStream.Write(chunkBytes, 0, chunkBytes.Length);

            // assert - base stream length is 0 as chunk 1 written results in following:
            // - fills sector buffer with remaining 10 bytes from chunk.
            Assert.Equal(0, baseStream.Position);
            Assert.Equal(10, sectorStream.SectorBufferPosition);

            // act - flush sector stream to write sector buffer
            sectorStream.Flush();
            
            // assert - base stream length is 512 as sector buffer is written to base stream
            Assert.Equal(512, baseStream.Position);
            Assert.Equal(0, sectorStream.SectorBufferPosition);
            
            // // assert - chunks written are equal with base stream
            // var zeroFillBytes = new byte[512 - chunkBytes.Length % 512];
            // var expectedData = chunkBytes.Concat(zeroFillBytes).ToArray();
            // var actualData = baseStream.ToArray();
            // Assert.Equal(expectedData.Length, actualData.Length);
            // Assert.Equal(expectedData, actualData);
            
            
            
            
            // arrange - create expected sector bytes containing chunk bytes and zero filled to sector size
            var expectedSectorBytes = new byte[512];
            Array.Fill<byte>(expectedSectorBytes, 1, 0, 10);
            
            // assert - read sector 0 matches expected sector bytes
            var actualSectorBytes = new byte[512];
            baseStream.Seek(0, SeekOrigin.Begin);
            var bytesRead = baseStream.Read(actualSectorBytes, 0, actualSectorBytes.Length);
            Assert.Equal(expectedSectorBytes.Length, bytesRead);
            Assert.Equal(expectedSectorBytes, actualSectorBytes);
        }

        [Fact]
        public void WhenWriteBytesLessThanSectorSizeMultipleTimesThenIncompleteSectorIsBufferedUntilItsFull()
        {
            var baseStream = new MemoryStream();
            var sectorStream = new SectorStream(baseStream);

            // arrange - create chunk 1: 400 bytes of value 1
            var chunk1Bytes = new byte[400];
            Array.Fill<byte>(chunk1Bytes, 1);

            // act - write chunk 1 bytes
            sectorStream.Write(chunk1Bytes, 0, chunk1Bytes.Length);
            
            // assert - base stream length is 0 as chunk 1 written to sector stream is less than sector size and
            // is added to sector stream buffer
            Assert.Equal(0, baseStream.Length);
            
            // arrange - create chunk 2: 400 bytes of value 2
            var chunk2Bytes = new byte[400];
            Array.Fill<byte>(chunk2Bytes, 2);

            // act - write chunk 2 bytes
            sectorStream.Write(chunk2Bytes, 0, chunk2Bytes.Length);

            // assert - base stream length is 512 as chunk 2 written to sector stream fills sector buffer
            // from 400 bytes to 512 bytes, writes buffer to base stream and fills sector buffer with
            // remaining 288 bytes from chunk 2
            Assert.Equal(512, baseStream.Length);
            Assert.Equal(288, sectorStream.SectorBufferPosition);
            
            // arrange - create chunk 3: 224 bytes of value 3
            var chunk3Bytes = new byte[224];
            Array.Fill<byte>(chunk3Bytes, 3);
            
            // act - write chunk 3 bytes
            sectorStream.Write(chunk3Bytes, 0, chunk3Bytes.Length);
            
            // assert - base stream length is 1024 as chunk 3 written to sector stream fills sector buffer
            // from 288 bytes to 512 bytes, writes buffer to base stream and with no remaining bytes
            // sector buffer empty
            Assert.Equal(1024, baseStream.Length);
            Assert.Equal(0, sectorStream.SectorBufferPosition);

            // assert - chunks written are equal with base stream
            var expectedData = chunk1Bytes.Concat(chunk2Bytes).Concat(chunk3Bytes).ToArray();
            var actualData = baseStream.ToArray();
            Assert.Equal(expectedData.Length, actualData.Length);
            Assert.Equal(expectedData, actualData);
        }
        
        [Fact]
        public void WhenWriteBytesLessThanSectorSizeAndWriteBytesLargerThanSectorSizeThenIncompleteSectorIsBufferedUntilItsFull()
        {
            var baseStream = new MemoryStream();
            var sectorStream = new SectorStream(baseStream);

            // arrange - create chunk 1: 400 bytes of value 1
            var chunk1Bytes = new byte[400];
            Array.Fill<byte>(chunk1Bytes, 1);

            // act - write chunk 1 bytes
            sectorStream.Write(chunk1Bytes, 0, chunk1Bytes.Length);
            
            // assert - base stream length is 0 as chunk 1 written to sector stream is less than sector size and
            // is added to sector stream buffer
            Assert.Equal(0, baseStream.Length);
            
            // arrange - create chunk 2: 2048 bytes of value 2
            var chunk2Bytes = new byte[2048];
            Array.Fill<byte>(chunk2Bytes, 2);

            // act - write chunk 2 bytes
            sectorStream.Write(chunk2Bytes, 0, chunk2Bytes.Length);

            // assert - base stream length is 2048 as chunk 2 written to sector stream fills sector buffer
            // from 400 bytes to 512 bytes followed by:
            // - writes sector buffer to base stream.
            // - writes chunk 2 offset 112 - 1648 since they are dividable by sector size resulting in 3 sectors of 1536 bytes.
            // - fills sector buffer with remaining 400 bytes from chunk 2.
            Assert.Equal(2048, baseStream.Length);
            Assert.Equal(400, sectorStream.SectorBufferPosition);

            // act - flush sector stream to write sector buffer
            sectorStream.Flush();
            
            // assert - base stream length is 2560 as sector buffer is written to base stream
            Assert.Equal(2560, baseStream.Length);
            Assert.Equal(0, sectorStream.SectorBufferPosition);
            
            // assert - chunks written are equal with base stream
            var zeroFillBytes = new byte[512 - (chunk1Bytes.Length + chunk2Bytes.Length) % 512];
            var expectedData = chunk1Bytes.Concat(chunk2Bytes).Concat(zeroFillBytes).ToArray();
            var actualData = baseStream.ToArray();
            Assert.Equal(expectedData.Length, actualData.Length);
            Assert.Equal(expectedData, actualData);
        }
        
        [Fact]
        public void WhenWriteBytesLargerThanSectorSizeAndWriteBytesLessThanSectorSizeAndThenIncompleteSectorIsBufferedUntilItsFull()
        {
            var baseStream = new MemoryStream();
            var sectorStream = new SectorStream(baseStream);

            // arrange - create chunk 1: 2000 bytes of value 1
            var chunk1Bytes = new byte[2000];
            Array.Fill<byte>(chunk1Bytes, 1);

            // act - write chunk 1 bytes
            sectorStream.Write(chunk1Bytes, 0, chunk1Bytes.Length);
            
            // assert - base stream length is 1536 as chunk 1 written results in following:
            // - writes chunk 1 offset 1 - 1536 since they are dividable by sector size resulting in 3 sectors of 1536 bytes.
            // - fills sector buffer with remaining 464 bytes from chunk 1.
            Assert.Equal(1536, baseStream.Length);
            Assert.Equal(464, sectorStream.SectorBufferPosition);
            
            // arrange - create chunk 2: 400 bytes of value 2
            var chunk2Bytes = new byte[400];
            Array.Fill<byte>(chunk2Bytes, 2);

            // act - write chunk 2 bytes
            sectorStream.Write(chunk2Bytes, 0, chunk2Bytes.Length);

            // assert - base stream length is 2048 as chunk 2 written results in following:
            // - fills sector buffer from 464 bytes to 512 bytes, first 48 bytes os chunk 2
            // - writes sector buffer to base stream.
            // - fills sector buffer with remaining 352 bytes from chunk 2.
            Assert.Equal(2048, baseStream.Length);
            Assert.Equal(352, sectorStream.SectorBufferPosition);

            // act - flush sector stream to write sector buffer
            sectorStream.Flush();
            
            // assert - base stream length is 2560 as sector buffer is written to base stream
            Assert.Equal(2560, baseStream.Length);
            Assert.Equal(0, sectorStream.SectorBufferPosition);
            
            // assert - chunks written are equal with base stream
            var zeroFillBytes = new byte[512 - (chunk1Bytes.Length + chunk2Bytes.Length) % 512];
            var expectedData = chunk1Bytes.Concat(chunk2Bytes).Concat(zeroFillBytes).ToArray();
            var actualData = baseStream.ToArray();
            Assert.Equal(expectedData.Length, actualData.Length);
            Assert.Equal(expectedData, actualData);
        }

        [Fact]
        public void WhenUsingSectorStreamScopeEndsThenSectorBufferIsFlushed()
        {
            // arrange - create chunk of 10 bytes with value 1
            var chunkBytes = new byte[10];
            Array.Fill<byte>(chunkBytes, 1);
                
            // act - write chunk and close, dispose sector stream
            var baseStream = new MemoryStream();
            using (var sectorStream = new SectorStream(baseStream))
            {
                sectorStream.Write(chunkBytes, 0, chunkBytes.Length);
            }

            // assert - base stream contains 512 bytes with chunk bytes and zero filled sector buffer 
            var expectedBytes = chunkBytes.Concat(new byte[512 - chunkBytes.Length]).ToArray();
            var actualBytes = baseStream.ToArray();
            Assert.Equal(expectedBytes.Length, actualBytes.Length);
            Assert.Equal(expectedBytes, actualBytes);
        }
    }
}