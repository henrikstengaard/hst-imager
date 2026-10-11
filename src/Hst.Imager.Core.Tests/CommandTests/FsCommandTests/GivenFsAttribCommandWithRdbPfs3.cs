using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Imager.Core.Commands.FsCommands;
using Hst.Imager.Core.Models;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsAttribCommandWithRdbPfs3
{
    [Fact]
    public async Task When_AddingScriptToAFile_Then_FileHasScriptProtectionBit()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var attribPath = Path.Combine(mediaPath, "rdb", "1", "dir1", "file1.txt");

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add media
        testCommandHelper.AddTestMedia(mediaPath, 0);

        // arrange - create rdb pfs3 formatted disk with directories and files
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);
        await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - create fs attrib command
        var fsAttribCommand = new FsAttribCommand(testCommandHelper, [], attribPath, ["+s"], false);

        // act - execute fs attrib command
        var result = await fsAttribCommand.Execute(CancellationToken.None);

        // assert - result is success
        Assert.True(result.IsSuccess);

        // arrange - clear active medias to ensure changes to media is flushed
        testCommandHelper.ClearActiveMedias();

        // assert - file1.txt has script and read, write, execute and delete protection bits
        var entries = (await RdbTestHelper
                .GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 0, ["dir1"]))
            .ToList();
        var file1Entry = entries.First(x => x.Name == "file1.txt");
        Assert.Equal(ProtectionBits.Script | ProtectionBits.Read | ProtectionBits.Write |
                     ProtectionBits.Executable | ProtectionBits.Delete, file1Entry.ProtectionBits);

        // assert - dir3 protection bits are unchanged
        var dir3Entry = entries.First(x => x.Name == "dir3");
        Assert.False(dir3Entry.ProtectionBits.HasFlag(ProtectionBits.Script));
    }

    [Fact]
    public async Task When_RemovingDeleteFromADirectory_Then_DirectoryDoesNotHaveDeleteProtectionBit()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var attribPath = Path.Combine(mediaPath, "rdb", "1", "dir1");

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add media
        testCommandHelper.AddTestMedia(mediaPath, 0);

        // arrange - create rdb pfs3 formatted disk with directories and files
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);
        await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - create fs attrib command
        var fsAttribCommand = new FsAttribCommand(testCommandHelper, [], attribPath, ["-d"], false);

        // act - execute fs attrib command
        var result = await fsAttribCommand.Execute(CancellationToken.None);

        // assert - result is success
        Assert.True(result.IsSuccess);

        // arrange - clear active medias to ensure changes to media is flushed
        testCommandHelper.ClearActiveMedias();

        // assert - dir1 doesn't have delete protection bit
        var rootEntries = (await RdbTestHelper
                .GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 0, []))
            .ToList();
        var dir1Entry = rootEntries.First(x => x.Name == "dir1");
        Assert.False(dir1Entry.ProtectionBits.HasFlag(ProtectionBits.Delete));

        // assert - entries in dir1 are unchanged
        var dir1Entries = (await RdbTestHelper
                .GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 0, ["dir1"]))
            .ToList();
        Assert.All(dir1Entries, x => Assert.True(x.ProtectionBits.HasFlag(ProtectionBits.Delete)));
    }

    [Fact]
    public async Task When_SettingExactProtectionBitsWithWildcard_Then_MatchingEntriesHaveExactProtectionBits()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var attribPath = Path.Combine(mediaPath, "rdb", "1", "dir1", "*");

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add media
        testCommandHelper.AddTestMedia(mediaPath, 0);

        // arrange - create rdb pfs3 formatted disk with directories and files
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);
        await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - create fs attrib command
        var fsAttribCommand = new FsAttribCommand(testCommandHelper, [], attribPath, ["re"], false);

        // act - execute fs attrib command
        var result = await fsAttribCommand.Execute(CancellationToken.None);

        // assert - result is success
        Assert.True(result.IsSuccess);

        // arrange - clear active medias to ensure changes to media is flushed
        testCommandHelper.ClearActiveMedias();

        // assert - dir3 and file1.txt only have read and execute protection bits
        var dir1Entries = (await RdbTestHelper
                .GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 0, ["dir1"]))
            .OrderBy(x => x.Name)
            .ToList();
        Assert.Equal(["dir3", "file1.txt"], dir1Entries.Select(x => x.Name).ToArray());
        Assert.All(dir1Entries, x => Assert.Equal(ProtectionBits.Read | ProtectionBits.Executable, x.ProtectionBits));

        // assert - dir1 is unchanged
        var rootEntries = (await RdbTestHelper
                .GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 0, []))
            .ToList();
        var dir1Entry = rootEntries.First(x => x.Name == "dir1");
        Assert.True(dir1Entry.ProtectionBits.HasFlag(ProtectionBits.Write));
    }

    [Fact]
    public async Task When_RemovingWriteRecursively_Then_DirectoryAndAllEntriesDoNotHaveWriteProtectionBit()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var attribPath = Path.Combine(mediaPath, "rdb", "1", "dir1");

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add media
        testCommandHelper.AddTestMedia(mediaPath, 0);

        // arrange - create rdb pfs3 formatted disk with directories and files
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);
        await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - create fs attrib command
        var fsAttribCommand = new FsAttribCommand(testCommandHelper, [], attribPath, ["-w"], true);

        // act - execute fs attrib command
        var result = await fsAttribCommand.Execute(CancellationToken.None);

        // assert - result is success
        Assert.True(result.IsSuccess);

        // arrange - clear active medias to ensure changes to media is flushed
        testCommandHelper.ClearActiveMedias();

        // assert - dir1 doesn't have write protection bit and dir2 is unchanged
        var rootEntries = (await RdbTestHelper
                .GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 0, []))
            .ToList();
        Assert.False(rootEntries.First(x => x.Name == "dir1").ProtectionBits.HasFlag(ProtectionBits.Write));
        Assert.True(rootEntries.First(x => x.Name == "dir2").ProtectionBits.HasFlag(ProtectionBits.Write));

        // assert - dir3 and file1.txt don't have write protection bit
        var dir1Entries = (await RdbTestHelper
                .GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 0, ["dir1"]))
            .ToList();
        Assert.Equal(2, dir1Entries.Count);
        Assert.All(dir1Entries, x => Assert.False(x.ProtectionBits.HasFlag(ProtectionBits.Write)));
    }

    [Fact]
    public async Task When_SettingAttributesForPartitionRootWithoutRecursive_Then_ErrorIsReturned()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var attribPath = Path.Combine(mediaPath, "rdb", "1");

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add media
        testCommandHelper.AddTestMedia(mediaPath, 0);

        // arrange - create rdb pfs3 formatted disk with directories and files
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);
        await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - create fs attrib command
        var fsAttribCommand = new FsAttribCommand(testCommandHelper, [], attribPath, ["+s"], false);

        // act - execute fs attrib command
        var result = await fsAttribCommand.Execute(CancellationToken.None);

        // assert - result is faulted
        Assert.True(result.IsFaulted);
    }

    [Fact]
    public async Task When_SettingWindowsAttributes_Then_ErrorIsReturned()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var attribPath = Path.Combine(mediaPath, "rdb", "1", "dir1", "file1.txt");

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add media
        testCommandHelper.AddTestMedia(mediaPath, 0);

        // arrange - create rdb pfs3 formatted disk with directories and files
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);
        await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - create fs attrib command with windows attributes mode
        var fsAttribCommand = new FsAttribCommand(testCommandHelper, [], attribPath, ["+h"], false,
            AttributesMode.Windows);

        // act - execute fs attrib command
        var result = await fsAttribCommand.Execute(CancellationToken.None);

        // assert - result is faulted
        Assert.True(result.IsFaulted);
    }
}
