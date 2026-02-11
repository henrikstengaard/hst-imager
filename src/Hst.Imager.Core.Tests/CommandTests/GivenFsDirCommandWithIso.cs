using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests
{
    public class GivenFsDirCommandWithIso : FsCommandTestBase
    {
        [Fact]
        public async Task When_ListingEntryInRootDirectory_Then_EntriesAreListed()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var dirPath = Path.Combine(isoPath, "file1.txt");
            const bool recursive = false;

            try
            {
                CreateIso9660WithDirectoriesAndFiles(isoPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    dirPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 1 entry is listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Single(entries);

                // assert - file is listed
                var expectedFileNames = new[]
                {
                    "file1.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(isoPath);
            }
        }

        [Fact]
        public async Task When_ListingEntryInOneLevelSubdirectoryDepth_Then_EntriesAreListed()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var dirPath = Path.Combine(isoPath, "dir1", "test.txt");
            const bool recursive = false;

            try
            {
                CreateIso9660WithDirectoriesAndFiles(isoPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    dirPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 1 entry is listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Single(entries);

                // assert - file is listed
                var expectedFileNames = new[]
                {
                    "test.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(isoPath);
            }
        }

        [Fact]
        public async Task When_ListingEntryInTwoLevelSubdirectoryDepth_Then_EntriesAreListed()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var dirPath = Path.Combine(isoPath, "dir1", "dir2", "file4.txt");
            const bool recursive = false;

            try
            {
                CreateIso9660WithDirectoriesAndFiles(isoPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    dirPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 1 entry is listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Single(entries);

                // assert - file is listed
                var expectedFileNames = new[]
                {
                    "file4.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(isoPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectory_Then_EntriesAreListed()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var dirPath = isoPath;
            const bool recursive = false;

            try
            {
                CreateIso9660WithDirectoriesAndFiles(isoPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    dirPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 3 entries are listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(3, entries.Count);

                // assert - directories are listed
                var expectedDirNames = new[] { "dir1" };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[] { "file1.txt", "file2.txt" };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(isoPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectoryWithPattern_Then_EntriesAreListed()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var pattern = "*.txt";
            var dirPath = Path.Combine(isoPath, pattern);
            const bool recursive = false;

            try
            {
                CreateIso9660WithDirectoriesAndFiles(isoPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    dirPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 2 entries are listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(2, entries.Count);

                // assert - files are listed
                var expectedFileNames = new[] { "file1.txt", "file2.txt" };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(isoPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectoryRecursive_Then_EntriesAreListed()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var dirPath = isoPath;
            const bool recursive = true;

            try
            {
                CreateIso9660WithDirectoriesAndFiles(isoPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    dirPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 7 entries are listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.OrderBy(x => x.Name).ToList();
                Assert.Equal(7, entries.Count);

                // assert - directories are listed
                var expectedDirNames = new[]
                {
                    "dir1",
                    Path.Combine("dir1", "dir2")
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    Path.Combine("dir1", "dir2", "file4.txt"),
                    Path.Combine("dir1", "file3.txt"),
                    Path.Combine("dir1", "test.txt"),
                    "file1.txt",
                    "file2.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(isoPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectoryWithPatternRecursive_Then_EntriesAreListed()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var pattern = "*.txt";
            var dirPath = Path.Combine(isoPath, pattern);
            const bool recursive = true;

            try
            {
                CreateIso9660WithDirectoriesAndFiles(isoPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    dirPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 7 entries are listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.OrderBy(x => x.Name).ToList();
                Assert.Equal(7, entries.Count);

                // assert - directories are listed
                var expectedDirNames = new[]
                {
                    "dir1",
                    Path.Combine("dir1", "dir2")
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    Path.Combine("dir1", "dir2", "file4.txt"),
                    Path.Combine("dir1", "file3.txt"),
                    Path.Combine("dir1", "test.txt"),
                    "file1.txt",
                    "file2.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(isoPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepth_Then_EntriesAreListed()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var dirPath = Path.Combine(isoPath, "dir1");
            const bool recursive = false;

            try
            {
                CreateIso9660WithDirectoriesAndFiles(isoPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    dirPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 3 entries are listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(3, entries.Count);

                // assert - directory is listed
                var expectedDirNames = new[]
                {
                    "dir2"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    "file3.txt",
                    "test.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(isoPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepthWithPattern_Then_EntriesAreListed()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var pattern = "*.txt";
            var dirPath = Path.Combine(isoPath, "dir1", pattern);
            const bool recursive = false;

            try
            {
                CreateIso9660WithDirectoriesAndFiles(isoPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    dirPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 2 entries are listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(2, entries.Count);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    "file3.txt",
                    "test.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(isoPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepthRecursive_Then_EntriesAreListed()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var dirPath = Path.Combine(isoPath, "dir1");
            const bool recursive = true;

            try
            {
                CreateIso9660WithDirectoriesAndFiles(isoPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    dirPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 4 entries are listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.OrderBy(x => x.Name).ToList();
                Assert.Equal(4, entries.Count);

                // assert - directories are listed
                var expectedDirNames = new[]
                {
                    "dir2"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    Path.Combine("dir2", "file4.txt"),
                    "file3.txt",
                    "test.txt",
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(isoPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepthWithPatternRecursive_Then_EntriesAreListed()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var pattern = "*.txt";
            var dirPath = Path.Combine(isoPath, "dir1", pattern);
            const bool recursive = true;

            try
            {
                CreateIso9660WithDirectoriesAndFiles(isoPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    dirPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 4 entries are listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(4, entries.Count);

                // assert - directory is listed
                var expectedDirNames = new[]
                {
                    "dir2"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    Path.Combine("dir2", "file4.txt"),
                    "file3.txt",
                    "test.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(isoPath);
            }

        }

        [Fact]
        public async Task When_ListingEntriesOfTwoLevelsSubdirectoryDepth_Then_EntriesAreListed()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var dirPath = Path.Combine(isoPath, "dir1", "dir2");
            const bool recursive = false;

            try
            {
                CreateIso9660WithDirectoriesAndFiles(isoPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    dirPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 1 entry is listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Single(entries);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    "file4.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(isoPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfTwoLevelsSubdirectoryDepthRecursive_Then_EntriesAreListed()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var dirPath = Path.Combine(isoPath, "dir1", "dir2");
            const bool recursive = true;

            try
            {
                CreateIso9660WithDirectoriesAndFiles(isoPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    dirPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 1 entry is listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Single(entries);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    "file4.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(isoPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesInSubDirectory_Then_EntriesAreListed()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var dirPath = Path.Combine(isoPath, "dir1");
            const bool recursive = false;

            try
            {
                // arrange - create iso with directories and files
                CreateIso9660WithDirectoriesAndFiles(isoPath);

                // arrange - test command helper
                using var testCommandHelper = new TestCommandHelper();
                
                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(), dirPath, recursive);
                EntriesInfo entriesInfo = null;
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(CancellationToken.None);

                // assert - success and entries info is not null
                Assert.True(result.IsSuccess);
                Assert.NotNull(entriesInfo);
                
                // assert - 3 entries are listed
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(3, entries.Count);

                // assert - 1 directory is listed
                var expectedDirNames = new[]
                {
                    "dir2"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);
                
                // assert - 1 file is listed
                var expectedFileNames = new[]
                {
                    "file3.txt",
                    "test.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(isoPath);
            }
        }
    }
}