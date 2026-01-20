using System;
using System.IO;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Helpers;
using Xunit;

namespace Hst.Imager.Core.Tests.HelperTests;

public class GivenMediaHelperWithMbr
{
    [Theory]
    [InlineData("mbr")]
    [InlineData("mbR")]
    [InlineData("MBR")]
    public async Task When_ResolvingStartOffsetAndSizeForPartition_Then_PartitionStartOffsetAndSizeAreReturned(
        string partitionTypePart)
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var partitionPath = Path.Combine(partitionTypePart, "1");
        
        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        testCommandHelper.AddTestMedia(mediaPath, 0);
        
        // arrange - create mbr disk with fat formatted partition
        var diskSize = 100.MB().ToSectorSize();
        const int partitionStartSector = 63;
        var partitionEndSector = (diskSize / 512) - partitionStartSector - 100;
        await MbrTestHelper.CreateMbrDisk(testCommandHelper, mediaPath, diskSize);
        await MbrTestHelper.AddMbrPartition(testCommandHelper, mediaPath, partitionStartSector, partitionEndSector);
        await MbrTestHelper.FatFormatMbrPartition(testCommandHelper, mediaPath, 0);

        // arrange - get readable media
        var readableMediaResult = await testCommandHelper.GetReadableFileMedia(mediaPath);
        Assert.True(readableMediaResult.IsSuccess);
        using var media = readableMediaResult.Value;
        
        // act - resolve start offset and size
        var startOffsetAndSizeResult = await MediaHelper.GetStartOffsetAndSize(testCommandHelper, media, partitionPath);
        
        // assert - start offset and size resolved
        Assert.True(startOffsetAndSizeResult.IsSuccess);
        var (startOffset, size) = startOffsetAndSizeResult.Value;
        var expectedStartOffset = partitionStartSector * 512L;
        var expectedSize = (partitionEndSector - partitionStartSector + 1) * 512L;
        Assert.Equal(expectedStartOffset, startOffset);
        Assert.Equal(expectedSize, size);
    }
}