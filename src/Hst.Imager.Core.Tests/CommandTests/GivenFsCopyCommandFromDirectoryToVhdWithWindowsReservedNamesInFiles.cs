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
    public async Task WhenCopyingRecursivelyToVhdThenDirectoriesAndFilesAreCopied()
    {
        var srcPath = $"{Guid.NewGuid()}";
        var destPath = $"{Guid.NewGuid()}.vhd";

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create source directory
            await CreateDirectoriesAndFiles(srcPath);
            
            // arrange - create destination disk image
            await CreatePfs3FormattedDisk(testCommandHelper, destPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, Path.Combine(destPath, "rdb", "dh0"), true, false, true);

            // act - copy
            var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get dest media
            var mediaResult = testCommandHelper.GetReadableFileMedia(destPath);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }
            
            // assert - mount pfs3 volume
            using var media = mediaResult.Value;
            await using var pfs3Volume = await MountPfs3Volume(media.Stream);
            // assert - get root entries
            var entries = (await pfs3Volume.ListEntries()).ToList();
            
            // assert - 1 directory in root are copied
            Assert.Single(entries);
            
            Assert.Equal("dir1",
                entries.FirstOrDefault(x => x.Name.Equals("dir1", StringComparison.OrdinalIgnoreCase))?.Name);
            
            await pfs3Volume.ChangeDirectory("dir1");
            
            // assert - get dir1 entries
            entries = (await pfs3Volume.ListEntries()).ToList();
            
            // assert - 2 files in dir1 are copied
            Assert.Equal(2, entries.Count);
            
            Assert.Equal("AUX",
                entries.FirstOrDefault(x => x.Name.Equals("AUX", StringComparison.OrdinalIgnoreCase))?.Name);
            Assert.Equal("AUX.info",
                entries.FirstOrDefault(x => x.Name.Equals("AUX.info", StringComparison.OrdinalIgnoreCase))?.Name);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    private async Task CreateDirectoriesAndFiles(string path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }
        
        var dir1Path = Path.Combine(path, "dir1");
        Directory.CreateDirectory(dir1Path);
        await File.WriteAllBytesAsync(Path.Combine(dir1Path, ".AUX"), Array.Empty<byte>());
        await File.WriteAllBytesAsync(Path.Combine(dir1Path, ".AUX.info"), Array.Empty<byte>());
    }
}