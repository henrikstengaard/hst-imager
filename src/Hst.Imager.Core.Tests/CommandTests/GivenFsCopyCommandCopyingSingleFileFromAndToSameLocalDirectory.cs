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
}