using System;
using System.IO;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Helpers;
using Xunit;

namespace Hst.Imager.Core.Tests.HelperTests;

public class GivenMediaHelperWithRdb
{
    [Theory]
    [InlineData("rdb")]
    [InlineData("rdB")]
    [InlineData("RDB")]
    public async Task When_ResolvingStartOffsetAndSizeForPartition_Then_PartitionStartOffsetAndSizeAreReturned(
        string partitionTypePart)
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var partitionPath = Path.Combine(partitionTypePart, "1");
        
        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        testCommandHelper.AddTestMedia(mediaPath, 0);
        
        // arrange - create gpt disk with fat formatted partition
        var diskSize = 100.MB().ToSectorSize();
        var cylinderSize = 16 * 63 * 512;
        var partitionSize = 90.MB() + cylinderSize - 90.MB() % cylinderSize;
        await TestHelper.CreateRdbDisk(testCommandHelper, mediaPath, diskSize);
        await RdbTestHelper.AddPfs3RdbPartition(testCommandHelper, mediaPath, "DH0", partitionSize);
        await RdbTestHelper.Pfs3FormatRdbPartition(testCommandHelper, mediaPath, 0);

        // arrange - get readable media
        var readableMediaResult = await testCommandHelper.GetReadableFileMedia(mediaPath);
        Assert.True(readableMediaResult.IsSuccess);
        using var media = readableMediaResult.Value;
        
        // act - resolve start offset and size
        var startOffsetAndSizeResult = await MediaHelper.GetStartOffsetAndSize(testCommandHelper, media, partitionPath);
        
        // assert - start offset and size resolved
        Assert.True(startOffsetAndSizeResult.IsSuccess);
        var (startOffset, size) = startOffsetAndSizeResult.Value;
        var expectedStartOffset = 2 * cylinderSize;
        Assert.Equal(expectedStartOffset, startOffset);
        Assert.Equal(partitionSize, size);
    }
}