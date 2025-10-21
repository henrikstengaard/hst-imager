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

public class GivenFsCopyCommandWithMbrFatFormattedDisk : FsCommandTestBase
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
            await testCommandHelper.AddTestMedia(srcPath);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - source disk image file with directories
            await CreateMbrFatFormattedDisk(testCommandHelper, srcPath, 10.MB());
            await CreateDirectoriesAndFiles(testCommandHelper, srcPath);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);

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
            Assert.Equal(file1,
                files.FirstOrDefault(x => x.Equals(file1, StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());

            // assert - file2.txt file was extracted
            var file2 = Path.Combine(destPath, "file2.txt");
            Assert.Equal(file2,
                files.FirstOrDefault(x => x.Equals(file2, StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());

            // assert - file3.txt file was extracted
            var file3 = Path.Combine(destPath, "dir1", "file3.txt");
            Assert.Equal(file3,
                files.FirstOrDefault(x => x.Equals(file3, StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());

            // assert - test.txt file was extracted
            var test = Path.Combine(destPath, "dir1", "test.txt");
            Assert.Equal(test,
                files.FirstOrDefault(x => x.Equals(test, StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    private async Task CreateDirectoriesAndFiles(TestCommandHelper testCommandHelper, string path)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }

        using var media = mediaResult.Value;
        var stream = media.Stream;

        var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(stream, Ownership.None);
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

    [Fact]
    public async Task When_CopyFromLocalSubDirectoryToDiskSubDirectory_Then_DirectoryIsCreatedAndFilesCopied()
    {
        var srcPath = $"{Guid.NewGuid()}";
        var destPath = $"{Guid.NewGuid()}.img";

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - source local directory and files
            var dir1Path = Path.Combine(srcPath, "src");
            Directory.CreateDirectory(dir1Path);
            var file1Path = Path.Combine(dir1Path, "file1.bin");
            await File.WriteAllBytesAsync(file1Path, new byte[50000]);
            var file2Path = Path.Combine(dir1Path, "file2.bin");
            await File.WriteAllBytesAsync(file2Path, new byte[250000]);

            // arrange - destination mbr fat formatted disk
            testCommandHelper.AddTestMedia(destPath, 10.MB());
            await CreateMbrFatFormattedDisk(testCommandHelper, destPath, 4.GB());

            // arrange - create destination directories
            await TestHelper.CreateMbrFatDirectory(testCommandHelper, destPath, ["dest"]);

            // arrange - create fs copy command
            var cancellationTokenSource = new CancellationTokenSource();
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "src"), Path.Combine(destPath, "mbr", "1", "dest"), true, false, true);

            // act - copy directories and files
            var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
            Assert.Equal(string.Empty, result.Error?.ToString() ?? string.Empty);
            Assert.True(result.IsSuccess);

            // arrange - get fat file system
            var mediaResult = await testCommandHelper.GetReadableMedia(
                Enumerable.Empty<IPhysicalDrive>(), destPath);
            using var media = mediaResult.Value;
            media.Stream.Position = 0;
            var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new DiscUtils.Raw.Disk(media.Stream,
                    Ownership.None);
            var biosPartitionTable = new BiosPartitionTable(disk);
            var partitionStream = biosPartitionTable.Partitions[0].Open();
            var fatFileSystem = new FatFileSystem(partitionStream);

            // assert - 1 directory in root directory
            var dirs = fatFileSystem.GetDirectories("").ToList();
            Assert.Single(dirs);

            // assert - "dest" exists in root directory
            var destDirPath = "dest";
            Assert.Equal(destDirPath,
                dirs.FirstOrDefault(x => x.Equals(destDirPath, StringComparison.OrdinalIgnoreCase))
                    ?.ToLowerInvariant());

            // assert - 0 files in root directory
            var files = fatFileSystem.GetFiles("").ToList();
            Assert.Empty(files);

            // assert - 0 directories in "dest" directory
            dirs = fatFileSystem.GetDirectories(destDirPath).ToList();
            Assert.Empty(dirs);

            // assert - 2 files in root directory
            files = fatFileSystem.GetFiles(destDirPath).ToList();
            Assert.Equal(2, files.Count);

            // assert - file1.bin file was copied and has size 50000 bytes
            file1Path = Path.Combine(destDirPath, "file1.bin");
            Assert.Equal(file1Path,
                files.FirstOrDefault(x => x.Equals(file1Path, StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());
            var file1Info = fatFileSystem.GetFileInfo(file1Path);
            Assert.Equal(50000, file1Info.Length);

            // assert - file2.bin file was copied and has size 250000 bytes
            file2Path = Path.Combine(destDirPath, "file2.bin");
            Assert.Equal(file2Path,
                files.FirstOrDefault(x => x.Equals(file2Path, StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());
            var file2Info = fatFileSystem.GetFileInfo(file2Path);
            Assert.Equal(250000, file2Info.Length);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }


    [Fact]
    public async Task When_CopyLocalToTwoSubdirectories()
    {
        var srcPath = $"{Guid.NewGuid()}";
        var destPath = $"{Guid.NewGuid()}.img";

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - source local directory and files
            var dir1Path = Path.Combine(srcPath, "src");
            Directory.CreateDirectory(dir1Path);
            var file1Path = Path.Combine(dir1Path, "file1.bin");
            await File.WriteAllBytesAsync(file1Path, new byte[50000]);
            var file2Path = Path.Combine(dir1Path, "file2.bin");
            await File.WriteAllBytesAsync(file2Path, new byte[250000]);

            // arrange - destination mbr fat formatted disk
            testCommandHelper.AddTestMedia(destPath, 10.MB());
            await CreateMbrFatFormattedDisk(testCommandHelper, destPath, 4.GB());
            
            // arrange - create destination directories
            await TestHelper.CreateMbrFatDirectory(testCommandHelper, destPath, ["dir1", "dir2"]);

            // arrange - create fs copy command
            var cancellationTokenSource = new CancellationTokenSource();
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "src"), Path.Combine(destPath, "mbr", "1", "dir1", "dir2"), true, false, true);

            // act - copy directories and files
            var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
            Assert.Equal(string.Empty, result.Error?.ToString() ?? string.Empty);
            Assert.True(result.IsSuccess);


            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), Path.Combine(destPath, "mbr", "1", "dir1", "dir2"), true);
            var entries = new List<Models.FileSystems.Entry>();
            fsDirCommand.EntriesRead += (sender, args) =>
            {
                entries = args.EntriesInfo.Entries.ToList();
            };
            var r2 = await fsDirCommand.Execute(cancellationTokenSource.Token);
            Assert.True(r2.IsSuccess);

            // arrange - get fat file system
            var mediaResult = await testCommandHelper.GetReadableMedia(
                Enumerable.Empty<IPhysicalDrive>(), destPath);
            using var media = mediaResult.Value;
            media.Stream.Position = 0;
            var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new DiscUtils.Raw.Disk(media.Stream,
                    Ownership.None);
            var biosPartitionTable = new BiosPartitionTable(disk);
            var partitionStream = biosPartitionTable.Partitions[0].Open();
            var fatFileSystem = new FatFileSystem(partitionStream);

            // assert - 1 directory in root directory
            var dirs = fatFileSystem.GetDirectories("").ToList();
            Assert.Single(dirs);

            // assert - "dir1" exists in root directory
            dir1Path = "dir1";
            Assert.Equal(dir1Path,
                dirs.FirstOrDefault(x => x.Equals(dir1Path, StringComparison.OrdinalIgnoreCase))
                    ?.ToLowerInvariant());

            // assert - 0 files in root directory
            var files = fatFileSystem.GetFiles("").ToList();
            Assert.Empty(files);

            // assert - 1 directory in "dir1" directory
            dirs = fatFileSystem.GetDirectories(dir1Path).ToList();
            Assert.Single(dirs);

            // assert - "dir2" exists in root directory
            var dir2Path = Path.Combine(dir1Path, "dir2");
            Assert.Equal(dir2Path,
                dirs.FirstOrDefault(x => x.Equals(dir2Path, StringComparison.OrdinalIgnoreCase))
                    ?.ToLowerInvariant());

            // assert - 2 files in dir2 directory
            files = fatFileSystem.GetFiles(dir2Path).ToList();
            Assert.Equal(2, files.Count);

            // assert - file1.bin file was copied and has size 50000 bytes
            file1Path = Path.Combine(dir2Path, "file1.bin");
            Assert.Equal(file1Path,
                files.FirstOrDefault(x => x.Equals(file1Path, StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());
            var file1Info = fatFileSystem.GetFileInfo(file1Path);
            Assert.Equal(50000, file1Info.Length);

            // assert - file2.bin file was copied and has size 250000 bytes
            file2Path = Path.Combine(dir2Path, "file2.bin");
            Assert.Equal(file2Path,
                files.FirstOrDefault(x => x.Equals(file2Path, StringComparison.OrdinalIgnoreCase))?.ToLowerInvariant());
            var file2Info = fatFileSystem.GetFileInfo(file2Path);
            Assert.Equal(250000, file2Info.Length);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
}