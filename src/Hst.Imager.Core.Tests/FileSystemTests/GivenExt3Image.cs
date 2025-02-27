using System.IO;
using System.Threading.Tasks;
using Hst.Imager.Core.FileSystems.Ext;
using Xunit;

namespace Hst.Imager.Core.Tests.FileSystemTests;

public class GivenExt3Image
{
    [Fact]
    public async Task WhenReadExtFileSystemInfoFromBytesThenExtVersionIsExt3()
    {
        // arrange
        var partitionBytes = ExtTestData.CreateExt3PartitionBytes();

        // act
        var info = await ExtFileSystemReader.Read(new MemoryStream(partitionBytes));
        
        // assert
        Assert.Equal(ExtFileSystemInfo.ExtVersion.Ext3, info.Version);
    }

    [Fact]
    public async Task WhenReadExtFileSystemInfoFromFileThenExtVersionIsExt3()
    {
        // arrange
        var partitionBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "ext", "ext3.img"));

        // act
        var info = await ExtFileSystemReader.Read(new MemoryStream(partitionBytes));
        
        // assert
        Assert.Equal(ExtFileSystemInfo.ExtVersion.Ext3, info.Version);
    }
}