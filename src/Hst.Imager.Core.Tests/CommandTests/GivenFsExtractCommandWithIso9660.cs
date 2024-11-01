namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class GivenFsExtractCommandWithIso9660 : FsCommandTestBase
{
    [Fact]
    public async Task WhenExtractingAllRecursivelyFromIsoToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.iso";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            CreateIso9660WithDirectoriesAndFiles(srcPath);

            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories).OrderBy(x => x).ToArray();

            // assert - 5 files was extracted
            Assert.Equal(5, files.Length);

            // assert - files are extracted
            var expectedFiles = new[]
            {
                Path.Combine(destPath, "dir1", "dir2", "file4.txt"),
                Path.Combine(destPath, "dir1", "file3.txt"),
                Path.Combine(destPath, "dir1", "test.txt"),
                Path.Combine(destPath, "file1.txt"),
                Path.Combine(destPath, "file2.txt")
            };
            Assert.Equal(expectedFiles, files);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    [Fact]
    public async Task WhenExtractingAllRecursivelyFromIsoSubdirectoryToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.iso";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            CreateIso9660WithDirectoriesAndFiles(srcPath);

            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "dir1"), destPath, true, false, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories).OrderBy(x => x).ToArray();

            // assert - 3 files was extracted
            Assert.Equal(3, files.Length);

            // assert - files are extracted
            var expectedFiles = new[]
            {
                Path.Combine(destPath, "dir2", "file4.txt"),
                Path.Combine(destPath, "file3.txt"),
                Path.Combine(destPath, "test.txt")
            };
            Assert.Equal(expectedFiles, files);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
    
    [Fact]
    public async Task WhenExtractingAllRecursivelyFromIsoWithWildcardToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.iso";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            CreateIso9660WithDirectoriesAndFiles(srcPath);

            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "file*.txt"), destPath, true, false, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories).OrderBy(x => x).ToArray();

            // assert - 4 files was extracted
            Assert.Equal(4, files.Length);

            // assert - files are extracted
            var expectedFiles = new[]
            {
                Path.Combine(destPath, "dir1", "dir2", "file4.txt"),
                Path.Combine(destPath, "dir1", "file3.txt"),
                Path.Combine(destPath, "file1.txt"),
                Path.Combine(destPath, "file2.txt")
            };
            Assert.Equal(expectedFiles, files);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
    
    [Fact]
    public async Task WhenExtractingAFileFromIsoToLocalDirectoryThenFileIsExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.iso";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            CreateIso9660WithDirectoriesAndFiles(srcPath);

            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "file1.txt"), destPath, true, false, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories).OrderBy(x => x).ToArray();

            // assert - 1 file was extracted
            Assert.Single(files);

            // assert - file is extracted
            var expectedFiles = new[]
            {
                Path.Combine(destPath, "file1.txt"),
            };
            Assert.Equal(expectedFiles, files);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    [Fact]
    public async Task WhenExtractingAFileFromIsoSubdirectoryToLocalDirectoryThenFileIsExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.iso";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            CreateIso9660WithDirectoriesAndFiles(srcPath);

            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "dir1", "file3.txt"), destPath, true, false, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories).OrderBy(x => x).ToArray();

            // assert - 1 file was extracted
            Assert.Single(files);

            // assert - file is extracted
            var expectedFiles = new[]
            {
                Path.Combine(destPath, "file3.txt"),
            };
            Assert.Equal(expectedFiles, files);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
}