using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenSectorStreamReadingData
{
    private const int SectorSize = 512;

    [Theory]
    [InlineData(512, 200)]
    [InlineData(512, 800)]
    [InlineData(512, 1200)]
    [InlineData(4096, 200)]
    [InlineData(4096, 800)]
    [InlineData(4096, 1200)]
    public void When_ReadingDataWithByteSwap_Then_DataReadIsByteSwapped(int bufferSize, int readLength)
    {
        // arrange - bytes to read
        var testDataBytes = TestDataHelper.CreateTestData(readLength);
        
        var expectedData = TestDataHelper.CreateTestData(readLength);
        TestDataHelper.ByteSwapData(expectedData);
        
        // arrange - memory and activity monitor stream
        var memoryStream = new MemoryStream(testDataBytes);
        var monitorStream = new ActivityMonitorStream(memoryStream);

        var actualData = new byte[readLength];
        
        // act - read byte swapped data
        using (var sectorStream = new SectorStream(monitorStream, bufferSize: bufferSize, byteSwap: true))
        {
            var bytesRead = sectorStream.Read(actualData, 0, readLength);
            Assert.Equal(readLength, bytesRead);
        }

        // assert - actual data read is equal to expected data
        Assert.Equal(expectedData.Length, actualData.Length);
        Assert.Equal(expectedData, actualData);

        var sectorsRead = Math.Ceiling((double)readLength / bufferSize);
        
        // assert - stream activities
        var activities = monitorStream.Activities.ToList();
        Assert.Equal(1 + (sectorsRead * 2), activities.Count);

        for (var i = 0; i < sectorsRead; i++)
        {
            // assert - seek activity to position 0 triggered by reading data
            // triggered by stream read
            var seekActivity = activities[i * 2] as SeekActivity;
            Assert.NotNull(seekActivity);
            Assert.Equal(i * SectorSize, seekActivity.Position);
            Assert.Equal(i * SectorSize, seekActivity.Offset);

            // assert - read activity from position 0
            // triggered by stream read
            var readActivity = activities[i * 2 + 1] as ReadActivity;
            Assert.NotNull(readActivity);
            Assert.Equal(i * SectorSize, readActivity.Position);
            Assert.Equal(0, readActivity.Offset);
            var expectedReadCount = bufferSize > SectorSize
                ? readLength - (readLength % SectorSize) + SectorSize
                : SectorSize;
            Assert.Equal(expectedReadCount, readActivity.Count);
        }
        
        // assert - flush activity
        // triggered by disposing sector stream
        var flushActivity = activities[^1] as FlushActivity;
        Assert.NotNull(flushActivity);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(1024 * 1024)]
    public void When_ReadingDataPastEndOfStream_Then_DataUntilEndOfStreamIsRead(int bufferSize)
    {
        // arrange - create test data
        var data = new byte[75899];
        Array.Fill<byte>(data, 1);

        // arrange - create sector stream
        using var memoryStream = new MemoryStream(data);
        using var sectorStream = new SectorStream(memoryStream, bufferSize: bufferSize);
        
        // act - read 75898 bytes
        var buffer1 = new byte[75898];
        var bytesRead = sectorStream.Read(buffer1, 0, buffer1.Length);
        
        // assert - 75898 bytes was read
        Assert.Equal(75898, bytesRead);
        Assert.Equal(data.Take(75898), buffer1);
            
        // act - read 21 bytes, 20 bytes past end of stream data
        var buffer2 = new byte[21];
        bytesRead = sectorStream.Read(buffer2, 0, buffer2.Length);

        // assert - 1 byte was read
        Assert.Equal(1, bytesRead);
        var expectedBuffer2Data = new byte[21];
        expectedBuffer2Data[0] = 1;
        Assert.Equal(expectedBuffer2Data, buffer2);
    }
}