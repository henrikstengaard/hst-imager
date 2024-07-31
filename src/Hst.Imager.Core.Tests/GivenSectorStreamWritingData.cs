using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenSectorStreamWritingData
{
    private const int SectorSize = 512;

    [Fact]
    public void Test()
    {
        // arrange - bytes to write
        var sectorBytes = TestDataHelper.CreateTestData(SectorSize);

        // arrange - memory and activity monitor stream
        var memoryStream = new MemoryStream();
        var monitorStream = new ActivityMonitorStream(memoryStream);

        // act - seek to write offset and write bytes
        using (var sectorStream = new SectorStream(monitorStream, bufferSize: SectorSize))
        {
            sectorStream.Write(sectorBytes, 0, sectorBytes.Length);
        }
        
        // assert - 3 stream activities
        var activities = monitorStream.Activities.ToList();
        Assert.Equal(3, activities.Count);
        
        // manually seeking to position 0
        var seekActivity1 = activities[0] as SeekActivity;
        Assert.NotNull(seekActivity1);
        Assert.Equal(0, seekActivity1.Position);
        Assert.Equal(0, seekActivity1.Offset);

        // assert - write activity from position overwritten sector offset
        // triggered by disposing sector stream as sector bytes was updated and not written
        var writeActivity = activities[1] as WriteActivity;
        Assert.NotNull(writeActivity);
        Assert.Equal(0, writeActivity.Position);
        Assert.Equal(0, writeActivity.Offset);
        Assert.Equal(SectorSize, writeActivity.Count);

        // assert - flush activity
        // triggered by disposing sector stream
        var flushActivity = activities[2] as FlushActivity;
        Assert.NotNull(flushActivity);
    }

    [Fact]
    public void When_WritingDataWithByteSwap_Then_DataWrittenIsByteSwapped()
    {
        // arrange - bytes to write
        var sectorBytes = TestDataHelper.CreateTestData(SectorSize);
        
        var expectedData = TestDataHelper.CreateTestData(SectorSize);
        TestDataHelper.ByteSwapData(expectedData);
        
        // arrange - memory and activity monitor stream
        var memoryStream = new MemoryStream();
        var monitorStream = new ActivityMonitorStream(memoryStream);

        // act - seek to write offset and write bytes
        using (var sectorStream = new SectorStream(monitorStream, bufferSize: 1024 * 1024, byteSwap: true))
        {
            sectorStream.Write(sectorBytes, 0, sectorBytes.Length);
        }

        // assert data
        var actualData = memoryStream.ToArray();
        Assert.Equal(expectedData.Length, actualData.Length);
        Assert.Equal(expectedData, actualData);
        
        // assert - 3 stream activities
        var activities = monitorStream.Activities.ToList();
        Assert.Equal(3, activities.Count);
        
        // manually seeking to position 0
        var seekActivity1 = activities[0] as SeekActivity;
        Assert.NotNull(seekActivity1);
        Assert.Equal(0, seekActivity1.Position);
        Assert.Equal(0, seekActivity1.Offset);

        // assert - write activity from position overwritten sector offset
        // triggered by disposing sector stream as sector bytes was updated and not written
        var writeActivity = activities[1] as WriteActivity;
        Assert.NotNull(writeActivity);
        Assert.Equal(0, writeActivity.Position);
        Assert.Equal(0, writeActivity.Offset);
        Assert.Equal(SectorSize, writeActivity.Count);

        // assert - flush activity
        // triggered by disposing sector stream
        var flushActivity = activities[2] as FlushActivity;
        Assert.NotNull(flushActivity);
    }
    
    [Fact]
    public void When_WritingDataSmallerThanBufferAndSeek_Then_BufferIsWritten()
    {
        var sectorBytes = TestDataHelper.CreateTestData(SectorSize);

        // arrange - memory and activity monitor stream
        var memoryStream = new MemoryStream();
        var monitorStream = new ActivityMonitorStream(memoryStream);

        // act - seek to write offset and write bytes
        using (var sectorStream = new SectorStream(monitorStream, bufferSize: 1024 * 1024))
        {
            sectorStream.Write(sectorBytes, 0, sectorBytes.Length);
            sectorStream.Seek(512, SeekOrigin.Begin);
        }

        var actualData = memoryStream.ToArray();
        Assert.Equal(sectorBytes.Length, actualData.Length);
        Assert.Equal(sectorBytes, actualData);
        
        // assert - 4 stream activities
        var activities = monitorStream.Activities.ToList();
        Assert.Equal(4, activities.Count);
        
        // assert - seek activity to position 0
        // triggered by manually seeking to position 512 writing updates sector bytes
        var seekActivity1 = activities[0] as SeekActivity;
        Assert.NotNull(seekActivity1);
        Assert.Equal(0, seekActivity1.Position);
        Assert.Equal(0, seekActivity1.Offset);

        // assert - write activity from position overwritten sector offset
        // triggered by manually seeking to position 512 writing updates sector bytes
        var writeActivity = activities[1] as WriteActivity;
        Assert.NotNull(writeActivity);
        Assert.Equal(0, writeActivity.Position);
        Assert.Equal(0, writeActivity.Offset);
        Assert.Equal(SectorSize, writeActivity.Count);

        // manually seeking to position 512
        var seekActivity2 = activities[2] as SeekActivity;
        Assert.NotNull(seekActivity2);
        Assert.Equal(512, seekActivity2.Position);
        Assert.Equal(512, seekActivity2.Offset);

        // assert - flush activity
        // triggered by disposing sector stream
        var flushActivity = activities[3] as FlushActivity;
        Assert.NotNull(flushActivity);
    }

    [Theory]
    [InlineData(512, 512, 300, 100, 0)]
    [InlineData(512, 1024, 300, 100, 0)]
    [InlineData(512, 1536, 700, 100, 512)]
    [InlineData(4096, 512, 300, 100, 0)]
    [InlineData(4096, 1024, 300, 100, 0)]
    [InlineData(4096, 1536, 700, 100, 512)]
    public void When_WriteDataAtOffsetInOneSector_Then_SectorIsOverwritten(
        int bufferSize,
        int sectorBytesLength,
        int writeOffset,
        int writeLength,
        int overwrittenSectorOffset)
    {
        var sectorBytes = TestDataHelper.CreateTestData(sectorBytesLength);

        // arrange - bytes to write
        var writeBytes = new byte[writeLength];
        Array.Fill<byte>(writeBytes, 1);

        // arrange - expected sector bytes
        var expectedSectorBytes = new byte[sectorBytesLength];
        Array.Copy(sectorBytes, 0, expectedSectorBytes, 0, sectorBytesLength);
        var expectedOverwrittenSectorBytes = new byte[SectorSize];
        Array.Fill<byte>(expectedOverwrittenSectorBytes, 1, writeOffset % SectorSize, writeLength);
        Array.Copy(expectedOverwrittenSectorBytes, 0, expectedSectorBytes,
            overwrittenSectorOffset, expectedOverwrittenSectorBytes.Length);

        // arrange - memory and activity monitor stream
        var memoryStream = new MemoryStream(sectorBytes);
        var monitorStream = new ActivityMonitorStream(memoryStream);

        // arrange - sector stream with buffer size
        using (var sectorStream = new SectorStream(monitorStream, bufferSize: bufferSize))
        {
            // act - seek to write offset
            sectorStream.Seek(writeOffset, SeekOrigin.Begin);

            // assert - seek activity triggered by seeking to write offset
            Assert.Equal(1, monitorStream.Activities.Count);
            var seekActivity1 = monitorStream.Activities.Last() as SeekActivity;
            Assert.NotNull(seekActivity1);
            Assert.Equal(0, seekActivity1.Position);
            Assert.Equal(overwrittenSectorOffset, seekActivity1.Offset);
            
            // act - write bytes
            sectorStream.Write(writeBytes, 0, writeLength);
        }
        
        // assert - 3 activities
        var activities = monitorStream.Activities.ToList();
        Assert.Equal(3, monitorStream.Activities.Count);

        // assert - write activity triggered by flush, disposing stream
        var writeActivity = activities[1] as WriteActivity;
        Assert.NotNull(writeActivity);
        Assert.Equal(overwrittenSectorOffset, writeActivity.Position);
        Assert.Equal(0, writeActivity.Offset);
        Assert.Equal(SectorSize, writeActivity.Count);

        // assert - flush activity triggered by disposing stream
        var flushActivity = activities[2] as FlushActivity;
        Assert.NotNull(flushActivity);
        
        // assert - expected sector bytes are equal to updated sector bytes
        var updatedSectorBytes = memoryStream.ToArray();
        Assert.Equal(expectedSectorBytes.Length, updatedSectorBytes.Length);
        Assert.Equal(expectedSectorBytes, updatedSectorBytes);
    }
}