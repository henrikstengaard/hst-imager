using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenFsCopyCommandCopyingSingleFileFromAndToSameLocalDirectory : FsCommandTestBase
{
    [Fact]
    public async Task When_CopyingToDirFromAndToSameLocalMedia_Then_FileIsCopied()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir1", "dir3");

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
            Assert.True(result.IsSuccess);
            
            // assert - directories exist
            var expectedDirs = new[]
            {
                Path.Combine(mediaPath, "dir1"),
                Path.Combine(mediaPath, "dir1", "dir3"),
                Path.Combine(mediaPath, "dir2")
            };
            var actualDirs = Directory.GetDirectories(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualDirs);
            Assert.Equal(expectedDirs, actualDirs);
            
            var expectedFiles = new[]
            {
                Path.Combine(mediaPath, "dir1", "dir3", "file1.txt"),
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
    public async Task When_CopyingToNewNameFromAndToSameLocalMedia_Then_FileIsCopied()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir1", "file1_copy.txt");

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
            Assert.True(result.IsSuccess);
            
            // assert - directories exist
            var expectedDirs = new[]
            {
                Path.Combine(mediaPath, "dir1"),
                Path.Combine(mediaPath, "dir1", "dir3"),
                Path.Combine(mediaPath, "dir2")
            };
            var actualDirs = Directory.GetDirectories(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualDirs);
            Assert.Equal(expectedDirs, actualDirs);
            
            var expectedFiles = new[]
            {
                Path.Combine(mediaPath, "dir1", "file1_copy.txt"),
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
    public async Task When_CopyingToNonExistingRootDirectoryFromAndToSameLocalMedia_Then_FileIsCopiedAndRenamed()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
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
            
            // assert - root directory contains 2 dir entries
            var expectedDirs = new[]
            {
                Path.Combine(mediaPath, "dir1"),
                Path.Combine(mediaPath, "dir1", "dir3"),
                Path.Combine(mediaPath, "dir2")
            };
            var actualDirs = Directory.GetDirectories(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualDirs);
            Assert.Equal(expectedDirs, actualDirs);
            
            // assert - root directory contains 1 file entry
            var expectedFiles = new[]
            {
                Path.Combine(mediaPath, "dir1", "file1.txt"),
                Path.Combine(mediaPath, "dir4")
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
    public async Task When_CopyingToNonExistingSubDirectoryFromAndToSameLocalMedia_Then_ErrorIsReturned()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
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
            
            // assert - error is returned
            Assert.False(result.IsSuccess);
            Assert.True(result.IsFaulted);
            Assert.NotNull(result.Error);
            Assert.IsType<PathNotFoundError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CopyingToNonExistingSubDirectoryFromAndToSameLocalMediaWithCreateDestDir_Then_FileIsCopied()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir4", "dir5");
        const bool createDestDir = true;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, true, false, true,
                makeDirectory: createDestDir);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            
            // assert - copy is successful
            Assert.True(result.IsSuccess);
            
            // assert - dir4, dir5 directory contains 1 file entry
            var expectedFiles = new[]
            {
                Path.Combine(mediaPath, "dir4", "dir5", "file1.txt"),
            };
            var actualFiles = Directory.GetFiles(Path.Combine(mediaPath, "dir4", "dir5"), "*.*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
}