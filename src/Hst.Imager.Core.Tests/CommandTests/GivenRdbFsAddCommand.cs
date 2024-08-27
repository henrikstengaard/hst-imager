using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenRdbFsAddCommand : FsCommandTestBase
{
    [Fact]
    public async Task When_FileSystemDoesHaveVersionStringAndVersionAndRevisionIsSet_Then_FileSystemIsAdded()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var diskSize = 100.MB();
        var fileSystemPath = "FastFileSystem";

        // arrange - create rdb disk
        testCommandHelper.AddTestMedia(imgPath, diskSize);
        await CreateRdbDisk(testCommandHelper, imgPath, diskSize);

        // arrange - file system without version string
        await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: new byte[36]);

        // arrange - rdb file system add command
        var cancellationTokenSource = new CancellationTokenSource();
        var command = new RdbFsAddCommand(new NullLogger<RdbFsAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, "FastFileSystem", "DOS3", 
            "FastFileSystem", 1, 0);

        // act - execute rdb file system add command
        var result = await command.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);
    }
    
    [Fact]
    public async Task When_FileSystemDoesHaveVersionStringAndVersionIsNotSet_Then_ErrorIsReturned()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var diskSize = 100.MB();
        var fileSystemPath = "FastFileSystem";

        // arrange - create rdb disk
        testCommandHelper.AddTestMedia(imgPath, diskSize);
        await CreateRdbDisk(testCommandHelper, imgPath, diskSize);

        // arrange - file system without version string
        await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: new byte[36]);

        // arrange - rdb file system add command
        var cancellationTokenSource = new CancellationTokenSource();
        var command = new RdbFsAddCommand(new NullLogger<RdbFsAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, "FastFileSystem", "DOS3", 
            "FastFileSystem", null, null);

        // act - execute rdb file system add command
        var result = await command.Execute(cancellationTokenSource.Token);
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFaulted);
        Assert.IsType<VersionNotFoundError>(result.Error);
    }

    [Fact]
    public async Task When_FileSystemDoesHaveVersionStringAndRevisionIsNotSet_Then_ErrorIsReturned()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var diskSize = 100.MB();
        var fileSystemPath = "FastFileSystem";

        // arrange - create rdb disk
        testCommandHelper.AddTestMedia(imgPath, diskSize);
        await CreateRdbDisk(testCommandHelper, imgPath, diskSize);

        // arrange - file system without version string
        await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: new byte[36]);

        // arrange - rdb file system add command
        var cancellationTokenSource = new CancellationTokenSource();
        var command = new RdbFsAddCommand(new NullLogger<RdbFsAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, "FastFileSystem", "DOS3", 
            "FastFileSystem", 1, null);

        // act - execute rdb file system add command
        var result = await command.Execute(cancellationTokenSource.Token);
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFaulted);
        Assert.IsType<VersionNotFoundError>(result.Error);
    }
    
    private static async Task CreateRdbDisk(TestCommandHelper testCommandHelper, string path, long diskSize)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, size: diskSize, create: true);
        using var media = mediaResult.Value;
        var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;
        
        var rigidDiskBlock = RigidDiskBlock.Create(diskSize.ToSectorSize());
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
    }
}