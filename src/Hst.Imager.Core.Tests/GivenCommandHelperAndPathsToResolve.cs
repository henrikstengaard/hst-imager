using System;
using System.IO;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenCommandHelperAndPathsToResolve
{
    [Theory]
    [InlineData("+bs:disk.img", "disk.img", ModifierEnum.ByteSwap)]
    [InlineData("+bs:", "", ModifierEnum.ByteSwap)]
    [InlineData("disk.img", "disk.img", ModifierEnum.None)]
    public void When_ResolveModifiersFromPath_Then_PathAndModifiersAreResolved(string path, string expectedPath, ModifierEnum expectedModifiers)
    {
        // arrange
        var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false);

        // act
        var result = commandHelper.ResolveModifiers(path);
        
        // assert
        Assert.Equal(expectedModifiers == ModifierEnum.ByteSwap, result.HasModifiers);
        Assert.Equal(expectedModifiers, result.Modifiers);
        Assert.Equal(expectedPath, result.Path);    
    }

    [Fact]
    public void When_ResolveMediaFromPhysical_Then_PathAndModifiersAreResolved()
    {
        var path = "+bs:\\disk2";
        var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false);

        var mediaResult = commandHelper.ResolveMedia(path);
        Assert.True(mediaResult.IsSuccess);

        var resolvedMedia = mediaResult.Value;
        Assert.Equal("\\\\.\\PhysicalDrive2", resolvedMedia.MediaPath);
        Assert.Equal(string.Empty, resolvedMedia.FileSystemPath);
        Assert.True(resolvedMedia.ByteSwap);
    }

    [Fact]
    public async Task When_ResolveMediaFromFile_Then_PathAndModifiersAreResolved()
    {
        var imgPath = $"{Guid.NewGuid()}.img";

        try
        {
            await File.WriteAllBytesAsync(imgPath, []);

            var path = $"+bs:{imgPath}";
            var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false);

            var mediaResult = commandHelper.ResolveMedia(path);
            Assert.True(mediaResult.IsSuccess);
        }
        finally
        {
            TestHelper.DeletePaths(imgPath);
        }
    }

    [Theory]
    [InlineData("", "rdb\\dh0", ModifierEnum.None, "rdb\\dh0", false)]
    [InlineData("", "rdb\\dh0\\+ file.txt", ModifierEnum.None, "rdb\\dh0\\+ file.txt", false)]
    [InlineData("+bs", "rdb\\dh0", ModifierEnum.ByteSwap, "rdb\\dh0", true)]
    [InlineData("+bs", "rdb\\dh0\\+ file.txt", ModifierEnum.ByteSwap, "rdb\\dh0\\+ file.txt", true)]
    public async Task When_ResolveMediaWithImg_Then_PathsAndModifiersMatch(string modifiers, string path,
        ModifierEnum expectedModifiers, string expectedFileSystemPath, bool expectedByteSwap)
    {
        if (!OperatingSystem.IsWindows())
        {
            path = path.Replace("\\", "/");
            expectedFileSystemPath = expectedFileSystemPath.Replace("\\", "/");
        }

        var imgPath = Path.GetFullPath($"{Guid.NewGuid()}.img");

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        try
        {
            // arrange - empty image
            await File.WriteAllTextAsync(imgPath, string.Empty);

            // act - resolve media
            var result = testCommandHelper.ResolveMedia(string.Concat(
                string.IsNullOrEmpty(modifiers) ? string.Empty : $"{modifiers}:",
                Path.Combine(imgPath, path)));

            // assert
            Assert.True(result.IsSuccess);
            Assert.Equal(imgPath, result.Value.MediaPath);
            Assert.Equal(expectedFileSystemPath, result.Value.FileSystemPath);
            Assert.Equal(expectedModifiers, result.Value.Modifiers);
            Assert.Equal(expectedByteSwap, result.Value.ByteSwap);
        }
        finally
        {
            TestHelper.DeletePaths(imgPath);
        }
    }

    [Theory]
    [InlineData("", "rdb\\dh0", "rdb\\dh0", false)]
    [InlineData("", "rdb\\dh0\\+ file.txt", "rdb\\dh0\\+ file.txt", false)]
    [InlineData("+bs", "rdb\\dh0", "rdb\\dh0", true)]
    [InlineData("+bs", "rdb\\dh0\\+ file.txt", "rdb\\dh0\\+ file.txt", true)]
    public void When_ResolveMediaWithPhysicalDrive_Then_PathsAndModifersMatch(string modifiers, string path,
        string expectedFileSystemPath, bool expectedByteSwap)
    {
        var physicalDrivePath = "\\\\.\\PhysicalDrive4";

        if (!OperatingSystem.IsWindows())
        {
            path = path.Replace("\\", "/");
            expectedFileSystemPath = expectedFileSystemPath.Replace("\\", "/");
            physicalDrivePath = physicalDrivePath.Replace("\\", "/");
        }

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        // act - resolve media
        // var result = testCommandHelper.ResolveMedia(Path.Combine(physicalDrivePath, path));

        // act - resolve media
        var result = testCommandHelper.ResolveMedia(string.Concat(
            string.IsNullOrEmpty(modifiers) ? string.Empty : $"{modifiers}:",
            Path.Combine(physicalDrivePath, path)));

        
        // assert
        Assert.True(result.IsSuccess);
        Assert.Equal(physicalDrivePath, result.Value.MediaPath);
        Assert.Equal(expectedFileSystemPath, result.Value.FileSystemPath);
        Assert.Equal(expectedByteSwap, result.Value.ByteSwap);
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
        var imgPath = $"{Guid.NewGuid()}.img";
        await File.WriteAllBytesAsync(imgPath, data);
        var imgPathWithModifier = $"+bs:{imgPath}";

        try
        {
            var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false);

            // act - get file media byte swapped
            var mediaResult = await commandHelper.GetReadableFileMedia(imgPathWithModifier);
            using var media = mediaResult.Value;
            var stream = media.Stream;

            // act - read 512 bytes from image
            var actualData = new byte[512];
            var bytesRead = await stream.ReadAsync(actualData, 0, actualData.Length);
            
            // assert - 512 bytes are read from image and they expected match byte swapped data
            Assert.Equal(512, bytesRead);
            Assert.Equal(expectedData.Length, actualData.Length);
            Assert.Equal(expectedData, actualData);
        }
        finally
        {
            File.Delete(imgPath);
        }
    }

    [Theory]
    [InlineData("img")]
    [InlineData("vhd")]
    public async Task When_WriteByteSwappedMedia_Then_DataIsWrittenByteSwapped(string imgType)
    {
        var imgPath = $"{Guid.NewGuid()}.{imgType}";
        var imgPathByteSwapped = $"+bs:{imgPath}";

        // arrange - create data to write
        const int diskSize = 1024;
        var data = new byte[diskSize];
        for (var i = 0; i < 512; i++)
        {
            data[i] = (byte)(i % 2 == 0 ? 1 : 2);
        }
        
        try
        {
            var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false);
            
            // act - create media with disk size and write data byte swapped
            var mediaResult = await commandHelper.GetWritableFileMedia(imgPathByteSwapped, create: true, size: diskSize);
            using (var media = mediaResult.Value)
            {
                var stream = MediaHelper.GetStreamFromMedia(media);
                await stream.WriteBytes(data);
            }

            // arrange - clear active media to read media
            commandHelper.ClearActiveMedias();

            // arrange - read data from media
            byte[] actualData;
            mediaResult = await commandHelper.GetReadableFileMedia(imgPath);
            using (var media = mediaResult.Value)
            {
                var stream = MediaHelper.GetStreamFromMedia(media);
                actualData = await stream.ReadBytes(diskSize);
            }

            // assert - data read from media matches data byte swapped
            var expectedData = new byte[diskSize];
            for(var i = 0; i < data.Length; i += 2)
            {
                expectedData[i] = data[i + 1];
                expectedData[i + 1] = data[i];
            }
            Assert.Equal(expectedData, actualData);
        }
        finally
        {
            File.Delete(imgPath);
        }
    }

    [Theory]
    [InlineData("img")]
    [InlineData("vhd")]
    public async Task When_ReadByteSwappedMedia_Then_DataIsReadByteSwapped(string imgType)
    {
        var imgPath = $"{Guid.NewGuid()}.{imgType}";
        var imgPathByteSwapped = $"+bs:{imgPath}";

        // arrange - create data to write
        const int diskSize = 1024;
        var data = new byte[diskSize];
        for (var i = 0; i < 512; i++)
        {
            data[i] = (byte)(i % 2 == 0 ? 1 : 2);
        }
        
        try
        {
            using var commandHelper = new CommandHelper(new NullLogger<ICommandHelper>(), false);
            
            // act - create media with disk size and write data
            var mediaResult = await commandHelper.GetWritableFileMedia(imgPath, create: true, size: diskSize);
            using (var media = mediaResult.Value)
            {
                var stream = MediaHelper.GetStreamFromMedia(media);
                await stream.WriteBytes(data);
            }
            
            // arrange - clear active media to read media byte swapped
            commandHelper.ClearActiveMedias();

            // arrange - read data byte swapped from media
            byte[] actualData;
            mediaResult = await commandHelper.GetReadableFileMedia(imgPathByteSwapped);
            using (var media = mediaResult.Value)
            {
                var stream = MediaHelper.GetStreamFromMedia(media);
                actualData = await stream.ReadBytes(diskSize);
            }

            // assert - data read byte swapped from media matches data
            var expectedData = new byte[diskSize];
            for(var i = 0; i < data.Length; i += 2)
            {
                expectedData[i] = data[i + 1];
                expectedData[i + 1] = data[i];
            }
            Assert.Equal(expectedData, actualData);
        }
        finally
        {
            File.Delete(imgPath);
        }
    }
}