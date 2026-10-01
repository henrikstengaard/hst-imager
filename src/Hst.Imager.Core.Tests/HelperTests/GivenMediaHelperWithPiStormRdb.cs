using System;
using System.IO;
using System.Threading.Tasks;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Xunit;

namespace Hst.Imager.Core.Tests.HelperTests;

public class GivenMediaHelperWithPiStormRdb
{
    [Fact]
    public async Task When_GettingPiStormRdbMedia_Then_MediaPathHasPiStormRdbMediaPath()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var fileSystemPath = Path.Combine("mbr", "2", "rdb", "1");
        
        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();
        
        // arrange - create pi storm rdb disk
        await PiStormRdbTestHelper.CreatePiStormRdbDisk(testCommandHelper, mediaPath);
        
        // arrange - get readable file media
        var mediaResult = await testCommandHelper.GetReadableFileMedia(mediaPath);
        using var media = mediaResult.Value;
        
        // act - get pi storm rdb media
        var piStormRdbMedia = MediaHelper.GetPiStormRdbMedia(media, fileSystemPath,
            Path.DirectorySeparatorChar.ToString());

        // assert - pi storm rdb media path contains mbr and partition 2
        Assert.Equal(Path.Combine(mediaPath, "mbr", "2"), piStormRdbMedia.Media.Path);
    }

    [Fact]
    public async Task When_GettingGptPiStormRdbMedia_Then_PiStormRdbMediaIsReturned()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var fileSystemPath = Path.Combine("gpt", "2", "rdb", "1");

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create gpt pi storm rdb disk
        await PiStormRdbTestHelper.CreateGptPiStormRdbDisk(testCommandHelper, mediaPath);

        // arrange - get readable file media
        var mediaResult = await testCommandHelper.GetReadableFileMedia(mediaPath);
        using var media = mediaResult.Value;

        // act - get pi storm rdb media
        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(media, fileSystemPath,
            Path.DirectorySeparatorChar.ToString());

        // assert - pi storm rdb media is returned with path containing gpt and partition 2
        Assert.True(piStormRdbMediaResult.HasPiStormRdb);
        Assert.IsType<PiStormRdbMedia>(piStormRdbMediaResult.Media);
        Assert.Equal(Path.Combine(mediaPath, "gpt", "2"), piStormRdbMediaResult.Media.Path);
        Assert.Equal(Path.Combine("rdb", "1"), piStormRdbMediaResult.FileSystemPath);

        // assert - rigid disk block is read from pi storm rdb media
        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(piStormRdbMediaResult.Media);
        Assert.NotNull(rigidDiskBlock);
    }

    [Fact]
    public async Task When_GettingGptPiStormRdbMediaFromNonPiStormRdbPartition_Then_MediaIsReturned()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var fileSystemPath = Path.Combine("gpt", "1");

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create gpt pi storm rdb disk
        await PiStormRdbTestHelper.CreateGptPiStormRdbDisk(testCommandHelper, mediaPath);

        // arrange - get readable file media
        var mediaResult = await testCommandHelper.GetReadableFileMedia(mediaPath);
        using var media = mediaResult.Value;

        // act - get pi storm rdb media
        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(media, fileSystemPath,
            Path.DirectorySeparatorChar.ToString());

        // assert - media is returned as pi storm rdb media is not found
        Assert.False(piStormRdbMediaResult.HasPiStormRdb);
        Assert.Same(media, piStormRdbMediaResult.Media);
        Assert.Equal(fileSystemPath, piStormRdbMediaResult.FileSystemPath);
    }

    [Theory]
    [InlineData("mbr")]
    [InlineData("gpt")]
    public async Task When_GettingPiStormRdbMediaWithPartitionNumberOutOfRange_Then_MediaIsReturned(
        string partitionTable)
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var fileSystemPath = Path.Combine(partitionTable, "9");

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create pi storm rdb disk
        if (partitionTable == "gpt")
        {
            await PiStormRdbTestHelper.CreateGptPiStormRdbDisk(testCommandHelper, mediaPath);
        }
        else
        {
            await PiStormRdbTestHelper.CreatePiStormRdbDisk(testCommandHelper, mediaPath);
        }

        // arrange - get readable file media
        var mediaResult = await testCommandHelper.GetReadableFileMedia(mediaPath);
        using var media = mediaResult.Value;

        // act - get pi storm rdb media
        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(media, fileSystemPath,
            Path.DirectorySeparatorChar.ToString());

        // assert - media is returned as partition number doesn't exist
        Assert.False(piStormRdbMediaResult.HasPiStormRdb);
        Assert.Same(media, piStormRdbMediaResult.Media);
    }
}