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
using Directory = System.IO.Directory;

public class GivenFsExtractCommandWithAdf : FsCommandTestBase
{
    [Fact]
    public async Task WhenExtractingAllRecursivelyFromAdfToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.adf";
        var destPath = $"{Guid.NewGuid()}-extract";
        const bool recursive = true;

        try
        {
            await CreateDos3FormattedAdf(srcPath);
            await CreateDos3AdfFiles(srcPath);

            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true);

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
    public async Task
        WhenExtractingAllRecursivelyFromAdfSubdirectoryToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.adf";
        var destPath = $"{Guid.NewGuid()}-extract";
        const bool recursive = true;

        try
        {
            await CreateDos3FormattedAdf(srcPath);
            await CreateDos3AdfFiles(srcPath);

            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "dir1"), destPath, recursive, false, true);

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
    public async Task
        WhenExtractingAllRecursivelyFromAdfWithWildcardToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.adf";
        var destPath = $"{Guid.NewGuid()}-extract";
        const bool recursive = true;

        try
        {
            await CreateDos3FormattedAdf(srcPath);
            await CreateDos3AdfFiles(srcPath);

            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "file*.txt"), destPath, recursive, false, true);

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
    public async Task WhenExtractingAFileFromAdfToLocalDirectoryThenFileIsExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.adf";
        var destPath = $"{Guid.NewGuid()}-extract";
        const bool recursive = true;

        try
        {
            await CreateDos3FormattedAdf(srcPath);
            await CreateDos3AdfFiles(srcPath);

            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "file1.txt"), destPath, recursive, false, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
            Assert.Single(files);

            var file1 = Path.Combine(destPath, "file1.txt");
            Assert.Equal(file1, files.FirstOrDefault(x => x.Equals(file1, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    [Fact]
    public async Task WhenExtractingAFileFromAdfSubdirectoryToLocalDirectoryThenFileIsExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.adf";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            await CreateDos3FormattedAdf(srcPath);
            await CreateDos3AdfFiles(srcPath);

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
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);

            // assert - 1 file was extracted
            Assert.Single(files);

            // assert - file3.txt file was extracted
            var file3TxtPath = Path.Combine(destPath, "file3.txt");
            Assert.Equal(file3TxtPath,
                files.FirstOrDefault(x => x.Equals(file3TxtPath, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
}