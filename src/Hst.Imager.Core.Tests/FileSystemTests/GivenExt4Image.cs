using System.IO;
using System.Threading.Tasks;
using Hst.Imager.Core.FileSystems.Ext;
using Xunit;

namespace Hst.Imager.Core.Tests.FileSystemTests;

public class GivenExt4Image
{
    [Fact]
    public async Task WhenReadExtFileSystemInfoFromBytesThenExtVersionIsExt4()
    {
        // arrange
        var partitionBytes = ExtTestData.CreateExt4PartitionBytes();

        // act
        var info = await ExtFileSystemReader.Read(new MemoryStream(partitionBytes));
        
        // assert
        Assert.Equal(ExtFileSystemInfo.ExtVersion.Ext4, info.Version);
    }
    
    [Fact]
    public async Task WhenReadExtFileSystemInfoFromFileThenExtVersionIsExt4()
    {
        // arrange
        var partitionBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "Ext", "ext4.img"));

        // act
        var info = await ExtFileSystemReader.Read(new MemoryStream(partitionBytes));
        
        // assert
        Assert.Equal(ExtFileSystemInfo.ExtVersion.Ext4, info.Version);
    }
}