﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenFsCopyCommandCopyingSingleFileFromAndToSameMbrFat32Media : FsCommandTestBase
{
    [Fact]
    public async Task When_CopyingToDirFromAndToSameMbrFat32Media_Then_FileIsCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine(mediaPath, "mbr", "1", "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "mbr", "1", "dir1", "dir3");
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            testCommandHelper.AddTestMedia(mediaPath, 100.MB());
            
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
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - dir3 directory contains 1 entry
            var entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, ["dir1", "dir3"])).ToList();
            Assert.Equal(["file1.txt"], entries.Select(x => x.Name).Order());
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingToNewNameFromAndToSameMbrFat32Media_Then_FileIsCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine(mediaPath, "mbr", "1", "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "mbr", "1", "dir1", "file1_copy.txt");
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            testCommandHelper.AddTestMedia(mediaPath, 100.MB());
            
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
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();
            
            // assert - dir1 directory contains 3 entries
            var entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, ["dir1"])).ToList();
            Assert.Equal(["dir3", "file1_copy.txt", "file1.txt"], entries.Select(x => x.Name).Order());
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingToNonExistingRootDirectoryFromAndToSameMbrFat32Media_Then_FileIsCopiedAndRenamed()
    {
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var srcPath = Path.Combine(mediaPath, "mbr", "1", "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "mbr", "1", "dir4");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            testCommandHelper.AddTestMedia(mediaPath, 100.MB());
            
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
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - root directory contains 2 dir entries
            var entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, [])).ToList();
            Assert.Equal(["dir1", "dir2"], entries.Where(x => x.Type == EntryType.Dir).Select(x => x.Name).Order());
            
            // assert - root directory contains 1 file entry
            entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                0, [])).ToList();
            Assert.Equal(["dir4"], entries.Where(x => x.Type == EntryType.File).Select(x => x.Name).Order());
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
        var srcPath = Path.Combine(mediaPath, "mbr", "1", "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "mbr", "1", "dir4", "dir5");
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - add test media
            testCommandHelper.AddTestMedia(mediaPath, 100.MB());
            
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
            Assert.False(result.IsSuccess);
            Assert.True(result.IsFaulted);
            Assert.NotNull(result.Error);
            Assert.IsType<PathNotFoundError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
}