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
    public class GivenFsDirCommandWithZip : FsCommandTestBase
    {
        [Fact]
        public async Task When_ListingEntryInRootDirectory_Then_EntriesAreListed()
        {
            var zipPath = $"{Guid.NewGuid()}.zip";

            try
            {
                await CreateZipWithDirectoriesAndFiles(zipPath);

                var fakeCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), fakeCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(zipPath, "file1.txt"), false);
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
                DeletePaths(zipPath);
            }
        }

        [Fact]
        public async Task When_ListingEntryInOneLevelSubdirectoryDepth_Then_EntriesAreListed()
        {
            var zipPath = $"{Guid.NewGuid()}.zip";

            try
            {
                await CreateZipWithDirectoriesAndFiles(zipPath);

                var fakeCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), fakeCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(zipPath, "dir1", "test.txt"), false);
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
                DeletePaths(zipPath);
            }
        }

        [Fact]
        public async Task When_ListingEntryInTwoLevelSubdirectoryDepth_Then_EntriesAreListed()
        {
            var zipPath = $"{Guid.NewGuid()}.zip";

            try
            {
                await CreateZipWithDirectoriesAndFiles(zipPath);

                var fakeCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), fakeCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(zipPath, "dir1", "dir2", "file4.txt"), false);
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
                DeletePaths(zipPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectory_Then_EntriesAreListed()
        {
            var zipPath = $"{Guid.NewGuid()}.zip";

            try
            {
                await CreateZipWithDirectoriesAndFiles(zipPath);

                var fakeCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), fakeCommandHelper,
                    new List<IPhysicalDrive>(),
                    zipPath, false);
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
                DeletePaths(zipPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectoryWithPattern_Then_EntriesAreListed()
        {
            var zipPath = $"{Guid.NewGuid()}.zip";
            var pattern = "*.txt";
            const bool recursive = false;

            try
            {
                await CreateZipWithDirectoriesAndFiles(zipPath);

                var fakeCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), fakeCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(zipPath, pattern), recursive);
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
                DeletePaths(zipPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectoryRecursive_Then_EntriesAreListed()
        {
            const bool recursive = true;
            var zipPath = $"{Guid.NewGuid()}.zip";

            try
            {
                await CreateZipWithDirectoriesAndFiles(zipPath);

                var fakeCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), fakeCommandHelper,
                    new List<IPhysicalDrive>(),
                    zipPath, recursive);
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
                var expectedDirNames = new[] { "dir1", "dir1/dir2" };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[] { "dir1/dir2/file4.txt", "dir1/file3.txt", "dir1/test.txt", "file1.txt", "file2.txt" };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(zipPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectoryWithPatternRecursive_Then_EntriesAreListed()
        {
            var zipPath = $"{Guid.NewGuid()}.zip";
            var pattern = "*.txt";
            const bool recursive = true;

            try
            {
                await CreateZipWithDirectoriesAndFiles(zipPath);

                var fakeCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), fakeCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(zipPath, pattern), recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 7 entries are listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(7, entries.Count);

                // assert - directories are listed
                var expectedDirNames = new[]
                {
                    "dir1",
                    "dir1/dir2"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[] {
                    "dir1/dir2/file4.txt",
                    "dir1/file3.txt",
                    "dir1/test.txt",
                    "file1.txt",
                    "file2.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(zipPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepth_Then_EntriesAreListed()
        {
            const bool recursive = false;
            var zipPath = $"{Guid.NewGuid()}.zip";

            try
            {
                await CreateZipWithDirectoriesAndFiles(zipPath);

                var fakeCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), fakeCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(zipPath, "dir1"), recursive);
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
                DeletePaths(zipPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepthWithPattern_Then_EntriesAreListed()
        {
            var zipPath = $"{Guid.NewGuid()}.zip";
            var pattern = "*.txt";
            const bool recursive = false;

            try
            {
                await CreateZipWithDirectoriesAndFiles(zipPath);

                var fakeCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), fakeCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(zipPath, "dir1", pattern), recursive);
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
                var expectedFileNames = new[] { "file3.txt", "test.txt" };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(zipPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepthRecursive_Then_EntriesAreListed()
        {
            const bool recursive = true;
            var zipPath = $"{Guid.NewGuid()}.zip";

            try
            {
                await CreateZipWithDirectoriesAndFiles(zipPath);

                var fakeCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), fakeCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(zipPath, "dir1"), recursive);
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

                // assert - directories are listed
                var expectedDirNames = new[] { "dir2" };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[] { "dir2/file4.txt", "file3.txt", "test.txt" };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(zipPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepthWithPatternRecursive_Then_EntriesAreListed()
        {
            var zipPath = $"{Guid.NewGuid()}.zip";
            var pattern = "*.txt";
            const bool recursive = true;

            try
            {
                await CreateZipWithDirectoriesAndFiles(zipPath);

                var fakeCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), fakeCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(zipPath, "dir1", pattern), recursive);
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
                    "dir2/file4.txt",
                    "file3.txt",
                    "test.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(zipPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfTwoLevelsSubdirectoryDepth_Then_EntriesAreListed()
        {
            const bool recursive = false;
            var zipPath = $"{Guid.NewGuid()}.zip";

            try
            {
                await CreateZipWithDirectoriesAndFiles(zipPath);

                var fakeCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), fakeCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(zipPath, "dir1", "dir2"), recursive);
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

                // assert - file4.txt file is listed
                var file4TxtEntry = entriesInfo.Entries.FirstOrDefault(x => x.Name == "file4.txt" && x.Type == Models.FileSystems.EntryType.File);
                Assert.NotNull(file4TxtEntry);
            }
            finally
            {
                DeletePaths(zipPath);
            }
        }
        
        [Fact]
        public async Task When_ListingEntriesOfTwoLevelsSubdirectoryDepthRecursive_Then_EntriesAreListed()
        {
            const bool recursive = true;
            var zipPath = $"{Guid.NewGuid()}.zip";

            try
            {
                await CreateZipWithDirectoriesAndFiles(zipPath);

                var fakeCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), fakeCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(zipPath, "dir1", "dir2"), recursive);
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
                var expectedFileNames = new[] { "file4.txt" };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(zipPath);
            }
        }
    }
}