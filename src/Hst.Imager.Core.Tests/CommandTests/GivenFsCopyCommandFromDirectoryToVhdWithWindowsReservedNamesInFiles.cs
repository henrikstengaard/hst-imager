using Hst.Imager.Core.UaeMetadatas;

namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class GivenFsCopyCommandFromDirectoryToVhdWithWindowsReservedNamesInFiles : FsCommandTestBase
{
    [Fact]
    public async Task When_CopyingToLocalDirectoryAndNoMetaData_Then_FilesAreCopiedWithoutMetaData()
    {
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}";
        var srcCopyPath = Path.Combine(srcPath, "rdb", "dh0");
        const UaeMetadata uaeMetadata = UaeMetadata.None;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create src amiga rdb image
            await CreatePfs3FormattedDisk(testCommandHelper, srcPath);
            await CreateAmigaFilesWithReservedWindowsFilename(testCommandHelper, srcPath);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcCopyPath, destPath, true, false, true, uaeMetadata: uaeMetadata);

            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - files are copied to dest path
            var expectedFiles = OperatingSystem.IsWindows()
                ? new[]
                {
                    Path.Combine(destPath, "_AUX"),
                    Path.Combine(destPath, "_AUX.info")
                }
                : new[]
                {
                    Path.Combine(destPath, "AUX"),
                    Path.Combine(destPath, "AUX.info")
                };
            var actualFiles = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    [Fact]
    public async Task When_CopyingToLocalDirectoryAndUaeFsDbMetaData_Then_FilesAreCopiedWithMetaData()
    {
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}";
        var srcCopyPath = Path.Combine(srcPath, "rdb", "dh0");
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create src amiga rdb image
            await CreatePfs3FormattedDisk(testCommandHelper, srcPath);
            await CreateAmigaFilesWithReservedWindowsFilename(testCommandHelper, srcPath);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcCopyPath, destPath, true, false, true, uaeMetadata: uaeMetadata);

            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - files are copied to dest path
            var expectedFiles = OperatingSystem.IsWindows()
                ? new[]
                {
                    Path.Combine(destPath, "__uae___AUX"),
                    Path.Combine(destPath, "__uae___AUX.info"),
                    Path.Combine(destPath, "_UAEFSDB.___")
                }
                : new[]
                {
                    Path.Combine(destPath, "AUX"),
                    Path.Combine(destPath, "AUX.info")
                };
            var actualFiles = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    [Fact]
    public async Task When_CopyingToLocalDirectoryAndUaeMetafileMetaData_Then_FilesAreCopiedWithMetaData()
    {
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}";
        var srcCopyPath = Path.Combine(srcPath, "rdb", "dh0");
        const UaeMetadata uaeMetadata = UaeMetadata.UaeMetafile;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create src amiga rdb image
            await CreatePfs3FormattedDisk(testCommandHelper, srcPath);
            await CreateAmigaFilesWithReservedWindowsFilename(testCommandHelper, srcPath);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcCopyPath, destPath, true, false, true, uaeMetadata: uaeMetadata);

            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - files are copied to dest path
            var expectedFiles = OperatingSystem.IsWindows()
                ? new[]
                {
                    Path.Combine(destPath, "%41%55%58"),
                    Path.Combine(destPath, "%41%55%58.uaem"),
                    Path.Combine(destPath, "%41%55%58%2e%69%6e%66%6f"),
                    Path.Combine(destPath, "%41%55%58%2e%69%6e%66%6f.uaem")
                }
                : new[]
                {
                    Path.Combine(destPath, "AUX"),
                    Path.Combine(destPath, "AUX.info")
                };
            var actualFiles = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    private async Task CreateAmigaFilesWithReservedWindowsFilename(TestCommandHelper testCommandHelper, string path)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }

        using var media = mediaResult.Value;
        var stream = media.Stream;

        await using var pfs3Volume = await MountPfs3Volume(stream);
        await pfs3Volume.CreateFile("AUX");
        await pfs3Volume.CreateFile("AUX.info");
    }
}