namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Commands;
using Hst.Core.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Models;
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
            CreateDirectories(srcPath);
            await CreatePfs3FormattedDisk(testCommandHelper, destPath);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, Path.Combine(destPath, "rdb", "dh0"), true, false, true);

            // act - copy
            var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get media
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

    [Fact]
    public async Task WhenCopyingFromAndToSameVhdThenDirectoriesAreCopied()
    {
        var imagePath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine(imagePath, "rdb", "dh0");
        var destPath = Path.Combine(imagePath, "rdb", "dh0", "copied");

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - source disk image file with directories
            await CreatePfs3FormattedDisk(testCommandHelper, imagePath);
            await CreatePfs3Directories(testCommandHelper, imagePath);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);

            // act - copy
            var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();
            
            // assert - get media
            var mediaResult = testCommandHelper.GetReadableFileMedia(imagePath);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }
            
            // assert - mount pfs3 volume
            using var media = mediaResult.Value;
            await using var pfs3Volume = await MountPfs3Volume(media.Stream);

            // assert - get root entries
            var entries = (await pfs3Volume.ListEntries()).ToList();

            // assert - root directory contains 3 entries
            Assert.Equal(3, entries.Count);

            // assert - root directory contains dir1 directory
            Assert.Equal("dir1",
                entries.FirstOrDefault(x => x.Type == EntryType.Dir && x.Name.Equals("dir1", StringComparison.OrdinalIgnoreCase))?.Name);

            // assert - root directory contains dir2 directory
            Assert.Equal("dir2",
                entries.FirstOrDefault(x => x.Type == EntryType.Dir && x.Name.Equals("dir2", StringComparison.OrdinalIgnoreCase))?.Name);

            // assert - root directory contains copied directory
            Assert.Equal("copied",
                entries.FirstOrDefault(x => x.Type == EntryType.Dir && x.Name.Equals("copied", StringComparison.OrdinalIgnoreCase))?.Name);

            await pfs3Volume.ChangeDirectory("dir1");

            // assert - get dir1 entries
            entries = (await pfs3Volume.ListEntries()).ToList();

            // assert - dir1 directory contains 1 entry
            Assert.Single(entries);
            
            // assert - dir1 directory contains dir3 directory
            Assert.Equal("dir3",
                entries.FirstOrDefault(x => x.Type == EntryType.Dir && x.Name.Equals("dir3", StringComparison.OrdinalIgnoreCase))?.Name);
            
            // assert - change to copied directory
            await pfs3Volume.ChangeDirectory("/");
            await pfs3Volume.ChangeDirectory("copied");

            // assert - get copied entries
            entries = (await pfs3Volume.ListEntries()).ToList();

            // assert - root directory contains 2 entries
            Assert.Equal(2, entries.Count);
            
            // assert - copied directory contains dir1 directory
            Assert.Equal("dir1",
                entries.FirstOrDefault(x => x.Type == EntryType.Dir && x.Name.Equals("dir1", StringComparison.OrdinalIgnoreCase))?.Name);

            // assert - copied directory contains dir2 directory
            Assert.Equal("dir2",
                entries.FirstOrDefault(x => x.Type == EntryType.Dir && x.Name.Equals("dir2", StringComparison.OrdinalIgnoreCase))?.Name);

            // assert - change to copied directory
            await pfs3Volume.ChangeDirectory("dir1");
            
            // assert - get copied/dir1 entries
            entries = (await pfs3Volume.ListEntries()).ToList();

            // assert - copied/dir1 directory contains 1 entry
            Assert.Single(entries);
            
            // assert - copied/dir1 directory contains dir3 directory
            Assert.Equal("dir3",
                entries.FirstOrDefault(x => x.Type == EntryType.Dir && x.Name.Equals("dir3", StringComparison.OrdinalIgnoreCase))?.Name);
        }
        finally
        {
            DeletePaths(imagePath);
        }
    }
    
    private void CreateDirectories(string path)
    {
        Directory.CreateDirectory(Path.Combine(path, "dir1"));
        Directory.CreateDirectory(Path.Combine(path, "dir2"));
        Directory.CreateDirectory(Path.Combine(path, "dir1", "dir3"));
    }
    
    private async Task CreatePfs3Directories(TestCommandHelper testCommandHelper, string path)
    {
        var mediaResult = testCommandHelper.GetWritableFileMedia(path);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }

        using var media = mediaResult.Value;
        var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

        await using var pfs3Volume = await MountPfs3Volume(stream);
        await pfs3Volume.CreateDirectory("dir1");
        await pfs3Volume.CreateDirectory("dir2");
        await pfs3Volume.ChangeDirectory("dir1");
        await pfs3Volume.CreateDirectory("dir3");
    }
}