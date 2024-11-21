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
            new List<IPhysicalDrive>(), imgPath, "Fat32", new Size(0, Unit.Bytes), null, null);

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
            new List<IPhysicalDrive>(), imgPath, "Fat32", new Size(50, Unit.Percent), null, null);

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

    [Fact]
    public async Task When_AddMbrPartitionTypeAsNumber_Then_ThenPartitionIsAdded()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();
        const byte partitionType = 187;
        var partitionTypeAsNumber = $"0x{partitionType:x}";

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create mbr disk
        await CreateMbrDisk(testCommandHelper, imgPath, size);

        // arrange - mbr partition add command with type FAT32 and size 0
        var cancellationTokenSource = new CancellationTokenSource();
        var mbrPartAddCommand = new MbrPartAddCommand(new NullLogger<MbrPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, partitionTypeAsNumber, new Size(0, Unit.Bytes), null, null);

        // act - execute mbr partition add
        var result = await mbrPartAddCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - read disk info
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        Assert.True(mediaResult.IsSuccess);
        using var media = mediaResult.Value;
        var diskInfo = await testCommandHelper.ReadDiskInfo(media);
        Assert.NotNull(diskInfo?.MbrPartitionTablePart);

        // assert - mbr partition of type 187 is added
        var partitionPartInfo = diskInfo.MbrPartitionTablePart.Parts.FirstOrDefault(x => x.PartType == PartType.Partition);
        Assert.NotNull(partitionPartInfo);
        Assert.Equal(partitionType.ToString(), partitionPartInfo.BiosType);
    }

    [Fact]
    public async Task When_AddAnySizePartitionWithExistingSmallPartition_Then_LargestUnallocatedPartIsUsed()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();
        const string partitionType = "fat32";

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create mbr disk with partition starting at sector 2048
        await CreateMbrDisk(testCommandHelper, imgPath, size);
        await AddMbrPartition(testCommandHelper, imgPath, 2048, 5000);

        // arrange - read disk info and get largest unallocated part
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        Assert.True(mediaResult.IsSuccess);
        PartInfo largestUnallocatedPart;
        using (var media = mediaResult.Value)
        {
            var diskInfo = await testCommandHelper.ReadDiskInfo(media);
            Assert.NotNull(diskInfo?.MbrPartitionTablePart);
            largestUnallocatedPart = diskInfo.MbrPartitionTablePart.Parts
                .OrderByDescending(x => x.Size).FirstOrDefault(x => x.PartType == PartType.Unallocated);
            Assert.NotNull(largestUnallocatedPart);
        }

        // arrange - mbr partition add command with type fat32 and size 0
        var cancellationTokenSource = new CancellationTokenSource();
        var mbrPartAddCommand = new MbrPartAddCommand(new NullLogger<MbrPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, partitionType.ToString(), new Size(0, Unit.Bytes), null, null);

        // act - execute mbr partition add
        var result = await mbrPartAddCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - read disk info and assert mbr partition added is equal to largest unallocated part
        mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        Assert.True(mediaResult.IsSuccess);
        using (var media = mediaResult.Value)
        {
            var diskInfo = await testCommandHelper.ReadDiskInfo(media);
            Assert.NotNull(diskInfo?.MbrPartitionTablePart);
            var largestPartition = diskInfo.MbrPartitionTablePart.Parts.FirstOrDefault(x => x.PartitionNumber == 2);
            Assert.NotNull(largestPartition);
            var margin = 5000;
            Assert.True(largestPartition.Size > largestUnallocatedPart.Size - margin &&
                largestPartition.Size < largestUnallocatedPart.Size);
        }
    }

    [Fact]
    public async Task When_AddMbrPartitionWithStartAndEndSectors_Then_PartitionIsAdded()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();
        var startSector = 63;
        var endSector = size / 1024;

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create mbr disk
        await CreateMbrDisk(testCommandHelper, imgPath, size);

        // arrange - mbr partition add command with type FAT32 and sectors 50% of disk size
        var cancellationTokenSource = new CancellationTokenSource();
        var mbrPartAddCommand = new MbrPartAddCommand(new NullLogger<MbrPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, "Fat32", new Size(), startSector, endSector);

        // act - execute mbr partition add
        var result = await mbrPartAddCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - read disk info
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        Assert.True(mediaResult.IsSuccess);
        using var media = mediaResult.Value;
        var diskInfo = await testCommandHelper.ReadDiskInfo(media);
        Assert.NotNull(diskInfo?.MbrPartitionTablePart);

        // assert - added mbr partition size is equal to 50% of disk size with an allowed margin of 50kb
        var expectedPartitionSize = diskInfo.Size * 0.5;
        var margin = 50000;
        var partInfo =
            diskInfo.MbrPartitionTablePart.Parts.FirstOrDefault(x =>
                x.PartType == PartType.Partition && x.Size > expectedPartitionSize - margin &&
                x.Size < expectedPartitionSize + margin);
        Assert.NotNull(partInfo);
        Assert.Equal(BiosPartitionTypes.Fat32.ToString(), partInfo.BiosType);
    }
}