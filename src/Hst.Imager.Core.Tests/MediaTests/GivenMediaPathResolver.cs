using System;
using System.IO;
using Hst.Imager.Core.Medias;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.MediaTests;

public class GivenMediaPathResolver
{
    [Theory]
    [InlineData("+bs:disk.img", "disk.img", ModifierEnum.ByteSwap)]
    [InlineData("disk.img", "disk.img", ModifierEnum.None)]
    public void When_ResolvingModifiers_Then_PathAndModifiersAreResolved(string path, string expectedPath,
        ModifierEnum expectedModifiers)
    {
        // arrange
        var pathResolver = new MediaPathResolver(NullLogger.Instance);

        // act
        var result = pathResolver.ResolveModifiers(path);

        // assert
        Assert.Equal(expectedModifiers, result.Modifiers);
        Assert.Equal(expectedPath, result.Path);
    }

    [Fact]
    public void When_ResolvingMediaWithFileSystemPathInImageFile_Then_MediaPathAndFileSystemPathAreResolved()
    {
        // arrange
        var imgPath = Path.GetFullPath($"{Guid.NewGuid()}.img");
        File.WriteAllBytes(imgPath, new byte[512]);
        var path = Path.Combine(imgPath, "rdb", "dh0");
        var pathResolver = new MediaPathResolver(NullLogger.Instance);

        try
        {
            // act
            var mediaResult = pathResolver.ResolveMedia(path);

            // assert
            Assert.True(mediaResult.IsSuccess);
            Assert.True(mediaResult.Value.Exists);
            Assert.Equal(imgPath, mediaResult.Value.MediaPath);
            Assert.Equal(Path.Combine("rdb", "dh0"), mediaResult.Value.FileSystemPath);
        }
        finally
        {
            File.Delete(imgPath);
        }
    }

    [Fact]
    public void When_GettingPhysicalDrivePathForImageFile_Then_NullIsReturned()
    {
        // arrange
        var pathResolver = new MediaPathResolver(NullLogger.Instance);

        // act
        var physicalDrivePath = pathResolver.GetPhysicalDrivePath(Path.GetFullPath("disk.img"));

        // assert
        Assert.Null(physicalDrivePath);
    }
}
