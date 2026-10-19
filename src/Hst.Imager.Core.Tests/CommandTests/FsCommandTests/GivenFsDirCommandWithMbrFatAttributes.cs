using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.FileSystems;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsDirCommandWithMbrFatAttributes
{
    [Fact]
    public async Task When_ListingDirectoryWithAttributesSet_Then_EntriesHaveAttributesFromFileSystem()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var dirPath = Path.Combine(mediaPath, "mbr", "1", "dir1");

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add media
        testCommandHelper.AddTestMedia(mediaPath, 0);

        // arrange - create mbr fat formatted disk with directories and files
        await TestHelper.CreateMbrFatFormattedDisk(testCommandHelper, mediaPath);
        await MbrTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - set read only and hidden attributes for file1.txt and system attribute for dir3
        var (media, fileSystem) = await MbrTestHelper.MountFileSystem(testCommandHelper, mediaPath, 0, true);
        using (media)
        {
            fileSystem.SetAttributes("dir1\\file1.txt", FileAttributes.ReadOnly | FileAttributes.Hidden);
            fileSystem.SetAttributes("dir1\\dir3", FileAttributes.Directory | FileAttributes.System);
        }

        // arrange - create fs dir command
        EntriesInfo entriesInfo = null;
        var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), dirPath, false);
        fsDirCommand.EntriesRead += (_, e) =>
        {
            entriesInfo = e.EntriesInfo;
        };

        // act - execute fs dir command
        var result = await fsDirCommand.Execute(CancellationToken.None);

        // assert - result is success
        Assert.True(result.IsSuccess);
        Assert.NotNull(entriesInfo);
        var entries = entriesInfo.Entries.ToList();

        // assert - file1.txt has read only and hidden attributes
        var file1Entry = entries.FirstOrDefault(x => x.Type == Models.FileSystems.EntryType.File &&
                                                     x.Name.Equals("file1.txt", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(file1Entry);
        Assert.Equal(((uint)(FileAttributes.ReadOnly | FileAttributes.Hidden)).ToString(),
            file1Entry.Properties[Constants.EntryPropertyNames.WindowsAttributes]);
        Assert.Equal("-RH-", AttributesFormatter.FormatAttributes(file1Entry, AttributesMode.Windows));

        // assert - dir3 has system attribute
        var dir3Entry = entries.FirstOrDefault(x => x.Type == Models.FileSystems.EntryType.Dir &&
                                                    x.Name.Equals("dir3", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(dir3Entry);
        Assert.Equal(((uint)(FileAttributes.Directory | FileAttributes.System)).ToString(),
            dir3Entry.Properties[Constants.EntryPropertyNames.WindowsAttributes]);
        Assert.Equal("---S", AttributesFormatter.FormatAttributes(dir3Entry, AttributesMode.Windows));
    }
}
