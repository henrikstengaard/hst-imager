using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Imager.Core.Commands.FsCommands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsMkDirCommandWithPiStormRdb
{
    [Fact]
    public async Task When_CreatingOneLevelDirectory_Then_DirectoryIsCreated()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var mkDirPath = Path.Combine(mediaPath, "mbr", "2", "rdb", "1", "dir1");

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();
        
        // arrange - create mdr disk
        await MbrTestHelper.CreateMbrDiskWithFat16AndPiStormRdbPartitions(testCommandHelper, mediaPath);

        // arrange - create fs mkdir command
        var fsMkDirCommand = new FsMkDirCommand(new NullLogger<FsMkDirCommand>(), testCommandHelper, [],
            mkDirPath);

        // act - execute fs mkdir command
        var result = await fsMkDirCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - root directory contains dir1 entry
        var entries = await PiStormRdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper,
            Path.Combine(mediaPath, "mbr", "2", "rdb", "1"));
        Assert.Equal(["dir1"], entries.Select(x => x.Name).Order());
    }

    [Fact]
    public async Task When_CreatingMultiLevelDirectories_Then_DirectoriesIsCreated()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var mkDirPath = Path.Combine(mediaPath, "mbr", "2", "rdb", "1", "dir1", "dir2", "dir3");
        
        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();
        
        // arrange - create mdr disk
        await MbrTestHelper.CreateMbrDiskWithFat16AndPiStormRdbPartitions(testCommandHelper, mediaPath);

        // arrange - create fs mkdir command
        var fsMkDirCommand = new FsMkDirCommand(new NullLogger<FsMkDirCommand>(), testCommandHelper, [],
            mkDirPath);

        // act - execute fs mkdir command
        var result = await fsMkDirCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - root directory contains dir1 entry
        var entries = await PiStormRdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper,
            Path.Combine(mediaPath, "mbr", "2", "rdb", "1"));
        Assert.Equal(["dir1"], entries.Where(x => x.Type == EntryType.Dir).Select(x => x.Name).Order());
            
        // assert - dir1 root directory contains dir2 entry
        entries = await PiStormRdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper,
            Path.Combine(mediaPath, "mbr", "2", "rdb", "1", "dir1"));
        Assert.Equal(["dir2"], entries.Where(x => x.Type == EntryType.Dir).Select(x => x.Name).Order());

        // assert - dir2 root directory contains dir3 entry
        entries = await PiStormRdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper,
            Path.Combine(mediaPath, "mbr", "2", "rdb", "1", "dir1", "dir2"));
        Assert.Equal(["dir3"], entries.Where(x => x.Type == EntryType.Dir).Select(x => x.Name).Order());
    }

    [Fact]
    public async Task When_CreatingExistingDirectory_Then_DirectoryIsCreated()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var mkDirPath = Path.Combine(mediaPath, "mbr", "2", "rdb", "1", "dir3");

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create mdr disk
        await MbrTestHelper.CreateMbrDiskWithFat16AndPiStormRdbPartitions(testCommandHelper, mediaPath);

        // arrange - create existing path
        await PiStormRdbTestHelper.CreateDirectory(testCommandHelper,
            Path.Combine(mediaPath, "mbr", "2", "rdb", "1", "dir3"));
        
        // arrange - create fs mkdir command
        var fsMkDirCommand = new FsMkDirCommand(new NullLogger<FsMkDirCommand>(), testCommandHelper, [],
            mkDirPath);

        // act - execute fs mkdir command
        var result = await fsMkDirCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - root directory contains dir3 entry
        var entries = await PiStormRdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper,
            Path.Combine(mediaPath, "mbr", "2", "rdb", "1"));
        Assert.Equal(["dir3"], entries.Select(x => x.Name).Order());
    }

    [Fact]
    public async Task When_CreatingDirectoryForNonSupportedMedia_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var mkDirPath = Path.Combine(mediaPath, "mbr", "2", "rdb", "1", "dir3");
        
        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create existing media path as a file to simulate non-supported media
        await File.WriteAllTextAsync(mediaPath, string.Empty);

        // arrange - create fs mkdir command
        var fsMkDirCommand = new FsMkDirCommand(new NullLogger<FsMkDirCommand>(), testCommandHelper, [],
            mkDirPath);

        // act - execute fs mkdir command
        var result = await fsMkDirCommand.Execute(CancellationToken.None);

        // assert - error is returned
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFaulted);
    }

    [Fact]
    public async Task When_CreatingDirectoryWithExistingFileInPath_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var mkDirPath = Path.Combine(mediaPath, "mbr", "2", "rdb", "1", "dir1", "file1.txt", "dir5");

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create pistorm rdb disk
            await PiStormRdbTestHelper.CreatePiStormRdbDisk(testCommandHelper, mediaPath);

            // arrange - create existing file in path
            await PiStormRdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper,
                Path.Combine(mediaPath, "mbr", "2", "rdb", "1"));
                
            // arrange - create fs mkdir command
            var fsMkDirCommand = new FsMkDirCommand(new NullLogger<FsMkDirCommand>(), testCommandHelper, [],
                mkDirPath);

            // act - execute fs mkdir command
            var result = await fsMkDirCommand.Execute(CancellationToken.None);
                
            // assert - error is returned
            Assert.False(result.IsSuccess);
            Assert.True(result.IsFaulted);
        }
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }
}