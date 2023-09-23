using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Commands.GptCommands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenGptInitCommand : FsCommandTestBase
{
    [Fact]
    public async Task WhenInitGptThenGptPartitionTableExist()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();

        // arrange - create img media
        await testCommandHelper.AddTestMedia(imgPath, imgPath, new byte[10.MB()]);

        // arrange - gpt init command
        var cancellationTokenSource = new CancellationTokenSource();
        var gptInitCommand = new GptInitCommand(new NullLogger<GptInitCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath);

        // act - execute gpt init
        var result = await gptInitCommand.Execute(cancellationTokenSource.Token);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        
        await AssertGpt(testCommandHelper, imgPath);
    }

    [Fact]
    public async Task WhenInitGptWithExistingDataThenGptPartitionTableExist()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();

        // arrange - create img media
        testCommandHelper.AddTestMediaWithData(imgPath, 10.MB());

        // arrange - gpt init command
        var cancellationTokenSource = new CancellationTokenSource();
        var gptInitCommand = new GptInitCommand(new NullLogger<GptInitCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath);

        // act - execute gpt init
        var result = await gptInitCommand.Execute(cancellationTokenSource.Token);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        
        await AssertGpt(testCommandHelper, imgPath);
    }

    [Fact]
    public async Task WhenInitGptTwiceThenGptPartitionTableExist()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();

        // arrange - create img media
        await testCommandHelper.AddTestMedia(imgPath, imgPath, new byte[10.MB()]);

        // arrange - gpt init command
        var cancellationTokenSource = new CancellationTokenSource();
        var gptInitCommand = new GptInitCommand(new NullLogger<GptInitCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath);

        // act - execute gpt init command
        var result = await gptInitCommand.Execute(cancellationTokenSource.Token);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        // arrange - gpt init command 2nd
        gptInitCommand = new GptInitCommand(new NullLogger<GptInitCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath);

        // act - execute gpt init command 2nd
        result = await gptInitCommand.Execute(cancellationTokenSource.Token);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        await AssertGpt(testCommandHelper, imgPath);
    }

    [Fact]
    public async Task WhenInitGptWithExistingMbrThenGptPartitionTableExist()
    {
        // arrange - path, size and test command helper
        var diskSize = 10.MB();
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();

        // arrange - create img media
        await testCommandHelper.AddTestMedia(imgPath, imgPath, new byte[diskSize]);

        await CreateMbrDisk(testCommandHelper, imgPath, diskSize);
        
        // arrange - gpt init command
        var cancellationTokenSource = new CancellationTokenSource();
        var gptInitCommand = new GptInitCommand(new NullLogger<GptInitCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath);

        // act - execute gpt init command
        var result = await gptInitCommand.Execute(cancellationTokenSource.Token);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        await AssertGpt(testCommandHelper, imgPath);
    }

    private async Task AssertGpt(ICommandHelper commandHelper, string path)
    {
        // assert - read disk info
        var mediaResult = await commandHelper.GetReadableMedia(new List<IPhysicalDrive>(), path);
        Assert.True(mediaResult.IsSuccess);
        using var media = mediaResult.Value;
        var diskInfo = await commandHelper.ReadDiskInfo(media);

        // assert - disk contains gpt and not mbr or rdb
        Assert.NotNull(diskInfo?.GptPartitionTablePart);
        Assert.NotNull(diskInfo.MbrPartitionTablePart);
        Assert.Null(diskInfo.RdbPartitionTablePart);

        // assert - mbr contains gpt protective partition
        Assert.Equal(1, diskInfo.MbrPartitionTablePart.Parts.Count(
            x => x.PartType == PartType.Partition));
        
        // assert - gpt doesn't contain any partitions
        Assert.Equal(0, diskInfo.GptPartitionTablePart.Parts.Count(
            x => x.PartType == PartType.Partition));
    }
}