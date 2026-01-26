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
    public class GivenFsDirCommandWithLha : FsCommandTestBase
    {
        [Fact]
        public async Task When_ListingEntryInRootDirectory_Then_EntriesAreListed()
        {
            var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
            var tempLhaPath = $"{Guid.NewGuid()}.lha";
            const bool recursive = false;

            try
            {
                File.Copy(lhaPath, tempLhaPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(tempLhaPath, "test.txt"), recursive);
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
                DeletePaths(tempLhaPath);
            }
        }

        [Fact]
        public async Task When_ListingEntryInOneLevelSubdirectoryDepth_Then_EntriesAreListed()
        {
            var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
            var tempLhaPath = $"{Guid.NewGuid()}.lha";
            const bool recursive = false;

            try
            {
                File.Copy(lhaPath, tempLhaPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(tempLhaPath, "test1", "test1.txt"), recursive);
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
                    "test1.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(tempLhaPath);
            }
        }

        [Fact]
        public async Task When_ListingEntryInTwoLevelSubdirectoryDepth_Then_EntriesAreListed()
        {
            var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
            var tempLhaPath = $"{Guid.NewGuid()}.lha";
            const bool recursive = false;

            try
            {
                File.Copy(lhaPath, tempLhaPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(tempLhaPath, "test1", "test2", "test2.txt"), recursive);
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
                    "test2.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(tempLhaPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectory_Then_EntriesAreListed()
        {
            var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
            var tempLhaPath = $"{Guid.NewGuid()}.lha";
            const bool recursive = false;

            try
            {
                File.Copy(lhaPath, tempLhaPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    tempLhaPath, recursive);
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
                    "test1"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    "test.txt",
                    "test1.info"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(tempLhaPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectoryWithPattern_Then_EntriesAreListed()
        {
            var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
            var tempLhaPath = $"{Guid.NewGuid()}.lha";
            var pattern = "*.txt";
            const bool recursive = false;

            try
            {
                File.Copy(lhaPath, tempLhaPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(tempLhaPath, pattern), recursive);
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
                    "test.txt",
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(tempLhaPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectoryRecursive_Then_EntriesAreListed()
        {
            var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
            var tempLhaPath = $"{Guid.NewGuid()}.lha";
            const bool recursive = true;

            try
            {
                File.Copy(lhaPath, tempLhaPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    tempLhaPath, recursive);
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
                    "test1",
                    "test1\\test2"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    "test.txt",
                    "test1.info",
                    "test1\\test1.txt",
                    "test1\\test2.info",
                    "test1\\test2\\test2.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(tempLhaPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfRootDirectoryWithPatternRecursive_Then_EntriesAreListed()
        {
            var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
            var tempLhaPath = $"{Guid.NewGuid()}.lha";
            var pattern = "*.txt";
            const bool recursive = true;

            try
            {
                File.Copy(lhaPath, tempLhaPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(tempLhaPath, pattern), recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 5 entries are listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(5, entries.Count);

                // assert - directories are listed
                var expectedDirNames = new[]
                {
                    "test1",
                    "test1\\test2"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    "test.txt",
                    "test1\\test1.txt",
                    "test1\\test2\\test2.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(tempLhaPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepth_Then_EntriesAreListed()
        {
            var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
            var tempLhaPath = $"{Guid.NewGuid()}.lha";
            const bool recursive = false;

            try
            {
                File.Copy(lhaPath, tempLhaPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(tempLhaPath, "test1"), recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 3 entries is listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(3, entries.Count);

                // assert - directories are listed
                var expectedDirNames = new[]
                {
                    "test2"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    "test1.txt",
                    "test2.info"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(tempLhaPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepthWithPattern_Then_EntriesAreListed()
        {
            var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
            var tempLhaPath = $"{Guid.NewGuid()}.lha";
            var pattern = "*.txt";
            const bool recursive = false;

            try
            {
                File.Copy(lhaPath, tempLhaPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(tempLhaPath, "test1", pattern), recursive);
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
                    "test1.txt",
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(tempLhaPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepthRecursive_Then_EntriesAreListed()
        {
            var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
            var tempLhaPath = $"{Guid.NewGuid()}.lha";
            const bool recursive = true;

            try
            {
                File.Copy(lhaPath, tempLhaPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(tempLhaPath, "test1"), recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 4 entries is listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(4, entries.Count);

                // assert - directories are listed
                var expectedDirNames = new[]
                {
                    "test2"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    "test1.txt",
                    "test2.info",
                    "test2\\test2.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(tempLhaPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfOneLevelSubdirectoryDepthWithPatternRecursive_Then_EntriesAreListed()
        {
            var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
            var tempLhaPath = $"{Guid.NewGuid()}.lha";
            var pattern = "*.txt";
            const bool recursive = true;

            try
            {
                File.Copy(lhaPath, tempLhaPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(tempLhaPath, "test1", pattern), recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - 3 entries is listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(3, entries.Count);

                // assert - directory is listed
                var expectedDirNames = new[]
                {
                    "test2"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    "test1.txt",
                    "test2\\test2.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(tempLhaPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfTwoLevelsSubdirectoryDepth_Then_EntriesAreListed()
        {
            var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
            var tempLhaPath = $"{Guid.NewGuid()}.lha";
            const bool recursive = false;

            try
            {
                File.Copy(lhaPath, tempLhaPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(tempLhaPath, "test1", "test2"), recursive);
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
                    "test2.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(tempLhaPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesOfTwoLevelsSubdirectoryDepthRecursive_Then_EntriesAreListed()
        {
            var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
            var tempLhaPath = $"{Guid.NewGuid()}.lha";
            const bool recursive = true;

            try
            {
                File.Copy(lhaPath, tempLhaPath);

                using var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    Path.Combine(tempLhaPath, "test1", "test2"), recursive);
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
                    "test2.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(tempLhaPath);
            }
        }

        [Fact]
        public async Task When_ListingEntriesInSubDirectory_Then_EntriesAreListed()
        {
            var lhaPath = Path.Combine("TestData", "Lha", "dirs-files.lha");
            var tempLhaPath = $"{Guid.NewGuid()}.lha";
            var dirPath = Path.Combine(tempLhaPath, "dir1");
            const bool recursive = false;

            try
            {
                // arrange - copy lha file to temp path
                File.Copy(lhaPath, tempLhaPath);

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
                
                // assert - 2 entries are listed
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(2, entries.Count);

                // assert - 1 directory is listed
                var expectedDirNames = new[]
                {
                    "dir3"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);
                
                // assert - 1 file is listed
                var expectedFileNames = new[]
                {
                    "file1.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(tempLhaPath);
            }
        }
    }
}