using System.IO;
using System.Linq;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenDeferredStartWriteStream
{
    private const int SectorSize = 512;
    private const int StartSize = SectorSize * 2;

    [Fact]
    public void WhenWritingDataThenStartIsWrittenLastWhenDisposed()
    {
        // arrange - data to write and base stream
        var data = TestDataHelper.CreateTestData(SectorSize * 4);
        var memoryStream = new MemoryStream(new byte[data.Length]);
        var monitorStream = new ActivityMonitorStream(memoryStream);
        var deferredStartWriteStream = new DeferredStartWriteStream(monitorStream, StartSize);

        // act - write data
        deferredStartWriteStream.Write(data, 0, data.Length);

        // assert - start is not written and data after start is written
        var bytes = memoryStream.ToArray();
        Assert.Equal(new byte[StartSize], bytes.Take(StartSize).ToArray());
        Assert.Equal(data.Skip(StartSize).ToArray(), bytes.Skip(StartSize).ToArray());

        // act - dispose deferred start write stream
        deferredStartWriteStream.Dispose();

        // assert - all data is written and last write is start
        Assert.Equal(data, memoryStream.ToArray());
        var lastWriteActivity = monitorStream.Activities.OfType<WriteActivity>().Last();
        Assert.Equal(0, lastWriteActivity.Position);
        Assert.Equal(StartSize, lastWriteActivity.Count);
    }

    [Fact]
    public void WhenWritingPartOfStartThenStartNotWrittenIsPreserved()
    {
        // arrange - existing data in base stream and data to write
        var existingData = TestDataHelper.CreateTestData(SectorSize * 4);
        var data = new byte[SectorSize];
        System.Array.Fill<byte>(data, 0xff);
        var memoryStream = new MemoryStream();
        memoryStream.Write(existingData, 0, existingData.Length);

        // act - write data to second sector
        using (var deferredStartWriteStream = new DeferredStartWriteStream(memoryStream, StartSize))
        {
            deferredStartWriteStream.Seek(SectorSize, SeekOrigin.Begin);
            deferredStartWriteStream.Write(data, 0, data.Length);
        }

        // assert - second sector is updated and rest of data is preserved
        var expectedData = existingData.ToArray();
        System.Array.Copy(data, 0, expectedData, SectorSize, data.Length);
        Assert.Equal(expectedData, memoryStream.ToArray());
    }

    [Fact]
    public void WhenReadingDataWrittenBeforeDisposedThenDataIsReturned()
    {
        // arrange - data to write and base stream
        var data = TestDataHelper.CreateTestData(SectorSize * 4);
        var memoryStream = new MemoryStream(new byte[data.Length]);
        using var deferredStartWriteStream = new DeferredStartWriteStream(memoryStream, StartSize);
        deferredStartWriteStream.Write(data, 0, data.Length);

        // act - read data
        deferredStartWriteStream.Seek(0, SeekOrigin.Begin);
        var buffer = new byte[data.Length];
        var bytesRead = deferredStartWriteStream.Read(buffer, 0, buffer.Length);

        // assert - data read is equal to data written
        Assert.Equal(data.Length, bytesRead);
        Assert.Equal(data, buffer);
    }
}
