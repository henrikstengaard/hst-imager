using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Imager.Core.Commands.FsCommands;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsAttribCommandWithAdf
{
    [Fact]
    public async Task When_AddingScriptAndRemovingDeleteFromAFile_Then_FileProtectionBitsAreChanged()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var attribPath = Path.Combine(mediaPath, "dir1", "file1.txt");

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - create adf formatted disk with directories and files
        await AdfTestHelper.CreateFormattedAdfDisk(testCommandHelper, mediaPath);
        await AdfTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

        // arrange - create fs attrib command
        var fsAttribCommand = new FsAttribCommand(testCommandHelper, [], attribPath, ["+s", "-d"], false);

        // act - execute fs attrib command
        var result = await fsAttribCommand.Execute(CancellationToken.None);

        // assert - result is success
        Assert.True(result.IsSuccess);

        // arrange - clear active medias to ensure changes to media is flushed
        testCommandHelper.ClearActiveMedias();

        // assert - file1.txt has script, read, write and execute protection bits
        var entries = (await AdfTestHelper
                .GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, ["dir1"]))
            .ToList();
        var file1Entry = entries.First(x => x.Name == "file1.txt");
        Assert.Equal(ProtectionBits.Script | ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable,
            file1Entry.ProtectionBits);
    }
}
