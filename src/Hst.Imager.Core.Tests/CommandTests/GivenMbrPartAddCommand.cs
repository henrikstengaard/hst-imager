using DiscUtils.Partitions;
using Hst.Core.Extensions;

namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Models;
using Xunit;

public class GivenMbrPartAddCommand : FsCommandTestBase
{
    [Fact]
    public async Task WhenAddMbrPartitionOfSize0ThenPartitionIsAddedWithRemainingDiskSize()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create mbr disk
        await CreateMbrDisk(testCommandHelper, imgPath, size);

        // arrange - mbr partition add command with type FAT32 and size 0
        var cancellationTokenSource = new CancellationTokenSource();
        var mbrPartAddCommand = new MbrPartAddCommand(new NullLogger<MbrPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, MbrPartType.Fat32, new Size(0, Unit.Bytes), null, null);

        // act - execute mbr partition add
        var result = await mbrPartAddCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - read disk info
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        Assert.True(mediaResult.IsSuccess);
        using var media = mediaResult.Value;
        var diskInfo = await testCommandHelper.ReadDiskInfo(media);
        Assert.NotNull(diskInfo?.MbrPartitionTablePart);

        // assert - added mbr partition size is equal to remaining disk size with an allowed margin of 50kb
        var expectedPartitionSize = diskInfo.MbrPartitionTablePart.Size - diskInfo.MbrPartitionTablePart
            .Parts.Where(x => x.PartType == PartType.PartitionTable).Sum(x => x.Size);
        var margin = 5000;
        var partInfo =
            diskInfo.MbrPartitionTablePart.Parts.FirstOrDefault(x =>
                x.PartType == PartType.Partition && x.Size > expectedPartitionSize - margin &&
                x.Size < expectedPartitionSize + margin);
        Assert.NotNull(partInfo);
        Assert.Equal(BiosPartitionTypes.Fat32.ToString(), partInfo.BiosType);
    }

    [Fact]
    public async Task WhenAddMbrPartitionOfSize50PercentDiskSizeThenPartitionIsAddedWith50PercentOfDiskSize()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create mbr disk
        await CreateMbrDisk(testCommandHelper, imgPath, size);

        // arrange - mbr partition add command with type FAT32 and size 50% of rdb disk size
        var cancellationTokenSource = new CancellationTokenSource();
        var mbrPartAddCommand = new MbrPartAddCommand(new NullLogger<MbrPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, MbrPartType.Fat32, new Size(50, Unit.Percent), null, null);

        // act - execute mbr partition add
        var result = await mbrPartAddCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - read disk info
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        Assert.True(mediaResult.IsSuccess);
        using var media = mediaResult.Value;
        var diskInfo = await testCommandHelper.ReadDiskInfo(media);
        Assert.NotNull(diskInfo?.MbrPartitionTablePart);

        // assert - added mbr partition size is equal to 50% of rdb disk size with an allowed margin of 5kb
        var expectedPartitionSize = diskInfo.Size * 0.5;
        var margin = 5000;
        var partInfo =
            diskInfo.MbrPartitionTablePart.Parts.FirstOrDefault(x =>
                x.PartType == PartType.Partition && x.Size > expectedPartitionSize - margin &&
                x.Size < expectedPartitionSize + margin);
        Assert.NotNull(partInfo);
        Assert.Equal(BiosPartitionTypes.Fat32.ToString(), partInfo.BiosType);
    }
}