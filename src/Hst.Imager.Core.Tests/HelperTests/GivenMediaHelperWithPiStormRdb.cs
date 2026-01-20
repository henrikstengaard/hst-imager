using System;
using System.IO;
using System.Threading.Tasks;
using Hst.Imager.Core.Helpers;
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
}