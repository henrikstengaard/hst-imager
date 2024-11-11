using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenSectorStreamWithBufferSize4096
{
    private const int SectorSize = 512;
    private const int BufferSize = 4096;
    
    [Theory]
    [InlineData(512, 300, 100, 0)]
    [InlineData(1024, 300, 100, 0)]
    [InlineData(1536, 700, 100, 512)]
    public void When_WriteDataAtOffsetInOneSector_Then_SectorIsOverwritten(int sectorBytesLength, int writeOffset,
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
        using (var sectorStream = new SectorStream(monitorStream, bufferSize: BufferSize))
        {
            // act - seek to write offset
            sectorStream.Seek(writeOffset, SeekOrigin.Begin);
            
            // assert - seek activity triggered by seeking to write offset
            Assert.Single(monitorStream.Activities);
            var seekActivity = monitorStream.Activities.Last() as SeekActivity;
            Assert.NotNull(seekActivity);
            Assert.Equal(0, seekActivity.Position);
            Assert.Equal(overwrittenSectorOffset, seekActivity.Offset);
            
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