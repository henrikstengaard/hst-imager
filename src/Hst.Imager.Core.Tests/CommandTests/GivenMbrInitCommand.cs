using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenMbrInitCommand : FsCommandTestBase
{
    [Fact]
    public async Task WhenInitMbrThenMbrPartitionTableExist()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, imgPath, new byte[10.MB()]);

        // arrange - mbr init command
        var cancellationTokenSource = new CancellationTokenSource();
        var mbrInitCommand = new MbrInitCommand(new NullLogger<MbrInitCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath);

        // act - execute mbr init
        var result = await mbrInitCommand.Execute(cancellationTokenSource.Token);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        
        await AssertMbr(testCommandHelper, imgPath);
    }

    [Fact]
    public async Task WhenInitMbrWithExistingDataThenMbrPartitionTableExist()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, 10.MB());

        // arrange - mbr init command
        var cancellationTokenSource = new CancellationTokenSource();
        var mbrInitCommand = new MbrInitCommand(new NullLogger<MbrInitCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath);

        // act - execute mbr init
        var result = await mbrInitCommand.Execute(cancellationTokenSource.Token);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        
        await AssertMbr(testCommandHelper, imgPath);
    }

    [Fact]
    public async Task WhenInitMbrPartitionTwiceThenMbrPartitionTableExist()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, imgPath, new byte[10.MB()]);

        // arrange - mbr init command
        var cancellationTokenSource = new CancellationTokenSource();
        var mbrInitCommand = new MbrInitCommand(new NullLogger<MbrInitCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath);

        // act - execute mbr init
        var result = await mbrInitCommand.Execute(cancellationTokenSource.Token);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        // arrange - mbr init command 2nd
        mbrInitCommand = new MbrInitCommand(new NullLogger<MbrInitCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath);

        // act - execute mbr init 2nd
        result = await mbrInitCommand.Execute(cancellationTokenSource.Token);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        await AssertMbr(testCommandHelper, imgPath);
    }

    [Fact]
    public async Task WhenInitMbrWithExistingMbrThenMbrPartitionTableExist()
    {
        // arrange - path, size and test command helper
        var diskSize = 10.MB();
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, imgPath, new byte[diskSize]);

        CreateMbrDisk(testCommandHelper, imgPath, diskSize);
        
        // arrange - mbr init command
        var cancellationTokenSource = new CancellationTokenSource();
        var mbrInitCommand = new MbrInitCommand(new NullLogger<MbrInitCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath);

        // act - execute mbr init
        var result = await mbrInitCommand.Execute(cancellationTokenSource.Token);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        await AssertMbr(testCommandHelper, imgPath);
    }

    [Fact]
    public async Task WhenInitMbrWithExistingGptThenMbrPartitionTableExist()
    {
        // arrange - path, size and test command helper
        var diskSize = 10.MB();
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, imgPath, new byte[diskSize]);

        CreateGptDisk(testCommandHelper, imgPath, diskSize);
        
        // arrange - mbr init command
        var cancellationTokenSource = new CancellationTokenSource();
        var mbrInitCommand = new MbrInitCommand(new NullLogger<MbrInitCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath);

        // act - execute mbr init
        var result = await mbrInitCommand.Execute(cancellationTokenSource.Token);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        await AssertMbr(testCommandHelper, imgPath);
    }

    private async Task AssertMbr(ICommandHelper commandHelper, string path)
    {
        // assert - read disk info
        var mediaResult = commandHelper.GetReadableMedia(new List<IPhysicalDrive>(), path);
        Assert.True(mediaResult.IsSuccess);
        using var media = mediaResult.Value;
        var diskInfo = await commandHelper.ReadDiskInfo(media);

        // assert - disk contains gpt and not mbr or rdb
        Assert.NotNull(diskInfo?.MbrPartitionTablePart);
        Assert.Null(diskInfo.GptPartitionTablePart);
        Assert.Null(diskInfo.RdbPartitionTablePart);

        // assert - mbr contains 2 parts
        Assert.Equal(2, diskInfo.MbrPartitionTablePart.Parts.Count());

        // assert - mbr contain 1 partition table
        Assert.Equal(1, diskInfo.MbrPartitionTablePart.Parts.Count(
            x => x.PartType == PartType.PartitionTable));

        // assert - mbr contain 1 unallocated part
        Assert.Equal(1, diskInfo.MbrPartitionTablePart.Parts.Count(
            x => x.PartType == PartType.Unallocated));

        // assert - mbr doesn't contain any partitions
        Assert.Equal(0, diskInfo.MbrPartitionTablePart.Parts.Count(
            x => x.PartType == PartType.Partition));
    }
}