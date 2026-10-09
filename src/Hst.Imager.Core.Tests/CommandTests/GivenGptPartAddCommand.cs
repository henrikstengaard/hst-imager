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
        var fileSystem = "fat32";

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create gpt
        await CreateGptDisk(testCommandHelper, imgPath, size);

        // arrange - gpt partition add command with type FAT32 and size 0
        var cancellationTokenSource = new CancellationTokenSource();
        var gptPartAddCommand = new GptPartAddCommand(new NullLogger<GptPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, fileSystem, "UNITTEST", new Size(0, Unit.Bytes), 
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

        // assert - added gpt partition size is equal to remaining disk size with an allowed margin of 100kb
        var expectedPartitionSize = diskInfo.Size - diskInfo.GptPartitionTablePart
            .Parts.Where(x => x.PartType == PartType.PartitionTable).Sum(x => x.Size);
        var margin = 100000;
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
        var fileSystem = "fat32";

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create gpt
        await CreateGptDisk(testCommandHelper, imgPath, size);

        // arrange - gpt partition add command with type FAT32 and size 50% of disk size
        var cancellationTokenSource = new CancellationTokenSource();
        var gptPartAddCommand = new GptPartAddCommand(new NullLogger<GptPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, fileSystem, "UNITTEST", new Size(50, Unit.Percent), 
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

    [Fact]
    public async Task When_AddGptPartitionWithStartAndEndSectors_Then_PartitionIsAdded()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();
        var startSector = 63;
        var endSector = size / 1024;
        var fileSystem = "fat32";

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create gpt
        await CreateGptDisk(testCommandHelper, imgPath, size);

        // arrange - gpt partition add command with type FAT32 and sectors 50% of disk size
        var cancellationTokenSource = new CancellationTokenSource();
        var gptPartAddCommand = new GptPartAddCommand(new NullLogger<GptPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, fileSystem, "UNITTEST", new Size(), startSector, endSector);

        // act - execute gpt partition add
        var result = await gptPartAddCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - read disk info
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        Assert.True(mediaResult.IsSuccess);
        using var media = mediaResult.Value;
        var diskInfo = await testCommandHelper.ReadDiskInfo(media);
        Assert.NotNull(diskInfo?.GptPartitionTablePart);

        // assert - added gpt partition size is equal to 50% of disk size with an allowed margin of 50kb
        var expectedPartitionSize = diskInfo.Size * 0.5;
        var margin = 50000;
        var partInfo =
            diskInfo.GptPartitionTablePart.Parts.FirstOrDefault(x =>
                x.PartType == PartType.Partition && x.Size > expectedPartitionSize - margin &&
                x.Size < expectedPartitionSize + margin);
        Assert.NotNull(partInfo);
        Assert.Equal(GuidPartitionTypes.WindowsBasicData.ToString(), partInfo.GuidType);
    }

    [Fact]
    public async Task When_AddGptPartitionWithStartSectorAfterExistingPartition_Then_PartitionIsAddedWithRemainingDiskSize()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create gpt
        await CreateGptDisk(testCommandHelper, imgPath, size);

        // arrange - add gpt partition 1 from sector 2048 to 22527 (10mb)
        var gptPartAddCommand = new GptPartAddCommand(new NullLogger<GptPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, "fat32", "UNITTEST1", new Size(), 2048, 22527);
        var result = await gptPartAddCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // act - add gpt partition 2 from sector 22528 of remaining disk size,
        // which is after unallocated space before partition 1
        gptPartAddCommand = new GptPartAddCommand(new NullLogger<GptPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, "fat32", "UNITTEST2", new Size(), 22528, null);
        result = await gptPartAddCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - read disk info
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        Assert.True(mediaResult.IsSuccess);
        using var media = mediaResult.Value;
        var diskInfo = await testCommandHelper.ReadDiskInfo(media);
        Assert.NotNull(diskInfo?.GptPartitionTablePart);

        // assert - gpt partition 2 starts at sector 22528 and uses remaining disk size
        var partInfo = diskInfo.GptPartitionTablePart.Parts.FirstOrDefault(x =>
            x.PartType == PartType.Partition && x.PartitionNumber == 2);
        Assert.NotNull(partInfo);
        Assert.Equal(22528, partInfo.StartSector);
        Assert.True(partInfo.Size > 85.MB());
    }
}