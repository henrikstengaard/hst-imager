namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Commands;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Core.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Models;
using Xunit;
using PartitionInfo = DiscUtils.Partitions.PartitionInfo;

public class GivenFsCopyCommandWithGptNtfsFormattedDisk : FsCommandTestBase
{
    [Fact]
    public async Task WhenCopyingAllRecursivelyFromDiskToLocalDirectoryThenDirectoriesAndFilesAreCopied()
    {
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}-local";

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(srcPath);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - source disk image file with directories
            CreateGptNtfsFormattedDisk(testCommandHelper, srcPath, 10.MB());
            using (var media = DiskFileSystemHelper.GetDiskMedia(testCommandHelper, srcPath))
            {
                DiskFileSystemHelper.CreateGptNtfsDirectoriesAndFiles(DiskFileSystemHelper.ToDisk(media));
            }

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "gpt", "1"), destPath, true, false, true);

            // act - execute fs copy command
            var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
            Assert.Equal(string.Empty, result.Error?.ToString() ?? string.Empty);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);

            // assert - 4 files was extracted
            Assert.Equal(4, files.Length);

            // assert - file1.txt file was extracted
            var file1 = Path.Combine(destPath, "file1.txt");
            Assert.Equal(file1, files.FirstOrDefault(x => x.Equals(file1, StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());

            // assert - file2.txt file was extracted
            var file2 = Path.Combine(destPath, "file2.txt");
            Assert.Equal(file2, files.FirstOrDefault(x => x.Equals(file2, StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());

            // assert - file3.txt file was extracted
            var file3 = Path.Combine(destPath, "dir1", "file3.txt");
            Assert.Equal(file3, files.FirstOrDefault(x => x.Equals(file3, StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());

            // assert - test.txt file was extracted
            var test = Path.Combine(destPath, "dir1", "test.txt");
            Assert.Equal(test, files.FirstOrDefault(x => x.Equals(test, StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    [Fact]
    public async Task WhenCopyingAllRecursivelyFromLocalDirectoryToDiskThenDirectoriesAndFilesAreCopied()
    {
        var srcPath = $"{Guid.NewGuid()}-local";
        var destPath = $"{Guid.NewGuid()}.vhd";

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(srcPath);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - source directories and files
            DiskFileSystemHelper.CreateLocalDirectoriesAndFiles(srcPath);
            
            // arrange - destination disk image
            CreateGptNtfsFormattedDisk(testCommandHelper, destPath, 10.MB());

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, Path.Combine(destPath, "gpt", "1"), true, false, true);

            // act - execute fs copy command
            var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
            Assert.Equal(string.Empty, result.Error?.ToString() ?? string.Empty);
            Assert.True(result.IsSuccess);

            // assert - get media
            using var media = DiskFileSystemHelper.GetDiskMedia(testCommandHelper, destPath);

            // arrange - get ntfs file system
            var fileSystem = DiskFileSystemHelper.GetGptNtfsFileSystem(DiskFileSystemHelper.ToDisk(media));

            // arrange - get files in root directory
            var files = fileSystem.GetFiles("").ToList();
            
            // assert - 2 files in root directory
            Assert.Equal(2, files.Count);

            // assert - file1.txt file exists
            Assert.Equal("file1.txt", files.FirstOrDefault(x => x.Equals("file1.txt", StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());
            
            // assert - file2.txt file exists
            var file2 = Path.Combine(destPath, "file2.txt");
            Assert.Equal("file2.txt", files.FirstOrDefault(x => x.Equals("file2.txt", StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());

            // arrange - get directories in root directory
            var directories = fileSystem.GetDirectories("").ToList();

            // assert - 1 directory in root directory
            Assert.Single(directories);
            
            // arrange - get files in dir1 directory
            files = fileSystem.GetFiles("dir1").ToList();
            
            // assert - 2 files in dir1 directory
            Assert.Equal(2, files.Count);

            // assert - file3.txt file was extracted
            var file3 = Path.Combine("dir1", "file3.txt");
            Assert.Equal(file3, files.FirstOrDefault(x => x.Equals(file3, StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());

            // assert - test.txt file was extracted
            var test = Path.Combine("dir1", "test.txt");
            Assert.Equal(test, files.FirstOrDefault(x => x.Equals(test, StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
}