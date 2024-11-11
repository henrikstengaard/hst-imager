using System.IO;
using System.Linq;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenSectorStreamSeekingData
{
    [Fact]
    public void When_SeekingToOffset_Then_OffsetIsReturned()
    {
        var sectorBytes = TestDataHelper.CreateTestData(4096);
        
        // arrange - memory and activity monitor stream
        var memoryStream = new MemoryStream(sectorBytes);
        var monitorStream = new ActivityMonitorStream(memoryStream);

        // act - seek to write offset and write bytes
        long offset;
        using (var sectorStream = new SectorStream(monitorStream))
        {
            offset = sectorStream.Seek(700, SeekOrigin.Begin);
        }
        Assert.Equal(700, offset);
        
        // assert - 2 stream activity
        var activities = monitorStream.Activities.ToList();
        Assert.Equal(2, activities.Count);

        // assert - seek activity from position 0 to offset 512, start offset of sector seeked to
        var seekActivity = activities[0] as SeekActivity;
        Assert.NotNull(seekActivity);
        Assert.Equal(0, seekActivity.Position);
        Assert.Equal(512, seekActivity.Offset);
        
        // assert - flush activity
        var flushActivity = activities[1] as FlushActivity;
        Assert.NotNull(flushActivity);
    }
    
    [Fact]
    public void When_SeekingWithinSameSectorReadingData_Then_BaseStreamIsOnlySeekedOnce()
    {
        var sectorBytes = TestDataHelper.CreateTestData(4096);
        
        // arrange - memory and activity monitor stream
        var memoryStream = new MemoryStream(sectorBytes);
        var monitorStream = new ActivityMonitorStream(memoryStream);

        // arrange - sector stream
        using (var sectorStream = new SectorStream(monitorStream))
        {
            // act - seek to position 700
            sectorStream.Seek(700, SeekOrigin.Begin);

            // assert - seek activity triggered by seeking to offset 700
            Assert.Single(monitorStream.Activities);
            var seekActivity = monitorStream.Activities.Last() as SeekActivity;
            Assert.NotNull(seekActivity);
            Assert.Equal(0, seekActivity.Position);
            Assert.Equal(512, seekActivity.Offset);

            // act - read 10 bytes
            var data = new byte[10];
            var bytesRead = sectorStream.Read(data, 0, data.Length);
            Assert.Equal(10, bytesRead);

            // assert - read activity triggered by reading 10 bytes
            Assert.Equal(2, monitorStream.Activities.Count);
            var readActivity = monitorStream.Activities.Last() as ReadActivity;
            Assert.NotNull(readActivity);
            Assert.Equal(512, readActivity.Position);
            Assert.Equal(0, readActivity.Offset);

            // act - seek to position 800
            sectorStream.Seek(800, SeekOrigin.Begin);
            
            // assert - monitor stream did not result in any activity as seek to position is within same sector
            Assert.Equal(2, monitorStream.Activities.Count);
        }
        
        // assert - flush activity triggered by disposing stream
        Assert.Equal(3, monitorStream.Activities.Count);
        var flushActivity = monitorStream.Activities.Last() as FlushActivity;
        Assert.NotNull(flushActivity);
    }

    [Fact]
    public void When_SeekingWithinSameSectorNotReadingData_Then_BaseStreamIsSeeked()
    {
        var sectorBytes = TestDataHelper.CreateTestData(4096);
        
        // arrange - memory and activity monitor stream
        var memoryStream = new MemoryStream(sectorBytes);
        var monitorStream = new ActivityMonitorStream(memoryStream);

        // act - seek to write offset and write bytes
        using (var sectorStream = new SectorStream(monitorStream))
        {
            sectorStream.Seek(700, SeekOrigin.Begin);
            sectorStream.Seek(800, SeekOrigin.Begin);
        }
        
        // assert - 3 stream activities
        var activities = monitorStream.Activities.ToList();
        Assert.Equal(3, activities.Count);
        
        // assert - seek activity from position 0 to offset 512, start offset of sector seeked to
        var seekActivity1 = activities[0] as SeekActivity;
        Assert.NotNull(seekActivity1);
        Assert.Equal(0, seekActivity1.Position);
        Assert.Equal(512, seekActivity1.Offset);
        
        // assert - seek activity from position 0 to offset 512, start offset of sector seeked to
        var seekActivity2 = activities[1] as SeekActivity;
        Assert.NotNull(seekActivity2);
        Assert.Equal(512, seekActivity2.Position);
        Assert.Equal(512, seekActivity2.Offset);
        
        // assert - flush activity
        var flushActivity = activities[2] as FlushActivity;
        Assert.NotNull(flushActivity);
    }

    [Fact]
    public void When_SeekingCurrentOrigin_Then_BaseStreamIsSeekedToSectorOffset()
    {
        var sectorBytes = TestDataHelper.CreateTestData(4096);
        
        // arrange - memory and activity monitor stream
        var memoryStream = new MemoryStream(sectorBytes);
        var monitorStream = new ActivityMonitorStream(memoryStream);

        // act - seek to write offset and write bytes
        using (var sectorStream = new SectorStream(monitorStream))
        {
            var offset1 = sectorStream.Seek(500, SeekOrigin.Current);
            Assert.Equal(500, offset1);
            var offset2 = sectorStream.Seek(200, SeekOrigin.Current);
            Assert.Equal(700, offset2);
        }
        
        // assert - 2 stream activity
        var activities = monitorStream.Activities.ToList();
        Assert.Equal(3, activities.Count);
        
        // assert - seek activity from position 0 to offset 512, start offset of sector seeked to
        var seekActivity1 = activities[0] as SeekActivity;
        Assert.NotNull(seekActivity1);
        Assert.Equal(0, seekActivity1.Position);
        Assert.Equal(0, seekActivity1.Offset);
        Assert.Equal(SeekOrigin.Current, seekActivity1.Origin);

        // assert - seek activity from position 0 to offset 512, start offset of sector seeked to
        var seekActivity2 = activities[1] as SeekActivity;
        Assert.NotNull(seekActivity2);
        Assert.Equal(0, seekActivity2.Position);
        Assert.Equal(512, seekActivity2.Offset);
        Assert.Equal(SeekOrigin.Current, seekActivity2.Origin);

        // assert - flush activity
        var flushActivity = activities[2] as FlushActivity;
        Assert.NotNull(flushActivity);
    }
}