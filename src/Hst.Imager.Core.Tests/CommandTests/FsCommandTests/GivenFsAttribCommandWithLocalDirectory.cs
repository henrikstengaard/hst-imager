using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Imager.Core.Caching;
using Hst.Imager.Core.Commands.FsCommands;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.UaeMetadatas;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsAttribCommandWithLocalDirectory
{
    [Fact]
    public async Task When_AddingHiddenToAFileOnWindows_Then_FileHasHiddenAttribute()
    {
        if (!System.OperatingSystem.IsWindows())
        {
            return;
        }

        // arrange - paths
        var mediaPath = Guid.NewGuid().ToString();
        var attribPath = Path.Combine(mediaPath, "dir1", "file1.txt");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create local directory with directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);

            // arrange - create fs attrib command
            var fsAttribCommand = new FsAttribCommand(testCommandHelper, [], attribPath, ["+h"], false);

            // act - execute fs attrib command
            var result = await fsAttribCommand.Execute(CancellationToken.None);

            // assert - result is success
            Assert.True(result.IsSuccess);

            // assert - file1.txt has hidden attribute
            Assert.True(File.GetAttributes(attribPath).HasFlag(FileAttributes.Hidden));
        }
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_RemovingExecuteFromAFileOnUnix_Then_FileDoesNotHaveExecutePermissions()
    {
        if (System.OperatingSystem.IsWindows())
        {
            return;
        }

        // arrange - paths
        var mediaPath = Guid.NewGuid().ToString();
        var attribPath = Path.Combine(mediaPath, "dir1", "file1.txt");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create local directory with directories and files with file mode 755 for file1.txt
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            File.SetUnixFileMode(attribPath, UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                             UnixFileMode.UserExecute | UnixFileMode.GroupRead |
                                             UnixFileMode.GroupExecute | UnixFileMode.OtherRead |
                                             UnixFileMode.OtherExecute);

            // arrange - create fs attrib command
            var fsAttribCommand = new FsAttribCommand(testCommandHelper, [], attribPath, ["-x"], false);

            // act - execute fs attrib command
            var result = await fsAttribCommand.Execute(CancellationToken.None);

            // assert - result is success
            Assert.True(result.IsSuccess);

            // assert - file1.txt has file mode 644
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead |
                         UnixFileMode.OtherRead, File.GetUnixFileMode(attribPath));
        }
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }

    [Theory]
    [InlineData(UaeMetadata.UaeFsDb)]
    [InlineData(UaeMetadata.UaeMetafile)]
    public async Task When_AddingScriptToAFileWithAmigaAttributes_Then_UaeMetadataHasScriptProtectionBit(
        UaeMetadata uaeMetadata)
    {
        // arrange - paths
        var mediaPath = Guid.NewGuid().ToString();
        var attribPath = Path.Combine(mediaPath, "dir1", "file1.txt");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create local directory with directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);

            // arrange - create fs attrib command with amiga attributes mode
            var fsAttribCommand = new FsAttribCommand(testCommandHelper, [], attribPath, ["+s", "-d"], false,
                AttributesMode.Amiga, uaeMetadata);

            // act - execute fs attrib command
            var result = await fsAttribCommand.Execute(CancellationToken.None);

            // assert - result is success
            Assert.True(result.IsSuccess);

            // assert - uae metadata for file1.txt has script, read, write and execute protection bits
            using var appCache = new MemoryAppCache();
            var uaeMetadataHelper = new UaeMetadataHelper(appCache);
            var uaeMetadataEntry = await uaeMetadataHelper.GetUaeMetadataEntry(uaeMetadata,
                PathHelper.Split(PathHelper.GetFullPath(attribPath)));
            Assert.NotNull(uaeMetadataEntry);
            Assert.True(uaeMetadataEntry.UaeMetadataExists);
            Assert.NotNull(uaeMetadataEntry.ProtectionBits);
            Assert.Equal(ProtectionBits.Script | ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable,
                ProtectionBitsConverter.ToProtectionBits(uaeMetadataEntry.ProtectionBits.Value));
        }
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_AddingScriptToAFileWithAmigaAttributesAndNoUaeMetadata_Then_ErrorIsReturned()
    {
        // arrange - paths
        var mediaPath = Guid.NewGuid().ToString();
        var attribPath = Path.Combine(mediaPath, "dir1", "file1.txt");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create local directory with directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);

            // arrange - create fs attrib command with amiga attributes mode and no uae metadata
            var fsAttribCommand = new FsAttribCommand(testCommandHelper, [], attribPath, ["+s"], false,
                AttributesMode.Amiga, UaeMetadata.None);

            // act - execute fs attrib command
            var result = await fsAttribCommand.Execute(CancellationToken.None);

            // assert - result is faulted
            Assert.True(result.IsFaulted);
        }
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }
}
