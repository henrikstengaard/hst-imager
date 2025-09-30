using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands.FsCommands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsMkDirCommandWithLocalDirectory
{
    [Fact]
    public async Task When_CreatingOneLevelDirectory_Then_DirectoryIsCreated()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var mkDirPath = Path.Combine(mediaPath, "dir1");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();
            
            // arrange - create media path
            Directory.CreateDirectory(mediaPath);

            // arrange - create fs mkdir command
            var fsMkDirCommand = new FsMkDirCommand(new NullLogger<FsMkDirCommand>(), testCommandHelper, [],
                mkDirPath);

            // act - execute fs mkdir command
            var result = await fsMkDirCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - directory exists
            Assert.True(Directory.Exists(mkDirPath));
        }
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CreatingMultiLevelDirectories_Then_DirectoriesIsCreated()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var mkDirPath = Path.Combine(mediaPath, "dir1", "dir2", "dir3");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();
            
            // arrange - create media path
            Directory.CreateDirectory(mediaPath);

            // arrange - create fs mkdir command
            var fsMkDirCommand = new FsMkDirCommand(new NullLogger<FsMkDirCommand>(), testCommandHelper, [],
                mkDirPath);

            // act - execute fs mkdir command
            var result = await fsMkDirCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - directory exists
            Assert.True(Directory.Exists(mkDirPath));
        }
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CreatingExistingDirectory_Then_DirectoryIsCreated()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var mkDirPath = Path.Combine(mediaPath, "dir3");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();
            
            // arrange - create existing path
            Directory.CreateDirectory(mkDirPath);

            // arrange - create fs mkdir command
            var fsMkDirCommand = new FsMkDirCommand(new NullLogger<FsMkDirCommand>(), testCommandHelper, [],
                mkDirPath);

            // act - execute fs mkdir command
            var result = await fsMkDirCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - directory exists
            Assert.True(Directory.Exists(mkDirPath));
        }
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CreatingDirectoryForNonSupportedMedia_Then_ErrorIsReturned()
    {
        var mediaPath = Guid.NewGuid().ToString();
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
        var mediaPath = Guid.NewGuid().ToString();
        var mkDirPath = Path.Combine(mediaPath, "dir1", "file1.txt", "dir5");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            await testCommandHelper.AddTestMedia(mediaPath);

            // arrange - create existing file in path
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            
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