using System.IO;
using System;
using System.Threading.Tasks;
using Xunit;
using Hst.Imager.Core.Commands;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Linq;

namespace Hst.Imager.Core.Tests.CommandTests.RdbCommandTests
{
    public class GivenFsDirCommandWithRdb : FsCommandTestBase
    {
        [Theory]
        [InlineData("dh0")]
        [InlineData("1")]
        public async Task When_ListingEntriesInRootDirectoryUsingPartitionNumberOrDeviceName_Then_EntriesAreListed(
            string partition)
        {
            var rdbPath = $"rdb-{Guid.NewGuid()}.vhd";
            var dirPath = Path.Combine(rdbPath, "rdb", partition);
            const bool recursive = false;

            try
            {
                // arrange - test command helper
                var testCommandHelper = new TestCommandHelper();
                await testCommandHelper.AddTestMedia(rdbPath);

                // arrange - rdb disk with directories and files
                await CreatePfs3FormattedDisk(testCommandHelper, rdbPath);
                await CreatePfs3DirectoriesAndFiles(testCommandHelper, rdbPath);

                // arrange - entries info with result from fs dir command
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
                var result = await fsDirCommand.Execute(CancellationToken.None);
                Assert.True(result.IsSuccess);

                // assert - 2 entries are listed
                Assert.NotNull(entriesInfo);
                var entries = entriesInfo.Entries.ToList();
                Assert.Equal(2, entries.Count);

                // assert - directory is listed
                var expectedDirNames = new[]
                {
                    "dir1"
                };
                var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
                Assert.Equal(expectedDirNames, dirNames);

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
                DeletePaths(rdbPath);
            }
        }

        private async Task CreatePfs3DirectoriesAndFiles(TestCommandHelper testCommandHelper, string path)
        {
            var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }

            using var media = mediaResult.Value;
            var stream = media.Stream;

            await using var pfs3Volume = await MountPfs3Volume(stream);
            await pfs3Volume.CreateFile("file1.txt");
            await pfs3Volume.CreateDirectory("dir1");
            await pfs3Volume.ChangeDirectory("dir1");
            await pfs3Volume.CreateFile("file2.txt");
        }
    }
}
