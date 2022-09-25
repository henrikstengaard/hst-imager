namespace Hst.Imager.Core.Tests;

using System.IO;
using System.Threading.Tasks;
using Xunit;

public class GivenDataSectorReaderAndReadNextLengthDifferentThanBufferSize : SectorTestBase
{
    [Fact]
    public async Task WhenReadNextLengthIsSmallerThanBufferSizeThenBufferSizeIsUsed()
    {
        // arrange - buffer size
        const int bufferSize = 512 * 1024;
        
        // arrange - zero filled sectors
        var zeroFilledSectorBytes = new byte[bufferSize + 1024];
        var stream = new MemoryStream(zeroFilledSectorBytes);

        // create data sector reader
        var reader = new DataSectorReader(stream, bufferSize: bufferSize);
        
        // act - read next of length buffer size
        var result = await reader.ReadNext(bufferSize);
        
        // assert - bytes read matches buffer size
        Assert.Equal(bufferSize, result.BytesRead);
        Assert.Equal(0, result.Start);
        Assert.Equal(bufferSize - 1, result.End);
        
        // act - read next of length buffer size
        result = await reader.ReadNext(bufferSize);
        
        // assert - bytes read matches remaining bytes
        Assert.Equal(1024, result.BytesRead);
        Assert.Equal(bufferSize, result.Start);
        Assert.Equal(bufferSize + 1024 - 1, result.End);
    }

    [Fact]
    public async Task WhenReadNextLengthIsLargerThanBufferSizeThenBufferSizeIsUsed()
    {
        // arrange - buffer size
        const int bufferSize = 512 * 1024;
        
        // arrange - zero filled sectors
        var zeroFilledSectorBytes = new byte[bufferSize + 1024];
        var stream = new MemoryStream(zeroFilledSectorBytes);

        // create data sector reader
        var reader = new DataSectorReader(stream, bufferSize: SectorSize);
        
        // act - read next of length buffer size
        var result = await reader.ReadNext(bufferSize);
        
        // assert - bytes read matches sector size
        Assert.Equal(SectorSize, result.BytesRead);
        Assert.Equal(0, result.Start);
        Assert.Equal(511, result.End);
    }
}