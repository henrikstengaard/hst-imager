using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands.FsCommands;
using Hst.Imager.Core.Models;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsAttribCommandWithMbr
{
    [Fact]
    public async Task When_AddingReadOnlyAndHiddenToAFile_Then_FileHasReadOnlyAndHiddenAttributes()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var attribPath = Path.Combine(mediaPath, "mbr", "1", "dir1", "file1.txt");

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add media
        testCommandHelper.AddTestMedia(mediaPath, 0);

        // arrange - create mbr fat formatted disk with directories and files
        await TestHelper.CreateMbrFatFormattedDisk(testCommandHelper, mediaPath);
        await MbrTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - create fs attrib command
        var fsAttribCommand = new FsAttribCommand(testCommandHelper, [], attribPath, ["+rh", "-a"], false);

        // act - execute fs attrib command
        var result = await fsAttribCommand.Execute(CancellationToken.None);

        // assert - result is success
        Assert.True(result.IsSuccess);

        // arrange - clear active medias to ensure changes to media is flushed
        testCommandHelper.ClearActiveMedias();

        // assert - file1.txt has read only and hidden attributes and not archive attribute
        var (media, fileSystem) = await MbrTestHelper.MountFileSystem(testCommandHelper, mediaPath, 0);
        using (media)
        {
            var attributes = fileSystem.GetAttributes("dir1\\file1.txt");
            Assert.True(attributes.HasFlag(FileAttributes.ReadOnly));
            Assert.True(attributes.HasFlag(FileAttributes.Hidden));
            Assert.False(attributes.HasFlag(FileAttributes.Archive));
        }
    }

    [Fact]
    public async Task When_AddingSystemToADirectory_Then_DirectoryHasSystemAttribute()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var attribPath = Path.Combine(mediaPath, "mbr", "1", "dir1", "dir3");

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add media
        testCommandHelper.AddTestMedia(mediaPath, 0);

        // arrange - create mbr fat formatted disk with directories and files
        await TestHelper.CreateMbrFatFormattedDisk(testCommandHelper, mediaPath);
        await MbrTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - create fs attrib command
        var fsAttribCommand = new FsAttribCommand(testCommandHelper, [], attribPath, ["+s"], false);

        // act - execute fs attrib command
        var result = await fsAttribCommand.Execute(CancellationToken.None);

        // assert - result is success
        Assert.True(result.IsSuccess);

        // arrange - clear active medias to ensure changes to media is flushed
        testCommandHelper.ClearActiveMedias();

        // assert - dir3 is a directory with system attribute
        var (media, fileSystem) = await MbrTestHelper.MountFileSystem(testCommandHelper, mediaPath, 0);
        using (media)
        {
            var attributes = fileSystem.GetAttributes("dir1\\dir3");
            Assert.True(attributes.HasFlag(FileAttributes.Directory));
            Assert.True(attributes.HasFlag(FileAttributes.System));
        }
    }

    [Fact]
    public async Task When_SettingAmigaProtectionBits_Then_ErrorIsReturned()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var attribPath = Path.Combine(mediaPath, "mbr", "1", "dir1", "file1.txt");

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - add media
        testCommandHelper.AddTestMedia(mediaPath, 0);

        // arrange - create mbr fat formatted disk with directories and files
        await TestHelper.CreateMbrFatFormattedDisk(testCommandHelper, mediaPath);
        await MbrTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - create fs attrib command with amiga attributes mode
        var fsAttribCommand = new FsAttribCommand(testCommandHelper, [], attribPath, ["+s"], false,
            AttributesMode.Amiga);

        // act - execute fs attrib command
        var result = await fsAttribCommand.Execute(CancellationToken.None);

        // assert - result is faulted
        Assert.True(result.IsFaulted);
    }
}
