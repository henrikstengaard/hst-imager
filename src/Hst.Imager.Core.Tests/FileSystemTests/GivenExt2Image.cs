using System.IO;
using System.Threading.Tasks;
using Hst.Imager.Core.FileSystems.Ext;
using Xunit;

namespace Hst.Imager.Core.Tests.FileSystemTests;

public class GivenExt2Image
{
    [Fact]
    public async Task WhenReadExtFileSystemInfoFromBytesThenExtVersionIsExt2()
    {
        // arrange
        var partitionBytes = ExtTestData.CreateExt2PartitionBytes();

        // act
        var info = await ExtFileSystemReader.Read(new MemoryStream(partitionBytes));
        
        // assert
        Assert.Equal(ExtFileSystemInfo.ExtVersion.Ext2, info.Version);
    }
    
    [Fact]
    public async Task WhenReadExtFileSystemInfoFromFileThenExtVersionIsExt2()
    {
        // arrange
        var partitionBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "Ext", "ext2.img"));

        // act
        var info = await ExtFileSystemReader.Read(new MemoryStream(partitionBytes));
        
        // assert
        Assert.Equal(ExtFileSystemInfo.ExtVersion.Ext2, info.Version);
    }
}