using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests
{
    public class GivenFsDirCommandWithMbrFatFormattedDisk : FsCommandTestBase
    {
        [Fact]
        public async Task When_ListingEntryInRootDirectory_Then_EntriesAreListed()
        {
            // arrange - paths
            var mbrDiskPath = $"mbr_{Guid.NewGuid()}.vhd";
            var dirPath = Path.Combine(mbrDiskPath, "mbr", "1", "file1.txt");
            const bool recursive = false;

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create mdr disk
            await CreateMbrTestDisk(testCommandHelper, mbrDiskPath);

            // arrange - create fs dir command
            EntriesInfo entriesInfo = null;
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - root contains 1 entry
            var entries = entriesInfo.Entries.ToList();
            Assert.Single(entries);

            // assert - file1.txt
            var file1Entry = entries.FirstOrDefault(x => x.Type == Models.FileSystems.EntryType.File &&
                x.Name.Equals("file1.txt", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(file1Entry);
        }

        [Fact]
        public async Task When_ListingEntryInOneLevelSubdirectoryDepth_Then_EntriesAreListed()
        {
            // arrange - paths
            var mbrDiskPath = $"mbr_{Guid.NewGuid()}.vhd";
            var dirPath = Path.Combine(mbrDiskPath, "mbr", "1", "dir1", "file2.txt");
            const bool recursive = false;

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create mdr disk
            await CreateMbrTestDisk(testCommandHelper, mbrDiskPath);

            // arrange - create fs dir command
            EntriesInfo entriesInfo = null;
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - dir1 contains 1 entry
            var entries = entriesInfo.Entries.ToList();
            Assert.Single(entries);

            // assert - file2.txt
            var file2Entry = entries.FirstOrDefault(x => x.Type == Models.FileSystems.EntryType.File &&
                x.Name.Equals("file2.txt", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(file2Entry);
        }

        [Fact]
        public async Task When_ListingEntryInTwoLevelSubdirectoryDepth_Then_EntriesAreListed()
        {
            // arrange - paths
            var mbrDiskPath = $"mbr_{Guid.NewGuid()}.vhd";
            var dirPath = Path.Combine(mbrDiskPath, "mbr", "1", "dir1", "dir2", "file3.txt");
            const bool recursive = false;

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create mdr disk
            await CreateMbrTestDisk(testCommandHelper, mbrDiskPath);

            // arrange - create fs dir command
            EntriesInfo entriesInfo = null;
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - dir2 contains 1 entry
            var entries = entriesInfo.Entries.ToList();
            Assert.Single(entries);

            // assert - file3.txt
            var file3Entry = entries.FirstOrDefault(x => x.Type == Models.FileSystems.EntryType.File &&
                x.Name.Equals("file3.txt", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(file3Entry);
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectoryWithPattern_Then_EntriesAreListed()
        {
            // arrange - paths
            var mbrDiskPath = $"mbr_{Guid.NewGuid()}.vhd";
            var pattern = "*.txt";
            var dirPath = Path.Combine(mbrDiskPath, "mbr", "1", pattern);
            const bool recursive = false;

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create mdr disk
            await CreateMbrTestDisk(testCommandHelper, mbrDiskPath);

            // arrange - create fs dir command
            EntriesInfo entriesInfo = null;
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - root contains 1 entry
            var entries = entriesInfo.Entries.ToList();
            Assert.Single(entries);

            // assert - file1.txt
            var file1Entry = entries.FirstOrDefault(x => x.Type == Models.FileSystems.EntryType.File &&
                x.Name.Equals("file1.txt", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(file1Entry);
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectoryRecursive_Then_EntriesAreListed()
        {
            // arrange - paths
            var mbrDiskPath = $"mbr_{Guid.NewGuid()}.vhd";
            var dirPath = Path.Combine(mbrDiskPath, "mbr", "1");
            const bool recursive = true;

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create mdr disk
            await CreateMbrTestDisk(testCommandHelper, mbrDiskPath);

            // arrange - create fs dir command
            EntriesInfo entriesInfo = null;
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - entries contain 6 entries
            var entries = entriesInfo.Entries.ToList();
            Assert.Equal(7, entries.Count);

            // assert - directories are listed
            var expectedDirNames = new[]
            {
                    "dir1",
                    Path.Combine("dir1", "dir2")
                };
            var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Equal(expectedDirNames, dirNames);

            // assert - files are listed
            var expectedFileNames = new[]
            {
                    "file1.txt",
                    Path.Combine("dir1", "file2.txt"),
                    Path.Combine("dir1", "dir2", "data.bin"),
                    Path.Combine("dir1", "dir2", "file3.txt"),
                    Path.Combine("dir1", "dir2", "file4.txt")
                };
            var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File)
                .OrderBy (x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Equal(expectedFileNames, fileNames);
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectoryWithPatternRecursive_Then_EntriesAreListed()
        {
            // arrange - paths
            var mbrDiskPath = $"mbr_{Guid.NewGuid()}.vhd";
            var pattern = "*.txt";
            var dirPath = Path.Combine(mbrDiskPath, "mbr", "1", pattern);
            const bool recursive = true;

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create mdr disk
            await CreateMbrTestDisk(testCommandHelper, mbrDiskPath);

            // arrange - create fs dir command
            EntriesInfo entriesInfo = null;
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - entries contain 6 entries
            var entries = entriesInfo.Entries.ToList();
            Assert.Equal(6, entries.Count);

            // assert - directories are listed
            var expectedDirNames = new[]
            {
                "dir1",
                Path.Combine("dir1", "dir2")
            };
            var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Equal(expectedDirNames, dirNames);

            // assert - files are listed
            var expectedFileNames = new[]
            {
                "file1.txt",
                Path.Combine("dir1", "file2.txt"),
                Path.Combine("dir1", "dir2", "file3.txt"),
                Path.Combine("dir1", "dir2", "file4.txt")
            };
            var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Equal(expectedFileNames, fileNames);
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepth_Then_EntriesAreListed()
        {
            // arrange - paths
            var mbrDiskPath = $"mbr_{Guid.NewGuid()}.vhd";
            var dirPath = Path.Combine(mbrDiskPath, "mbr", "1", "dir1");
            const bool recursive = false;

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create mdr disk
            await CreateMbrTestDisk(testCommandHelper, mbrDiskPath);

            // arrange - create fs dir command
            EntriesInfo entriesInfo = null;
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - dir1 contains 2 entries
            var entries = entriesInfo.Entries.ToList();
            Assert.Equal(2, entries.Count);

            // assert - directories are listed
            var expectedDirNames = new[]
            {
                "dir2"
            };
            var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Equal(expectedDirNames, dirNames);

            // assert - files are listed
            var expectedFileNames = new[]
            {
                "file2.txt"
            };
            var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Equal(expectedFileNames, fileNames);
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepthWithPattern_Then_EntriesAreListed()
        {
            // arrange - paths
            var mbrDiskPath = $"mbr_{Guid.NewGuid()}.vhd";
            var pattern = "*.txt";
            var dirPath = Path.Combine(mbrDiskPath, "mbr", "1", "dir1", pattern);
            const bool recursive = false;

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create mdr disk
            await CreateMbrTestDisk(testCommandHelper, mbrDiskPath);

            // arrange - create fs dir command
            EntriesInfo entriesInfo = null;
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - dir1 contains 1 entry
            var entries = entriesInfo.Entries.ToList();
            Assert.Single(entries);

            // assert - no directories are listed
            var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Empty(dirNames);

            // assert - files are listed
            var expectedFileNames = new[]
            {
                "file2.txt"
            };
            var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Equal(expectedFileNames, fileNames);
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepthRecursive_Then_EntriesAreListed()
        {
            // arrange - paths
            var mbrDiskPath = $"mbr_{Guid.NewGuid()}.vhd";
            var dirPath = Path.Combine(mbrDiskPath, "mbr", "1", "dir1");
            const bool recursive = true;

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create mdr disk
            await CreateMbrTestDisk(testCommandHelper, mbrDiskPath);

            // arrange - create fs dir command
            EntriesInfo entriesInfo = null;
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - dir1 contains 5 entries
            var entries = entriesInfo.Entries.ToList();
            Assert.Equal(5, entries.Count);

            // assert - directories are listed
            var expectedDirNames = new[]
            {
                "dir2"
            };
            var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Equal(expectedDirNames, dirNames);

            // assert - files are listed
            var expectedFileNames = new[]
            {
                "file2.txt",
                Path.Combine("dir2", "data.bin"),
                Path.Combine("dir2", "file3.txt"),
                Path.Combine("dir2", "file4.txt")
            };
            var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Equal(expectedFileNames, fileNames);
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepthWithPatternRecursive_Then_EntriesAreListed()
        {
            // arrange - paths
            var mbrDiskPath = $"mbr_{Guid.NewGuid()}.vhd";
            var pattern = "*.txt";
            var dirPath = Path.Combine(mbrDiskPath, "mbr", "1", "dir1", pattern);
            const bool recursive = true;

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create mdr disk
            await CreateMbrTestDisk(testCommandHelper, mbrDiskPath);

            // arrange - create fs dir command
            EntriesInfo entriesInfo = null;
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - dir1 contains 4 entries
            var entries = entriesInfo.Entries.ToList();
            Assert.Equal(4, entries.Count);

            // assert - directories are listed
            var expectedDirNames = new[]
            {
                "dir2"
            };
            var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Equal(expectedDirNames, dirNames);

            // assert - files are listed
            var expectedFileNames = new[]
            {
                "file2.txt",
                Path.Combine("dir2", "file3.txt"),
                Path.Combine("dir2", "file4.txt")
            };
            var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Equal(expectedFileNames, fileNames);
        }

        [Fact]
        public async Task When_ListingEntriesOfTwoLevelsSubdirectoryDepth_Then_EntriesAreListed()
        {
            // arrange - paths
            var mbrDiskPath = $"mbr_{Guid.NewGuid()}.vhd";
            var dirPath = Path.Combine(mbrDiskPath, "mbr", "1", "dir1", "dir2");
            const bool recursive = false;

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create mdr disk
            await CreateMbrTestDisk(testCommandHelper, mbrDiskPath);

            // arrange - create fs dir command
            EntriesInfo entriesInfo = null;
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - dir2 contains 3 entries
            var entries = entriesInfo.Entries.ToList();
            Assert.Equal(3, entries.Count);

            // assert - no directories are listed
            var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Empty(dirNames);

            // assert - files are listed
            var expectedFileNames = new[]
            {
                "data.bin",
                "file3.txt",
                "file4.txt"
            };
            var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Equal(expectedFileNames, fileNames);
        }

        [Fact]
        public async Task When_ListingEntriesOfTwoLevelsSubdirectoryDepthRecursive_Then_EntriesAreListed()
        {
            // arrange - paths
            var mbrDiskPath = $"mbr_{Guid.NewGuid()}.vhd";
            var dirPath = Path.Combine(mbrDiskPath, "mbr", "1", "dir1", "dir2");
            const bool recursive = true;

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create mdr disk
            await CreateMbrTestDisk(testCommandHelper, mbrDiskPath);

            // arrange - create fs dir command
            EntriesInfo entriesInfo = null;
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - dir2 contains 3 entries
            var entries = entriesInfo.Entries.ToList();
            Assert.Equal(3, entries.Count);

            // assert - no directories are listed
            var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Empty(dirNames);

            // assert - files are listed
            var expectedFileNames = new[]
            {
                "data.bin",
                "file3.txt",
                "file4.txt"
            };
            var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File)
                .OrderBy(x => x.Name.Length)
                .ThenBy(x => x.Name)
                .Select(x => x.Name)
                .ToList();
            Assert.Equal(expectedFileNames, fileNames);
        }

        private async Task CreateMbrTestDisk(TestCommandHelper testCommandHelper, string mbrDiskPath)
        {
            // disk sizes
            var mbrDiskSize = 100.MB();

            // add mbr disk media
            testCommandHelper.AddTestMedia(mbrDiskPath, mbrDiskSize);

            // mbr disk
            await CreateMbrFatFormattedDisk(testCommandHelper, mbrDiskPath, mbrDiskSize);

            var mediaResult = await testCommandHelper.GetWritableFileMedia(mbrDiskPath);
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

            using (fatFileSystem.OpenFile("file1.txt", FileMode.Create))
            {
            }

            fatFileSystem.CreateDirectory("dir1");

            using (fatFileSystem.OpenFile("dir1\\file2.txt", FileMode.Create))
            {
            }

            fatFileSystem.CreateDirectory("dir1\\dir2");

            using (fatFileSystem.OpenFile("dir1\\dir2\\file3.txt", FileMode.Create))
            {
            }

            using (fatFileSystem.OpenFile("dir1\\dir2\\file4.txt", FileMode.Create))
            {
            }

            using (fatFileSystem.OpenFile("dir1\\dir2\\data.bin", FileMode.Create))
            {
            }
        }
    }
}