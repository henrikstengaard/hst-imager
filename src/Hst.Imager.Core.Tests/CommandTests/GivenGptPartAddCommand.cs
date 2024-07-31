using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Partitions;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Commands.GptCommands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenGptPartAddCommand : FsCommandTestBase
{
    [Fact]
    public async Task WhenAddGptPartitionOfSize0ThenPartitionIsAddedWithRemainingDiskSize()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create gpt
        await CreateGptDisk(testCommandHelper, imgPath, size);

        // arrange - gpt partition add command with type FAT32 and size 0
        var cancellationTokenSource = new CancellationTokenSource();
        var gptPartAddCommand = new GptPartAddCommand(new NullLogger<GptPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, GptPartType.Fat32, "FAT32GPT", new Size(0, Unit.Bytes), 
            null, null);

        // act - execute gpt partition add
        var result = await gptPartAddCommand.Execute(cancellationTokenSource.Token);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        // assert - read disk info
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        Assert.True(mediaResult.IsSuccess);
        using var media = mediaResult.Value;
        var diskInfo = await testCommandHelper.ReadDiskInfo(media);
        Assert.NotNull(diskInfo?.GptPartitionTablePart);

        // assert - added gpt partition size is equal to remaining disk size with an allowed margin of 5kb
        var expectedPartitionSize = diskInfo.Size - diskInfo.GptPartitionTablePart
            .Parts.Where(x => x.PartType == PartType.PartitionTable).Sum(x => x.Size);
        var margin = 5000;
        var partInfo =
            diskInfo.GptPartitionTablePart.Parts.FirstOrDefault(x =>
                x.PartType == PartType.Partition && x.Size > expectedPartitionSize - margin &&
                x.Size < expectedPartitionSize + margin);
        Assert.NotNull(partInfo);
        Assert.Equal(GuidPartitionTypes.WindowsBasicData.ToString(), partInfo.GuidType);
    }

    [Fact]
    public async Task WhenAddGptPartitionOfSize50PercentDiskSizeThenPartitionIsAddedWith50PercentOfDiskSize()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create gpt
        await CreateGptDisk(testCommandHelper, imgPath, size);

        // arrange - gpt partition add command with type FAT32 and size 50% of disk size
        var cancellationTokenSource = new CancellationTokenSource();
        var gptPartAddCommand = new GptPartAddCommand(new NullLogger<GptPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, GptPartType.Fat32, "FAT32GPT", new Size(50, Unit.Percent), 
            null, null);

        // act - execute gpt partition add
        var result = await gptPartAddCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - read disk info
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        Assert.True(mediaResult.IsSuccess);
        using var media = mediaResult.Value;
        var diskInfo = await testCommandHelper.ReadDiskInfo(media);
        Assert.NotNull(diskInfo?.GptPartitionTablePart);
        
        // assert - added gpt partition size is equal to 50% of disk size with an allowed margin of 5kb
        var expectedPartitionSize = diskInfo.Size * 0.5;
        var margin = 5000;
        var partInfo =
            diskInfo.GptPartitionTablePart.Parts.FirstOrDefault(x =>
                x.PartType == PartType.Partition && x.Size > expectedPartitionSize - margin &&
                x.Size < expectedPartitionSize + margin);
        Assert.NotNull(partInfo);
        Assert.Equal(GuidPartitionTypes.WindowsBasicData.ToString(), partInfo.GuidType);
    }
}