﻿namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Commands;
using Hst.Core.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Models;
using Xunit;

public class GivenRdbPartAddCommand : FsCommandTestBase
{
    [Fact]
    public async Task WhenAddRdbPartitionOfSize0ThenPartitionIsAddedWithRemainingDiskSize()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var fileSystemBlockSize = 512;
        var testCommandHelper = new TestCommandHelper();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath);

        // arrange - create rdb with pfs3 file system
        await CreateRdbWithPfs3(testCommandHelper, imgPath);

        // arrange - rdb partition add command with partition name DH0 and size 0
        var cancellationTokenSource = new CancellationTokenSource();
        var rdbPartAddCommand = new RdbPartAddCommand(new NullLogger<RdbPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, "DH0", "PFS3", new Size(0, Unit.Bytes), null, null, null, null,
            null,
            false, true, 0, fileSystemBlockSize);

        // act - execute rdb partition add
        var result = await rdbPartAddCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - read disk info
        var mediaResult = testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        Assert.True(mediaResult.IsSuccess);
        using var media = mediaResult.Value;
        var diskInfo = await testCommandHelper.ReadDiskInfo(media, media.Stream);
        Assert.NotNull(diskInfo?.RigidDiskBlock);

        // assert - added rdb partition size is equal to rdb disk size
        var partitionBlock = diskInfo.RigidDiskBlock.PartitionBlocks.FirstOrDefault(x => x.DriveName == "DH0");
        Assert.NotNull(partitionBlock);
        var cylinderSize = diskInfo.RigidDiskBlock.Sectors * diskInfo.RigidDiskBlock.Heads *
                           diskInfo.RigidDiskBlock.BlockSize;
        var cylinders = diskInfo.RigidDiskBlock.HiCylinder - diskInfo.RigidDiskBlock.LoCylinder + 1;
        var expectedPartitionSize = cylinders * cylinderSize;
        Assert.Equal(expectedPartitionSize, partitionBlock.PartitionSize);
    }
    
    [Fact]
    public async Task WhenAddRdbPartitionOfSize50PercentThenPartitionIsAddedWith50PercentOfDiskSize()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var fileSystemBlockSize = 512;
        var testCommandHelper = new TestCommandHelper();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath);

        // arrange - create rdb with pfs3 file system
        await CreateRdbWithPfs3(testCommandHelper, imgPath);

        // arrange - rdb partition add command with partition name DH0 and size 50% of rdb disk size
        var cancellationTokenSource = new CancellationTokenSource();
        var rdbPartAddCommand = new RdbPartAddCommand(new NullLogger<RdbPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, "DH0", "PFS3", new Size(50, Unit.Percent), null, null, null, null,
            null,
            false, true, 0, fileSystemBlockSize);

        // act - execute rdb partition add
        var result = await rdbPartAddCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - read disk info
        var mediaResult = testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        Assert.True(mediaResult.IsSuccess);
        using var media = mediaResult.Value;
        var diskInfo = await testCommandHelper.ReadDiskInfo(media, media.Stream);
        Assert.NotNull(diskInfo?.RigidDiskBlock);

        // assert - added rdb partition size is equal to 50% of rdb disk size
        var partitionBlock = diskInfo.RigidDiskBlock.PartitionBlocks.FirstOrDefault(x => x.DriveName == "DH0");
        Assert.NotNull(partitionBlock);
        var cylinderSize = diskInfo.RigidDiskBlock.Sectors * diskInfo.RigidDiskBlock.Heads *
                           diskInfo.RigidDiskBlock.BlockSize;
        var cylinders = Math.Ceiling((diskInfo.RigidDiskBlock.DiskSize * 0.5d) / cylinderSize);
        var expectedPartitionSize = cylinders * cylinderSize;
        Assert.Equal(expectedPartitionSize, partitionBlock.PartitionSize);
    }

    [Fact]
    public async Task WhenAddRdbPartitionAndMbrPartitionThenPartitionsAreAdded()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var fileSystemBlockSize = 512;
        var testCommandHelper = new TestCommandHelper();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath);

        // act - create rdb of size 5mb with pfs3 file system at sector 2 (rdb block lo) 
        await CreateRdbWithPfs3(testCommandHelper, imgPath, rdbSize: 5.MB(), rdbBlockLo: 2);

        // act - rdb partition add command with partition name DH0 and size 0
        var cancellationTokenSource = new CancellationTokenSource();
        var rdbPartAddCommand = new RdbPartAddCommand(new NullLogger<RdbPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, "DH0", "PFS3", new Size(0, Unit.Bytes), null, null, null, null,
            null,
            false, true, 0, fileSystemBlockSize);

        // act - execute rdb partition add
        var rdbPartAddResult = await rdbPartAddCommand.Execute(cancellationTokenSource.Token);
        Assert.True(rdbPartAddResult.IsSuccess);

        // arrange - create mbr at sector 0
        await CreateMbr(testCommandHelper, imgPath);
        
        // arrange - mbr partition add command with type FAT32 and size 0
        cancellationTokenSource = new CancellationTokenSource();
        var mbrPartAddCommand = new MbrPartAddCommand(new NullLogger<MbrPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, "FAT32", new Size(0, Unit.Bytes), null, null);

        // act - execute mbr partition add
        var mbrPartAddResult = await mbrPartAddCommand.Execute(cancellationTokenSource.Token);
        Assert.True(mbrPartAddResult.IsSuccess);

        
        // assert - read disk info
        var mediaResult = testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        Assert.True(mediaResult.IsSuccess);
        using var media = mediaResult.Value;
        var diskInfo = await testCommandHelper.ReadDiskInfo(media, media.Stream);
        Assert.NotNull(diskInfo);
        
        Assert.Equal(2, diskInfo.PartitionTables.Count());

        // assert - added rdb partition size is equal to 50% of rdb disk size
        // var partitionBlock = diskInfo.RigidDiskBlock.PartitionBlocks.FirstOrDefault(x => x.DriveName == "DH0");
        // Assert.NotNull(partitionBlock);
        // var cylinderSize = diskInfo.RigidDiskBlock.Sectors * diskInfo.RigidDiskBlock.Heads *
        //                    diskInfo.RigidDiskBlock.BlockSize;
        // var cylinders = Math.Ceiling((diskInfo.RigidDiskBlock.DiskSize * 0.5d) / cylinderSize);
        // var expectedPartitionSize = cylinders * cylinderSize;
        // Assert.Equal(expectedPartitionSize, partitionBlock.PartitionSize);
    }

}