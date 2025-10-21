using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenFsCopyCommandCopyingMultipleDirectoriesAndFilesFromAndToSameRdbMedia : FsCommandTestBase
{
    [Fact]
    public async Task When_CopyingFromAndToSameRdbMedia_Then_DirectoriesAndFilesAreCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "rdb","1","*"]);
        var destPath = Path.Combine([mediaPath, "rdb", "1", "dir1"]);

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            testCommandHelper.AddTestMedia(mediaPath, 100.MB());
            
            await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

            await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - root directory contains 2 entries
            var entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, [])).ToList();
            Assert.Equal(2, entries.Count);
            Assert.Equal(["dir1", "dir2"], entries.Select(x => x.Name).Order());

            // assert - dir1 directory contains 4 entries
            entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, ["dir1"])).ToList();
            Assert.Equal(["dir1", "dir2", "dir3", "file1.txt"], entries.Select(x => x.Name).Order());

            // assert - dir1, dir1 directory contains 2 entries
            entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, ["dir1", "dir1"])).ToList();
            Assert.Equal(2, entries.Count);
            Assert.Equal(["dir3", "file1.txt"], entries.Select(x => x.Name).Order());

            // assert - dir1, dir1, dir3 directory contains 0 entries
            entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, ["dir1", "dir1", "dir3"])).ToList();
            Assert.Empty(entries);

            // assert - dir1, dir2 directory contains 0 entries
            entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, ["dir1", "dir2"])).ToList();
            Assert.Empty(entries);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CopyingToNonExistingRootDirectoryFromAndToSameRdbMedia_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "rdb","1","*"]);
        var destPath = Path.Combine([mediaPath, "rdb", "1", "dir4"]);

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            testCommandHelper.AddTestMedia(mediaPath, 100.MB());
            
            // arrange - create pfs3 formatted disk
            await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsFaulted);
            Assert.False(result.IsSuccess);
            Assert.IsType<PathNotFoundError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingToNonExistingSubDirectoryFromAndToSameRdbMedia_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "rdb","1","*"]);
        var destPath = Path.Combine([mediaPath, "rdb", "1", "dir4", "dir5"]);

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            testCommandHelper.AddTestMedia(mediaPath, 100.MB());
            
            // arrange - create pfs3 formatted disk
            await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await RdbTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsFaulted);
            Assert.False(result.IsSuccess);
            Assert.IsType<PathNotFoundError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
}