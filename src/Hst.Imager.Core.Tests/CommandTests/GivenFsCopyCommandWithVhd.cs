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

public class GivenFsCopyCommandWithVhd : FsCommandTestBase
{
    [Fact]
    public async Task WhenCopyingDirectoriesRecursivelyToVhdThenDirectoriesAreCopied()
    {
        var srcPath = $"{Guid.NewGuid()}";
        var destPath = $"{Guid.NewGuid()}.vhd";

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - source disk image file with directories
            await CreatePfs3FormattedDisk(testCommandHelper, srcPath);
            await CreateDirectories(testCommandHelper, srcPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, true);

            // act - copy
            var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get media
            var mediaResult = testCommandHelper.GetReadableFileMedia(srcPath);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }
            
            // assert - mount pfs3 volume
            using var media = mediaResult.Value;
            await using var pfs3Volume = await MountVolume(media.Stream);

            // assert - get root entries
            var entries = (await pfs3Volume.ListEntries()).ToList();

            // assert - 2 directories in root are copied
            Assert.Equal(2, entries.Count);

            Assert.Equal("dir1",
                entries.FirstOrDefault(x => x.Name.Equals("dir1", StringComparison.OrdinalIgnoreCase))?.Name);

            Assert.Equal("dir2",
                entries.FirstOrDefault(x => x.Name.Equals("dir2", StringComparison.OrdinalIgnoreCase))?.Name);

            await pfs3Volume.ChangeDirectory("dir1");

            // assert - get dir1 entries
            entries = (await pfs3Volume.ListEntries()).ToList();

            // assert - 1 directory in dir1 is copied
            Assert.Single(entries);
            
            Assert.Equal("dir3",
                entries.FirstOrDefault(x => x.Name.Equals("dir3", StringComparison.OrdinalIgnoreCase))?.Name);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    private async Task CreateDirectories(TestCommandHelper testCommandHelper, string path)
    {
        var mediaResult = testCommandHelper.GetWritableFileMedia(path);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }

        using var media = mediaResult.Value;
        await using var pfs3Volume = await MountVolume(media.Stream);
        await pfs3Volume.CreateDirectory("dir1");
        await pfs3Volume.CreateDirectory("dir2");
        await pfs3Volume.ChangeDirectory("dir1");
        await pfs3Volume.CreateDirectory("dir3");
    }
}