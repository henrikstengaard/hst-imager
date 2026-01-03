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

public class GivenFsCopyCommandCopyingMultipleDirectoriesAndFilesFromAndToSameMbrFat32Media : FsCommandTestBase
{
    [Fact]
    public async Task When_CopyingFromAndToSameMbrFat32Media_Then_DirectoriesAndFilesAreCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "mbr", "1", "dir1", "*"]);
        var destPath = Path.Combine([mediaPath, "mbr", "1", "copied"]);
        const bool recursive = true;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            testCommandHelper.AddTestMedia(mediaPath, 0);
            
            // arrange - create fat formatted disk
            await TestHelper.CreateMbrFatFormattedDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await MbrTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);
            await MbrTestHelper.CreateDirectory(testCommandHelper, mediaPath, 0, ["copied"]);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - root directory contains 2 entries
            var entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, [])).ToList();
            
            // assert - root directory contains 3 directories
            Assert.Equal(3, entries.Count);
            Assert.Equal(["copied", "dir1", "dir2"], entries.Select(x => x.Name).Order());

            // assert - copied directory contains 2 entries
            entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, ["copied"])).ToList();
            Assert.Equal(2, entries.Count);
            Assert.Equal(["dir3", "file1.txt"], entries.Select(x => x.Name).Order());

            // assert - copied, dir3 directory is empty
            entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, ["copied", "dir3"])).ToList();
            Assert.Empty(entries);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingFromAndToSameMbrFat32MediaBetweenTwoPartitions_Then_DirectoriesAndFilesAreCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "mbr", "1", "dir1", "*"]);
        var destPath = Path.Combine([mediaPath, "mbr", "2"]);
        const bool recursive = true;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            testCommandHelper.AddTestMedia(mediaPath, 0);
            
            // arrange - create fat formatted disk
            await TestHelper.CreateMbrDisk(testCommandHelper, mediaPath, 100.MB());
            await TestHelper.AddMbrDiskPartition(testCommandHelper, mediaPath, 45.MB());
            await TestHelper.AddMbrDiskPartition(testCommandHelper, mediaPath, 45.MB());
            await MbrTestHelper.FatFormatMbrPartition(testCommandHelper, mediaPath, 0);
            await MbrTestHelper.FatFormatMbrPartition(testCommandHelper, mediaPath, 1);

            // arrange - create directories and files in partition 1
            await MbrTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - partition 2 root directory contains 2 entries
            var entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                1, [])).ToList();
            Assert.Equal(2, entries.Count);
            Assert.Equal(["dir3", "file1.txt"], entries.Select(x => x.Name).Order());

            // assert - partition 2, dir3 directory is empty
            entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                1, ["dir3"])).ToList();
            Assert.Empty(entries);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CopyingFromAndToSameMbrFat32MediaWithCyclicPath_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "mbr", "1", "*"]);
        var destPath = Path.Combine([mediaPath, "mbr", "1", "copied"]);

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            testCommandHelper.AddTestMedia(mediaPath, 0);
            
            // arrange - create fat formatted disk
            await TestHelper.CreateMbrFatFormattedDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await MbrTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);
            await MbrTestHelper.CreateDirectory(testCommandHelper, mediaPath, 0, ["copied"]);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            
            // assert - copy failed and returned cyclic error
            Assert.True(result.IsFaulted);
            Assert.IsType<CyclicPathError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingFromAndToSameMbrFat32MediaWithSelfCopyPath_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "mbr", "1", "dir1", "file1.txt"]);
        var destPath = Path.Combine([mediaPath, "mbr", "1", "dir1", "file1.txt"]);

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            testCommandHelper.AddTestMedia(mediaPath, 0);
            
            // arrange - create fat formatted disk
            await TestHelper.CreateMbrFatFormattedDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await MbrTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            
            // assert - copy failed and returned self copy error
            Assert.True(result.IsFaulted);
            Assert.IsType<SelfCopyError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingToNonExistingRootDirectoryFromAndToSameMbrFat32Media_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "mbr","1","*"]);
        var destPath = Path.Combine([mediaPath, "mbr", "1", "dir4"]);

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            testCommandHelper.AddTestMedia(mediaPath, 0);
            
            // arrange - create fat formatted disk
            await TestHelper.CreateMbrFatFormattedDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await MbrTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

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
    public async Task When_CopyingToNonExistingSubDirectoryFromAndToSameMbrFat32Media_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine([mediaPath, "mbr","1","*"]);
        var destPath = Path.Combine([mediaPath, "mbr", "1", "dir4", "dir5"]);

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            testCommandHelper.AddTestMedia(mediaPath, 0);
            
            // arrange - create fat formatted disk
            await TestHelper.CreateMbrFatFormattedDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await MbrTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

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