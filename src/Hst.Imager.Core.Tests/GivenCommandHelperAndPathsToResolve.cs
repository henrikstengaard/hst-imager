using System;
using System.IO;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenCommandHelperAndPathsToResolve
{
    [Fact]
    public void WhenResolveMediaWithByteSwapModifierThenModifierIsDetected()
    {
        var path = "\\disk2\\+bs";
        var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false);

        var mediaResult = commandHelper.ResolveMedia(path);
        Assert.True(mediaResult.IsSuccess);

        var resolvedMedia = mediaResult.Value;
        Assert.Equal("\\\\.\\PhysicalDrive2", resolvedMedia.MediaPath);
        Assert.Equal(string.Empty, resolvedMedia.FileSystemPath);
        Assert.True(resolvedMedia.ByteSwap);
    }

    [Fact]
    public async Task WhenGetReadableFileMediaWithByteSwapModifierThenDataReadIsByteSwapped()
    {
        // arrange - create data to read
        var data = new byte[512];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 2 == 0 ? 1 : 2);
        }

        // arrange - create expected byte swapped data
        var expectedData = new byte[512];
        for (var i = 0; i < expectedData.Length; i++)
        {
            expectedData[i] = (byte)(i % 2 == 0 ? 2 : 1);
        }

        // arrange - write image to read
        var path = $"{Guid.NewGuid()}.img";
        File.WriteAllBytes(path, data);

        try
        {
            var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false);

            // act - get file media byte swapped
            var mediaResult = await commandHelper.GetReadableFileMedia(Path.Combine(path, "+bs"));
            using var media = mediaResult.Value;
            var stream = media.Stream;

            // act - read 512 bytes from image
            var actualData = new byte[512];
            var bytesRead = stream.Read(actualData, 0, actualData.Length);
            
            // assert - 512 bytes are read from image and they expected match byte swapped data
            Assert.Equal(512, bytesRead);
            Assert.Equal(expectedData.Length, actualData.Length);
            Assert.Equal(expectedData, actualData);
        }
        finally
        {
            File.Delete(path);
        }
    }
}