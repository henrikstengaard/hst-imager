using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.RdbCommandTests
{
    public class GivenFsCopyCommandWithRdb : FsCommandTestBase
    {
        [Theory]
        [InlineData("dh0")]
        [InlineData("1")]
        public async Task When_CopyingEntriesToRootDirectoryUsingPartitionNumberOrDeviceName_Then_EntriesAreCopied(
    string partition)
        {
            var srcPath = $"src-{Guid.NewGuid()}";
            var destPath = $"rdb-{Guid.NewGuid()}.vhd";
            var copyPath = Path.Combine(destPath, "rdb", partition);
            const bool recursive = true;

            try
            {
                // arrange - test command helper
                var testCommandHelper = new TestCommandHelper();

                // arrange - source directory and files
                await CreateDirectoriesAndFiles(srcPath);

                // arrange - destination rdb disk
                await testCommandHelper.AddTestMedia(destPath);
                await CreatePfs3FormattedDisk(testCommandHelper, destPath);

                // arrange - create fs copy command
                var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(), srcPath, copyPath, recursive, true, true);

                // act - execute fs copy command
                var result = await fsCopyCommand.Execute(CancellationToken.None);
                Assert.True(result.IsSuccess);

                // arrange - entries info with result from fs dir command
                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    copyPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                result = await fsDirCommand.Execute(CancellationToken.None);
                Assert.True(result.IsSuccess);

                // assert - 3 entries are listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(3, entries.Count);

                // assert - directory is listed
                var expectedDirNames = new[]
                {
                    "dir1"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    "dir1/file2.txt",
                    "file1.txt"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(srcPath, destPath);
            }
        }

        private static async Task CreateDirectoriesAndFiles(string path)
        {
            var file1Txt = Path.Combine(path, "file1.txt");
            var dir1 = Path.Combine(path, "dir1");
            var file2Txt = Path.Combine(dir1, "file2.txt");

            Directory.CreateDirectory(dir1);
            await File.WriteAllTextAsync(file1Txt, string.Empty);
            await File.WriteAllTextAsync(file2Txt, string.Empty);
        }
    }
}
