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

public class GivenFsCopyCommandCopyingSingleFileFromAndToSameMedia : FsCommandTestBase
{
    [Fact]
    public async Task When_CopyingToDirFromAndToSameRdbMedia_Then_FileIsCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine(mediaPath, "rdb", "1", "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "rdb", "1", "dir1", "dir3");
        
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
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - dir3 directory contains 1 entry
            var entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, ["dir1", "dir3"])).ToList();
            Assert.Equal(["file1.txt"], entries.Select(x => x.Name).Order());
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingToNewNameFromAndToSameRdbMedia_Then_FileIsCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine(mediaPath, "rdb", "1", "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "rdb", "1", "dir1", "file1_copy.txt");
        
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
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - dir1 directory contains 3 entries
            var entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, ["dir1"])).ToList();
            Assert.Equal(["dir3", "file1_copy.txt", "file1.txt"], entries.Select(x => x.Name).Order());
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingToNonExistingRootDirectoryFromAndToSameRdbMedia_Then_FileIsCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine(mediaPath, "rdb", "1", "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "rdb", "1", "dir4");
        
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
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - root directory contains 3 entries
            var entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, [])).ToList();
            Assert.Equal(["dir1", "dir2", "dir4"], entries.Select(x => x.Name).Order());
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
        var srcPath = Path.Combine(mediaPath, "rdb", "1", "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "rdb", "1", "dir4", "dir5");
        
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
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
}