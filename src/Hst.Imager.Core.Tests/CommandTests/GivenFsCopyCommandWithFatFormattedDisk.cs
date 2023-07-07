namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Commands;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Core.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Models;
using Xunit;

public class GivenFsCopyCommandWithFatFormattedDisk : FsCommandTestBase
{
    [Fact]
    public async Task WhenCopyingAllRecursivelyFromDiskToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(srcPath);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - source disk image file with directories
            CreateFatFormattedDisk(testCommandHelper, srcPath, 10.MB());
            CreateDirectoriesAndFiles(testCommandHelper, srcPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "mbr", "1"), destPath, true, false, true);

            // act - extract
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

    private void CreateDirectoriesAndFiles(TestCommandHelper testCommandHelper, string path)
    {
        var mediaResult = testCommandHelper.GetWritableFileMedia(path);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }

        using var media = mediaResult.Value;
        var stream = media.Stream;
        
        using var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(stream, Ownership.None);
        var biosPartitionTable = new BiosPartitionTable(disk);
        var partition = biosPartitionTable.Partitions.FirstOrDefault();

        if (partition == null)
        {
            throw new IOException("No partitions in master boot record");
        }

        using var fatFileSystem = new FatFileSystem(partition.Open());

        using (var file1 = fatFileSystem.OpenFile("file1.txt", FileMode.Create))
        {
            using (var streamWriter = new StreamWriter(file1, Encoding.UTF8))
            {
                streamWriter.Write("test");
            }
        }

        using (fatFileSystem.OpenFile("file2.txt", FileMode.Create))
        {
        }

        fatFileSystem.CreateDirectory("dir1");

        using (fatFileSystem.OpenFile("dir1\\file3.txt", FileMode.Create))
        {
        }

        using (fatFileSystem.OpenFile("dir1\\test.txt", FileMode.Create))
        {
        }
    }
}