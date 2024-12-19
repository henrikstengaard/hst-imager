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
    public class GivenFsExtractCommandWithRdb : FsCommandTestBase
    {
        [Theory]
        [InlineData("dh0")]
        [InlineData("1")]
        public async Task When_ExtractingLhaToRootDirectoryUsingPartitionNumberOrDeviceName_Then_EntriesAreExtracted(
    string partition)
        {
            var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
            var srcPath = $"src-{Guid.NewGuid()}.lha";
            var destPath = $"dest-{Guid.NewGuid()}.vhd";
            var extractPath = Path.Combine(destPath, "rdb", partition);
            const bool recursive = true;

            try
            {
                // arrange - test command helper
                var testCommandHelper = new TestCommandHelper();

                // arrange - source lha file
                File.Copy(lhaPath, srcPath, true);

                // arrange - destination rdb disk
                await testCommandHelper.AddTestMedia(destPath);
                await CreatePfs3FormattedDisk(testCommandHelper, destPath);

                // arrange - create fs extract command
                var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(), srcPath, extractPath, recursive, true, true);

                // act - execute fs extract command
                var result = await fsExtractCommand.Execute(CancellationToken.None);
                Assert.True(result.IsSuccess);

                // arrange - entries info with result from fs dir command
                EntriesInfo entriesInfo = null;

                // arrange - create fs dir command
                var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(),
                    extractPath, recursive);
                fsDirCommand.EntriesRead += (_, e) =>
                {
                    entriesInfo = e.EntriesInfo;
                };

                // act - execute fs dir command
                result = await fsDirCommand.Execute(CancellationToken.None);
                Assert.True(result.IsSuccess);

                // assert - 7 entries are listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(7, entries.Count);

                // assert - directories are listed
                var expectedDirNames = new[]
                {
                    "test1",
                    "test1/test2"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

                // assert - files are listed
                var expectedFileNames = new[]
                {
                    "test1/test2/test2.txt",
                    "test1/test1.txt",
                    "test1/test2.info",
                    "test.txt",
                    "test1.info"
                };
                var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
                Assert.Equal(expectedFileNames, fileNames);
            }
            finally
            {
                DeletePaths(srcPath, destPath);
            }
        }
    }
}
