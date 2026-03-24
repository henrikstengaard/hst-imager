using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.UaeMetadatas;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsDelCommandWithRdb
{
    [Fact]
    public async Task When_DeleteDirectory_Then_DirectoryIsDeleted()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var deletePath = Path.Combine(mediaPath, "rdb", "1", "dir2");
        const bool recursive = false;
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - add media
        testCommandHelper.AddTestMedia(mediaPath, 0);

        // arrange - create rdb pfs3 formatted disk with directories and files
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);
        await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - create fs del command
        var fsDelCommand = new FsDelCommand(new NullLogger<FsDelCommand>(), testCommandHelper, [],
            deletePath, recursive, uaeMetadata);
        
        // act - execute fs del command
        var result = await fsDelCommand.Execute(CancellationToken.None);
        
        // assert - result is success
        Assert.True(result.IsSuccess);
        
        // assert - dir2 is deleted, only dir1 exists
        var entries = (await RdbTestHelper
            .GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 0, []))
            .OrderBy(x => x.Name)
            .ToList();
        Assert.Single(entries);
        string[] expectedEntries =
        [
            "dir1"
        ];
        Assert.Equal(expectedEntries, entries.Select(x => x.Name).ToArray());
    }
}