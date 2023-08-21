using System;
using System.IO;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenSectorStreamWithByteSwap
{
    private readonly byte[] data;

    public GivenSectorStreamWithByteSwap()
    {
        data = new byte[1024 * 1024];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 255);
        }
    }
    
    private byte[] CreateByteSwappedData(int offset, int count)
    {
        var buffer = new byte[count];
        Array.Copy(data, offset, buffer, 0, count);
        
        for (var i = 0; i < count - count % 2; i += 2)
        {
            (buffer[i + 1], buffer[i]) = (buffer[i], buffer[i + 1]);
        }

        return buffer;
    }
    
    [Fact]
    public void WhenReadDataOfSectorSizeThenDataIsByteSwapped()
    {
        // arrange - sector stream with byte swap 
        var baseStream = new MemoryStream(data);
        var sectorStream = new SectorStream(baseStream, byteSwap: true);

        // act - read 512 bytes of data from sector stream
        var buffer = new byte[512];
        var bytesRead = sectorStream.Read(buffer, 0, buffer.Length);
            
        // assert - 512 bytes was read
        Assert.Equal(512, bytesRead);

        // assert - buffer is byte swapped
        var expectedData = CreateByteSwappedData(0, 512);
        Assert.Equal(expectedData.Length, buffer.Length);
        Assert.Equal(expectedData, buffer);
    }
    
    [Fact]
    public void WhenReadDataLessThanSectorSizeThenDataIsByteSwapped()
    {
        // arrange - sector stream with byte swap 
        var baseStream = new MemoryStream(data);
        var sectorStream = new SectorStream(baseStream, byteSwap: true);

        // act - read 100 bytes of data from sector stream
        var buffer = new byte[100];
        var bytesRead = sectorStream.Read(buffer, 0, buffer.Length);
            
        // assert - 100 bytes was read
        Assert.Equal(100, bytesRead);

        // assert - buffer is byte swapped
        var expectedData = CreateByteSwappedData(0, 100);
        Assert.Equal(expectedData.Length, buffer.Length);
        Assert.Equal(expectedData, buffer);
    }

    [Fact]
    public void WhenReadDataLessThanSectorSizeMultipleTimesThenDataIsByteSwapped()
    {
        // arrange - sector stream with byte swap 
        var baseStream = new MemoryStream(data);
        var sectorStream = new SectorStream(baseStream, byteSwap: true);

        // act - read chunk 1: 100 bytes of data from sector stream
        var chunk1Bytes = new byte[100];
        var bytesRead = sectorStream.Read(chunk1Bytes, 0, chunk1Bytes.Length);
        
        // assert - 100 bytes was read
        Assert.Equal(100, bytesRead);

        // assert - chunk 1 bytes are byte swapped
        var expectedData = CreateByteSwappedData(0, 100);
        Assert.Equal(expectedData.Length, chunk1Bytes.Length);
        Assert.Equal(expectedData, chunk1Bytes);
        
        // act - read chunk 2: 200 bytes of data from sector stream
        var chunk2Bytes = new byte[200];
        bytesRead = sectorStream.Read(chunk2Bytes, 0, chunk2Bytes.Length);
        
        // assert - 200 bytes was read
        Assert.Equal(200, bytesRead);

        // assert - chunk 2 bytes are byte swapped
        expectedData = CreateByteSwappedData(100, 200);
        Assert.Equal(expectedData.Length, chunk2Bytes.Length);
        Assert.Equal(expectedData, chunk2Bytes);
        
        // act - read chunk 3: 150 bytes of data from sector stream
        var chunk3Bytes = new byte[150];
        bytesRead = sectorStream.Read(chunk3Bytes, 0, chunk3Bytes.Length);
        
        // assert - 150 bytes was read
        Assert.Equal(150, bytesRead);

        // assert - chunk 3 bytes are byte swapped
        expectedData = CreateByteSwappedData(300, 150);
        Assert.Equal(expectedData.Length, chunk3Bytes.Length);
        Assert.Equal(expectedData, chunk3Bytes);
    }
    
    [Fact]
    public void WhenReadDataLargerThanSectorSizeThenDataIsByteSwapped()
    {
        // arrange - sector stream with byte swap 
        var baseStream = new MemoryStream(data);
        var sectorStream = new SectorStream(baseStream, byteSwap: true);

        // act - read 612 bytes of data from sector stream
        var buffer = new byte[612];
        var bytesRead = sectorStream.Read(buffer, 0, buffer.Length);
        
        // assert - 612 bytes was read
        Assert.Equal(612, bytesRead);

        // assert - buffer is byte swapped
        var expectedData = CreateByteSwappedData(0, 612);
        Assert.Equal(expectedData.Length, buffer.Length);
        Assert.Equal(expectedData, buffer);
    }
}