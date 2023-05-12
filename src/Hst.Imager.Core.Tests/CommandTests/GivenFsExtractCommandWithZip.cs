namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class GivenFsExtractCommandWithZip : FsCommandTestBase
{
    [Fact]
    public async Task WhenExtractingAllRecursivelyFromZipToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.zip";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            CreateZipFileWithDirectoriesAndFiles(srcPath);

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
    public async Task WhenExtractingAllRecursivelyFromZipSubdirectoryToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.zip";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            CreateZipFileWithDirectoriesAndFiles(srcPath);

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
    public async Task WhenExtractingAllRecursivelyFromZipWithWildcardToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.zip";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            CreateZipFileWithDirectoriesAndFiles(srcPath);

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

            // assert - test.txt file was extracted
            var file3 = Path.Combine(destPath, "dir1", "file3.txt");
            Assert.Equal(file3, files.FirstOrDefault(x => x.Equals(file3, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
    
    [Fact]
    public async Task WhenExtractingAFileFromZipToLocalDirectoryThenFileIsExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.zip";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            CreateZipFileWithDirectoriesAndFiles(srcPath);

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

            // assert - 1 file was extracted
            Assert.Single(files);

            // assert - file1.txt file was extracted
            var file1 = Path.Combine(destPath, "file1.txt");
            Assert.Equal(file1, files.FirstOrDefault(x => x.Equals(file1, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    [Fact]
    public async Task WhenExtractingAFileFromZipSubdirectoryToLocalDirectoryThenFileIsExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.zip";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            CreateZipFileWithDirectoriesAndFiles(srcPath);

            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "dir1", "file3.txt"), destPath, true, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);

            // assert - 1 file was extracted
            Assert.Single(files);

            // assert - file3.txt file was extracted
            var file3TxtPath = Path.Combine(destPath, "file3.txt");
            Assert.Equal(file3TxtPath, files.FirstOrDefault(x => x.Equals(file3TxtPath, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
    
    private void CreateZipFileWithDirectoriesAndFiles(string path)
    {
        using var stream = File.Open(path, FileMode.Create, FileAccess.ReadWrite);
        using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Create);
        zipArchive.CreateEntry("file1.txt");
        zipArchive.CreateEntry("file2.txt");
        zipArchive.CreateEntry(@"dir1\file3.txt");
        zipArchive.CreateEntry(@"dir1\test.txt");
    }
}