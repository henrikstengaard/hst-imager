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

    private void CreateIso9660WithDirectoriesAndFiles(string path)
    {
        var builder = new DiscUtils.Iso9660.CDBuilder
        {
            UseJoliet = true
        };
        builder.AddFile("file1.txt", Array.Empty<byte>());
        builder.AddFile("file2.txt", Array.Empty<byte>());
        builder.AddDirectory("dir1");
        builder.AddFile(@"dir1\file3.txt", Array.Empty<byte>());
        builder.AddFile(@"dir1\test.txt", Array.Empty<byte>());
        builder.Build(path);
    }
}