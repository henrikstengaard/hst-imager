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

        try
        {
            await CreateDos3FormattedAdf(srcPath);
            await CreateDos3AdfFiles(srcPath);

            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);

            // assert - 4 files was extracted
            Assert.Equal(4, files.Length);

            // assert - file1.txt file was extracted
            var file1 = Path.Combine(destPath, "file1.txt");
            Assert.Equal(file1, files.FirstOrDefault(x => x.Equals(file1, StringComparison.OrdinalIgnoreCase)));

            // assert - file2.txt file was extracted
            var file2 = Path.Combine(destPath, "file2.txt");
            Assert.Equal(file2, files.FirstOrDefault(x => x.Equals(file2, StringComparison.OrdinalIgnoreCase)));

            // assert - file3.txt file was extracted
            var file3 = Path.Combine(destPath, "dir1", "file3.txt");
            Assert.Equal(file3, files.FirstOrDefault(x => x.Equals(file3, StringComparison.OrdinalIgnoreCase)));

            // assert - test.txt file was extracted
            var test = Path.Combine(destPath, "dir1", "test.txt");
            Assert.Equal(test, files.FirstOrDefault(x => x.Equals(test, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    [Fact]
    public async Task WhenExtractingAllRecursivelyFromAdfSubdirectoryToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
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
                Path.Combine(srcPath, "dir1"), destPath, true, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);

            // assert - 2 files was extracted
            Assert.Equal(2, files.Length);

            // assert - file3.txt file was extracted
            var file3 = Path.Combine(destPath, "file3.txt");
            Assert.Equal(file3, files.FirstOrDefault(x => x.Equals(file3, StringComparison.OrdinalIgnoreCase)));

            // assert - test.txt file was extracted
            var test = Path.Combine(destPath, "test.txt");
            Assert.Equal(test, files.FirstOrDefault(x => x.Equals(test, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
    
    [Fact]
    public async Task WhenExtractingAllRecursivelyFromAdfWithWildcardToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
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
                Path.Combine(srcPath, "file*.txt"), destPath, true, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);

            // assert - 3 files was extracted
            Assert.Equal(3, files.Length);

            // assert - file1.txt file was extracted
            var file1 = Path.Combine(destPath, "file1.txt");
            Assert.Equal(file1, files.FirstOrDefault(x => x.Equals(file1, StringComparison.OrdinalIgnoreCase)));

            // assert - file2.txt file was extracted
            var file2 = Path.Combine(destPath, "file2.txt");
            Assert.Equal(file2, files.FirstOrDefault(x => x.Equals(file2, StringComparison.OrdinalIgnoreCase)));

            // assert - file3.txt file was extracted
            var file3 = Path.Combine(destPath, "dir1", "file3.txt");
            Assert.Equal(file3, files.FirstOrDefault(x => x.Equals(file3, StringComparison.OrdinalIgnoreCase)));
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

        try
        {
            await CreateDos3FormattedAdf(srcPath);
            await CreateDos3AdfFiles(srcPath);

            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "file1.txt"), destPath, true, true);

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
}