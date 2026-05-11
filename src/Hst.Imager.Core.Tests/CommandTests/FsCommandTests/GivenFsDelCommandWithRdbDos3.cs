using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.UaeMetadatas;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsDelCommandWithRdbDos3
{
    [Fact]
    public async Task When_DeletingAFile_Then_FileIsDeleted()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var deletePath = Path.Combine(mediaPath, "rdb", "1", "dir1", "file1.txt");
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add media
        testCommandHelper.AddTestMedia(mediaPath, 0);

        // arrange - create rdb dos3 formatted disk with directories and files
        await TestHelper.CreateDos3FormattedDisk(testCommandHelper, mediaPath);
        await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - create fs del command
        var fsDelCommand = new FsDelCommand(new NullLogger<FsDelCommand>(), testCommandHelper, [],
            deletePath, uaeMetadata);
        
        // act - execute fs del command
        var result = await fsDelCommand.Execute(CancellationToken.None);
        
        // assert - result is success
        Assert.True(result.IsSuccess);
        
        // arrange - clear active medias to ensure changes to media is flushed
        testCommandHelper.ClearActiveMedias();
        
        // assert - file1.txt is deleted and dir3 exists in dir1
        var entries = (await RdbTestHelper
                .GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 0, ["dir1"]))
            .OrderBy(x => x.Name)
            .ToList();
        Assert.Single(entries);
        string[] expectedEntries =
        [
            "dir3"
        ];
        Assert.Equal(expectedEntries, entries.Select(x => x.Name).ToArray());
    }

    [Fact]
    public async Task When_DeletingASubDirectory_Then_DirectoryIsDeleted()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var deletePath = Path.Combine(mediaPath, "rdb", "1", "dir1", "dir3");
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add media
        testCommandHelper.AddTestMedia(mediaPath, 0);

        // arrange - create rdb dos3 formatted disk with directories and files
        await TestHelper.CreateDos3FormattedDisk(testCommandHelper, mediaPath);
        await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - create fs del command
        var fsDelCommand = new FsDelCommand(new NullLogger<FsDelCommand>(), testCommandHelper, [],
            deletePath, uaeMetadata);
        
        // act - execute fs del command
        var result = await fsDelCommand.Execute(CancellationToken.None);
        
        // assert - result is success
        Assert.True(result.IsSuccess);
        
        // arrange - clear active medias to ensure changes to media is flushed
        testCommandHelper.ClearActiveMedias();
        
        // assert - file1.txt is deleted and dir3 exists in dir1
        var entries = (await RdbTestHelper
                .GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 0, ["dir1"]))
            .OrderBy(x => x.Name)
            .ToList();
        Assert.Single(entries);
        string[] expectedEntries =
        [
            "file1.txt"
        ];
        Assert.Equal(expectedEntries, entries.Select(x => x.Name).ToArray());
    }

    [Fact]
    public async Task When_DeletingADirectory_Then_DirectoryIsDeleted()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var deletePath = Path.Combine(mediaPath, "rdb", "1", "dir1");
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add media
        testCommandHelper.AddTestMedia(mediaPath, 0);

        // arrange - create rdb dos3 formatted disk with directories and files
        await TestHelper.CreateDos3FormattedDisk(testCommandHelper, mediaPath);
        await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - create fs del command
        var fsDelCommand = new FsDelCommand(new NullLogger<FsDelCommand>(), testCommandHelper, [],
            deletePath, uaeMetadata);
        
        // act - execute fs del command
        var result = await fsDelCommand.Execute(CancellationToken.None);
        
        // assert - result is success
        Assert.True(result.IsSuccess);

        // arrange - clear active medias to ensure changes to media is flushed
        testCommandHelper.ClearActiveMedias();
        
        // assert - dir1 is deleted and dir2 exists in root
        var entries = (await RdbTestHelper
                .GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 0, []))
            .OrderBy(x => x.Name)
            .ToList();
        Assert.Single(entries);
        string[] expectedEntries =
        [
            "dir2"
        ];
        Assert.Equal(expectedEntries, entries.Select(x => x.Name).ToArray());
    }
}