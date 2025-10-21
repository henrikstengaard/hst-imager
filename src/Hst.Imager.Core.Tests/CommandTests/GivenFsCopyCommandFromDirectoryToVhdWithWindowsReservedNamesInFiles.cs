using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class GivenFsCopyCommandFromDirectoryToVhdWithWindowsReservedNamesInFiles : FsCommandTestBase
{
    [Fact]
    public async Task When_CopyingFromAmigaRdbToLocalDirectory_Then_DirectoriesAndFilesAreCopied()
    {
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}";
        var srcCopyPath = Path.Combine(srcPath, "rdb", "dh0");

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
                srcCopyPath, destPath, true, false, true);

            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - files are copied to dest path
            var expectedFiles = new[]
            {
                Path.Combine(destPath, string.Concat(OperatingSystem.IsWindows() ? "__uae___" : string.Empty, "AUX")),
                Path.Combine(destPath, "AUX.info")
            }.Concat(OperatingSystem.IsWindows()
                ? [Path.Combine(destPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName)] : []);
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
        var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

        await using var pfs3Volume = await MountPfs3Volume(stream);
        await pfs3Volume.CreateFile("AUX");
        await pfs3Volume.CreateFile("AUX.info");
    }

    private async Task CreateDirectoriesAndFiles(string path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }
        
        var dir1Path = Path.Combine(path, "dir1");
        Directory.CreateDirectory(dir1Path);

        var windowsReservedPrefix = OperatingSystem.IsWindows() ? "." : string.Empty;
        
        await File.WriteAllBytesAsync(Path.Combine(dir1Path, $"{windowsReservedPrefix}AUX"), Array.Empty<byte>());
        await File.WriteAllBytesAsync(Path.Combine(dir1Path, $"{windowsReservedPrefix}AUX.info"), Array.Empty<byte>());
    }
}