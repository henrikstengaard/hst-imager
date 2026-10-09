using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenRdbFsUpdateCommand : FsCommandTestBase
{
    [Fact]
    public async Task When_VersionAndRevisionIsSet_Then_FileSystemVersionIsUpdated()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var diskSize = 100.MB();
        var fileSystemPath = "FastFileSystem";

        // arrange - create rdb disk
        testCommandHelper.AddTestMedia(imgPath, diskSize);
        await TestHelper.CreateRdbDisk(testCommandHelper, imgPath, diskSize);

        // arrange - add file system without version string with version 1.0
        await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: new byte[36]);
        var addCommand = new RdbFsAddCommand(new NullLogger<RdbFsAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, fileSystemPath, "DOS3", "FastFileSystem", 1, 0);
        Assert.True((await addCommand.Execute(CancellationToken.None)).IsSuccess);

        // arrange - rdb file system update command to update version of added file system 2 to 47.2
        var command = new RdbFsUpdateCommand(new NullLogger<RdbFsUpdateCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, 2, string.Empty, string.Empty, string.Empty, 47, 2);

        // act - execute rdb file system update command
        var result = await command.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - file system has updated version
        var mediaResult = await testCommandHelper.GetReadableFileMedia(imgPath);
        using var media = mediaResult.Value;
        var rigidDiskBlock = await RigidDiskBlockReader.Read(media.Stream);
        var fileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks.ToList();
        Assert.Equal(2, fileSystemHeaderBlocks.Count);
        var fileSystemHeaderBlock = fileSystemHeaderBlocks[1];
        Assert.Equal("DOS\\3", fileSystemHeaderBlock.DosTypeFormatted);
        Assert.Equal(47, fileSystemHeaderBlock.Version);
        Assert.Equal(2, fileSystemHeaderBlock.Revision);
    }
}
