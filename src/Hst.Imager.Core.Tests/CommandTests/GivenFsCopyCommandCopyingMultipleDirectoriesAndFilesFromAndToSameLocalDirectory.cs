using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenFsCopyCommandCopyingMultipleDirectoriesAndFilesFromAndToSameLocalDirectory : FsCommandTestBase
{
    [Fact]
    public async Task When_CopyingFromAndToSameLocalDirectory_Then_DirectoriesAndFilesAreCopied()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "dir1", "*");
        var destPath = Path.Combine(mediaPath, "copied");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            Directory.CreateDirectory(destPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - directories exist
            var expectedDirs = new[]
            {
                Path.Combine(mediaPath, "copied"),
                Path.Combine(mediaPath, "copied", "dir3"),
                Path.Combine(mediaPath, "dir1"),
                Path.Combine(mediaPath, "dir1", "dir3"),
                Path.Combine(mediaPath, "dir2")
            };
            var actualDirs = Directory.GetDirectories(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualDirs);
            Assert.Equal(expectedDirs, actualDirs);
            
            // assert - files exist
            var expectedFiles = new[]
            {
                Path.Combine(mediaPath, "copied", "file1.txt"),
                Path.Combine(mediaPath, "dir1", "file1.txt")
            };
            var actualFiles = Directory.GetFiles(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingFromAndToSameLocalDirectoryWithCyclicPath_Then_ErrorIsReturned()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "*");
        var destPath = Path.Combine(mediaPath, "copied");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            Directory.CreateDirectory(destPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);

            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            
            // assert - copy failed and returned cyclic error
            Assert.True(result.IsFaulted);
            Assert.IsType<CyclicPathError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingFromAndToSameLocalDirectoryWithSelfCopyPath_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "dir1", "file1.txt"]);
        var destPath = Path.Combine([mediaPath, "dir1", "file1.txt"]);
        const bool force = true;
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true, forceOverwrite: force);

            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            
            // assert - copy failed and returned self copy error
            Assert.True(result.IsFaulted);
            Assert.IsType<SelfCopyError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingToNonExistingRootDirectoryFromAndToSameLocalDirectory_Then_ErrorIsReturned()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "*");
        var destPath = Path.Combine(mediaPath, "dir4");
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsFaulted);
            Assert.False(result.IsSuccess);
            Assert.IsType<PathNotFoundError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingToNonExistingSubDirectoryFromAndToSameLocalDirectory_Then_ErrorIsReturned()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "*");
        var destPath = Path.Combine(mediaPath, "dir4", "dir5");
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsFaulted);
            Assert.False(result.IsSuccess);
            Assert.IsType<PathNotFoundError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
}