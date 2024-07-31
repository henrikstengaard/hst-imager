using System.IO;
using System.Linq;
using DiscUtils;
using DiscUtils.Fat;
using Hst.Core.Extensions;
using Xunit;

namespace Hst.Imager.Core.Tests.FileSystemTests;

public class GivenFloppyImage
{
    [Fact]
    public void When_ReadFilesFromRootDirectory_Then_FilesAreReturned()
    {
        // arrange
        var floppyImageBytes = CreateFloppyImageBytes();

        // act
        string[] files;
        using (var imageStream = new MemoryStream(floppyImageBytes))
        {
            using (FatFileSystem fatFileSystem = new FatFileSystem(imageStream))
            {
                // get files from root directory
                files = fatFileSystem.GetFiles(string.Empty).ToArray();
            }
        }
        
        // assert
        Assert.Single(files);
        Assert.Equal("FILE.TXT", files[0]);
    }
    
    private static byte[] CreateFloppyImageBytes()
    {
        using var imageStream = new MemoryStream();
        
        using (var fatFileSystem = FatFileSystem.FormatFloppy(imageStream, FloppyDiskType.HighDensity, "TEST       "))
        {
            using (var fileStream = fatFileSystem.OpenFile("FILE.TXT", FileMode.Create))
            {
                fileStream.WriteBytes(new byte[100]);
            }
        }

        return imageStream.ToArray();
    }
}