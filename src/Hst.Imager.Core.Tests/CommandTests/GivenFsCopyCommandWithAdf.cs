namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Models;
using Xunit;

public class GivenFsCopyCommandWithAdf : FsCommandTestBase
{
    [Fact]
    public async Task WhenCopyingFromAndToSameAdfThenDirectoriesAreCopied()
    {
        var imagePath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(imagePath, "*");
        var destPath = Path.Combine(imagePath, "copied");

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - source disk image file with directories
            await CreateAdfDisk(testCommandHelper, imagePath);
            await CreateAdfContent(testCommandHelper, imagePath);

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
            var mediaResult = await testCommandHelper.GetReadableFileMedia(imagePath);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }
            
            // assert - mount fast file system volume
            using var media = mediaResult.Value;
            await using var ffsVolume = await MountFastFileSystemVolume(media.Stream);

            // assert - get root entries
            var entries = (await ffsVolume.ListEntries()).ToList();

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

            await ffsVolume.ChangeDirectory("copied");

            // assert - get copied entries
            entries = (await ffsVolume.ListEntries()).ToList();
            
            // assert - copied directory contains 2 entries
            Assert.Equal(2, entries.Count);

            // assert - root directory contains dir1 directory
            Assert.Equal("dir1",
                entries.FirstOrDefault(x => x.Type == EntryType.Dir && x.Name.Equals("dir1", StringComparison.OrdinalIgnoreCase))?.Name);

            // assert - root directory contains dir2 directory
            Assert.Equal("dir2",
                entries.FirstOrDefault(x => x.Type == EntryType.Dir && x.Name.Equals("dir2", StringComparison.OrdinalIgnoreCase))?.Name);
            
            await ffsVolume.ChangeDirectory("dir1");
            
            // assert - get dir1 entries
            entries = (await ffsVolume.ListEntries()).ToList();

            // assert - dir1 directory contains 1 entry
            Assert.Equal(2, entries.Count);
            
            // assert - dir1 directory contains dir3 directory
            Assert.Equal("dir3",
                entries.FirstOrDefault(x => x.Type == EntryType.Dir && x.Name.Equals("dir3", StringComparison.OrdinalIgnoreCase))?.Name);

            // assert - dir1 directory contains file1.txt file
            Assert.Equal("file1.txt",
                entries.FirstOrDefault(x => x.Type == EntryType.File && x.Name.Equals("file1.txt", StringComparison.OrdinalIgnoreCase))?.Name);
        }
        finally
        {
            DeletePaths(imagePath);
        }
    }
    
    private async Task CreateAdfContent(TestCommandHelper testCommandHelper, string path)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }

        using var media = mediaResult.Value;
        var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

        await using var ffsVolume = await MountFastFileSystemVolume(stream);
        await ffsVolume.CreateDirectory("dir1");
        await ffsVolume.CreateDirectory("dir2");
        await ffsVolume.ChangeDirectory("dir1");
        await ffsVolume.CreateDirectory("dir3");
        await ffsVolume.CreateFile("file1.txt");
    }
}