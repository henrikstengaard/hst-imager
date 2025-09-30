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

public class GivenFsMkDirCommandWithAdf
{
    [Fact]
    public async Task When_CreatingOneLevelDirectory_Then_DirectoryIsCreated()
    {
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var mkDirPath = Path.Combine(mediaPath, "dir1");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            await TestHelper.CreateFormattedAdfDisk(testCommandHelper, mediaPath, "Amiga");
            
            // arrange - create fs mkdir command
            var fsMkDirCommand = new FsMkDirCommand(new NullLogger<FsMkDirCommand>(), testCommandHelper, [],
                mkDirPath);

            // act - execute fs mkdir command
            var result = await fsMkDirCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - root directory contains dir1 entry
            var entries = await AdfTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, []);
            Assert.Equal(["dir1"], entries.Where(x => x.Type == EntryType.Dir).Select(x => x.Name).Order());
        }
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CreatingMultiLevelDirectories_Then_DirectoriesIsCreated()
    {
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var mkDirPath = Path.Combine(mediaPath, "dir1", "dir2", "dir3");
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            await TestHelper.CreateFormattedAdfDisk(testCommandHelper, mediaPath, "Amiga");
            
            // arrange - create fs mkdir command
            var fsMkDirCommand = new FsMkDirCommand(new NullLogger<FsMkDirCommand>(), testCommandHelper, [],
                mkDirPath);

            // act - execute fs mkdir command
            var result = await fsMkDirCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - root directory contains dir1 entry
            var entries = await AdfTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, []);
            Assert.Equal(["dir1"], entries.Where(x => x.Type == EntryType.Dir).Select(x => x.Name).Order());
            
            // assert - dir1 root directory contains dir2 entry
            entries = await AdfTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, ["dir1"]);
            Assert.Equal(["dir2"], entries.Where(x => x.Type == EntryType.Dir).Select(x => x.Name).Order());

            // assert - dir2 root directory contains dir3 entry
            entries = await AdfTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, ["dir1", "dir2"]);
            Assert.Equal(["dir3"], entries.Where(x => x.Type == EntryType.Dir).Select(x => x.Name).Order());
        }
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CreatingExistingDirectory_Then_DirectoryIsCreated()
    {
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var mkDirPath = Path.Combine(mediaPath, "dir3");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            await TestHelper.CreateFormattedAdfDisk(testCommandHelper, mediaPath, "Amiga");
            
            // arrange - create existing path
            await AdfTestHelper.CreateDirectory(testCommandHelper, mediaPath, ["dir3"]);
            
            // arrange - create fs mkdir command
            var fsMkDirCommand = new FsMkDirCommand(new NullLogger<FsMkDirCommand>(), testCommandHelper, [],
                mkDirPath);

            // act - execute fs mkdir command
            var result = await fsMkDirCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - root directory contains dir3 entry
            var entries = await AdfTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, []);
            Assert.Equal(["dir3"], entries.Select(x => x.Name).Order());
        }
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CreatingDirectoryForNonSupportedMedia_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var mkDirPath = Path.Combine(mediaPath, "dir3");
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();
            
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
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CreatingDirectoryWithExistingFileInPath_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var mkDirPath = Path.Combine(mediaPath, "dir1", "file1.txt", "dir5");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            await AdfTestHelper.CreateFormattedAdfDisk(testCommandHelper, mediaPath);

            // arrange - create existing file in path
            await AdfTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);
            
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