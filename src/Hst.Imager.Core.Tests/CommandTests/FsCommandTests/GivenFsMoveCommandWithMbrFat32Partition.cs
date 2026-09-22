using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.UaeMetadatas;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsMoveCommandWithMbrFat32Partition : FsCommandTestBase
{
    [Fact]
    public async Task When_MovingFileOnSameMedia_Then_FileIsMoved()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        const string srcFilename = "file1.txt";
        var srcPath = Path.Combine(mediaPath, "mbr", "1", srcFilename);
        const string destFilename = "file2.txt";
        var destPath = Path.Combine(mediaPath, "mbr", "1", destFilename);
        const bool forceOverwrite = false;
        const UaeMetadata uaeMetadata = UaeMetadata.None;
        
        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media path with src file
            await commandHelper.AddTestMedia(mediaPath);
            await MbrTestHelper.CreateMbrFatFormattedDisk(commandHelper, mediaPath);
            await MbrTestHelper.CreateFile(commandHelper, mediaPath, [srcFilename]);
        
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());
            
            // assert - file is moved
            var entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
                0, [])).ToList();
            Assert.Single(entries);
            Assert.DoesNotContain(entries, entry => Path.GetFileName(entry.Name) == srcFilename);
            Assert.Contains(entries, entry => Path.GetFileName(entry.Name) == destFilename);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_MovingAFileOnSameMediaAndFileDoesntExist_Then_ErrorIsReturned()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        const string srcFilename = "file1.txt";
        var srcPath = Path.Combine(mediaPath, "mbr", "1", srcFilename);
        const string destFilename = "file2.txt";
        var destPath = Path.Combine(mediaPath, "mbr", "1", destFilename);
        const bool forceOverwrite = false;
        const UaeMetadata uaeMetadata = UaeMetadata.None;
        
        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media path
            await commandHelper.AddTestMedia(mediaPath);
            await MbrTestHelper.CreateMbrFatFormattedDisk(commandHelper, mediaPath);
        
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is faulted
            Assert.True(result.IsFaulted);
            Assert.IsType<PathNotFoundError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_MovingAFileOnSameMediaAndFileExists_Then_ErrorIsReturned()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        const string srcFilename = "file1.txt";
        var srcPath = Path.Combine(mediaPath, "mbr", "1", srcFilename);
        const string destFilename = "file2.txt";
        var destPath = Path.Combine(mediaPath, "mbr", "1", destFilename);
        const bool forceOverwrite = false;
        const UaeMetadata uaeMetadata = UaeMetadata.None;
        
        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media path with src file
            await commandHelper.AddTestMedia(mediaPath);
            await MbrTestHelper.CreateMbrFatFormattedDisk(commandHelper, mediaPath);
            await MbrTestHelper.CreateFile(commandHelper, mediaPath, [srcFilename]);
            
            // arrange - create dest file
            await MbrTestHelper.CreateFile(commandHelper, mediaPath, [destFilename]);
        
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is faulted
            Assert.True(result.IsFaulted);
            Assert.IsType<PathExistsError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_ForceAMovingFileOnSameMediaAndFileExists_Then_FileIsMoved()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        const string srcFilename = "file1.txt";
        var srcPath = Path.Combine(mediaPath, "mbr", "1", srcFilename);
        const string destFilename = "file2.txt";
        var destPath = Path.Combine(mediaPath, "mbr", "1", destFilename);
        const bool forceOverwrite = true;
        const UaeMetadata uaeMetadata = UaeMetadata.None;
        
        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media path with src file
            await commandHelper.AddTestMedia(mediaPath);
            await MbrTestHelper.CreateMbrFatFormattedDisk(commandHelper, mediaPath);
            await MbrTestHelper.CreateFile(commandHelper, mediaPath, [srcFilename]);
            
            // arrange - create dest file
            await MbrTestHelper.CreateFile(commandHelper, mediaPath, [destFilename]);
        
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());
            
            // assert - file is moved
            var entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
                0, [])).ToList();
            Assert.Single(entries);
            Assert.DoesNotContain(entries, entry => Path.GetFileName(entry.Name) == srcFilename);
            Assert.Contains(entries, entry => Path.GetFileName(entry.Name) == destFilename);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_MovingAFileToADirOnSameMedia_Then_FileIsMoved()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        const string srcFilename = "file1.txt";
        var srcPath = Path.Combine(mediaPath, "mbr", "1", srcFilename);
        const string destFilename = "dir1";
        var destPath = Path.Combine(mediaPath, "mbr", "1", destFilename);
        const bool forceOverwrite = true;
        const UaeMetadata uaeMetadata = UaeMetadata.None;

        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media path with src file
            await commandHelper.AddTestMedia(mediaPath);
            await MbrTestHelper.CreateMbrFatFormattedDisk(commandHelper, mediaPath);
            await MbrTestHelper.CreateFile(commandHelper, mediaPath, [srcFilename]);
            
            // arrange - create dest directory
            await MbrTestHelper.CreateDirectory(commandHelper, mediaPath, 0, [destFilename]);
        
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());
            
            // assert - file is moved from root directory
            var entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
                0, [])).ToList();
            Assert.Single(entries);
            Assert.DoesNotContain(entries, entry => Path.GetFileName(entry.Name) == srcFilename);
            Assert.Contains(entries, entry => Path.GetFileName(entry.Name) == destFilename);
            
            // assert - file is moved to dest directory
            var entriesInDestDir = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
                0, [destFilename])).ToList();
            Assert.Single(entriesInDestDir);
            Assert.Contains(entriesInDestDir, entry => Path.GetFileName(entry.Name) == srcFilename);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_MovingADirFromAndToSameMedia_Then_DirIsMoved()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";    
        var srcPath = Path.Combine(mediaPath, "mbr", "1", "dir1");
        var destPath = Path.Combine(mediaPath, "mbr", "1", "dir2");
        
        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media directory with files
            await commandHelper.AddTestMedia(mediaPath);
            await MbrTestHelper.CreateMbrFatFormattedDisk(commandHelper, mediaPath);
            await MbrTestHelper.CreateDirectoriesAndFiles(commandHelper, mediaPath);

            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath);

            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());

            // assert - media root directory contains dir2 entry and not dir1 entry
            var entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
                0, [])).ToList();
            Assert.Single(entries);
            Assert.Contains("dir2", entries.Select(e => e.Name));

            // assert - media dir2 directory contains dir1
            entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
                0, ["dir2"])).ToList();
            Assert.Single(entries);
            Assert.Contains("dir1", entries.Select(e => e.Name));
            
            // assert - media dir2/dir1 directory contains file1 and dir3 from dir1
            entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
                0, ["dir2", "dir1"])).ToList();
            Assert.Equal(2, entries.Count);
            Assert.Contains("file1.txt", entries.Select(e => e.Name));
            Assert.Contains("dir3", entries.Select(e => e.Name));
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_MovingADirFromAndToSameMediaWithPattern_Then_DirIsMoved()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";    
        var srcPath = Path.Combine(mediaPath, "mbr", "1", "dir1", "*");
        var destPath = Path.Combine(mediaPath, "mbr", "1", "dir2");
        
        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media directory with files
            await commandHelper.AddTestMedia(mediaPath);
            await MbrTestHelper.CreateMbrFatFormattedDisk(commandHelper, mediaPath);
            await MbrTestHelper.CreateDirectoriesAndFiles(commandHelper, mediaPath);

            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath);

            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());

            // assert - media root directory contains dir1 and dir2 entries
            var entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
                0, [])).ToList();
            Assert.Equal(2, entries.Count);
            Assert.Contains("dir1", entries.Select(e => e.Name));
            Assert.Contains("dir2", entries.Select(e => e.Name));

            // assert - media dir1 directory is empty
            entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
                0, ["dir1"])).ToList();
            Assert.Empty(entries);
            
            // assert - media dir2 directory contains file1 and dir3 from dir1
            entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
                0, ["dir2"])).ToList();
            Assert.Equal(2, entries.Count);
            Assert.Contains("file1.txt", entries.Select(e => e.Name));
            Assert.Contains("dir3", entries.Select(e => e.Name));
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_MovingADirFromLocalDirectoryToMbrFat32Partition_Then_DirIsMoved()
    {
        // arrange - create paths
        var srcMediaPath = $"{Guid.NewGuid()}-local";
        var srcPath = srcMediaPath;
        var destMediaPath = $"{Guid.NewGuid()}.vhd";    
        var destPath = Path.Combine(destMediaPath, "mbr", "1");

        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create src media directory with files
            Directory.CreateDirectory(srcMediaPath);
            await LocalTestHelper.CreateDirectoriesAndFiles(srcMediaPath);

            // arrange - create dest media path with dirs and files
            await commandHelper.AddTestMedia(destMediaPath);
            await MbrTestHelper.CreateMbrFatFormattedDisk(commandHelper, destMediaPath);
            
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath);

            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());

            // assert - src media directory is removed
            var srcDirExist = Directory.Exists(srcMediaPath);
            Assert.False(srcDirExist);

            // assert - dest root directory contains src media directory entry
            var entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, destMediaPath,
                0, [])).ToList();
            Assert.Single(entries);
            Assert.Contains(entries, entry => entry.Name == srcMediaPath);
            
            // assert - dest src media directory contains dir1 and dir2 entries
            entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, destMediaPath,
                0, [srcMediaPath])).ToList();
            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, entry => entry.Name == "dir1");
            Assert.Contains(entries, entry => entry.Name == "dir2");
            
            // assert - dest dir1 directory contains dir3 and file1.txt entries
            entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, destMediaPath,
                0, [srcMediaPath, "dir1"])).ToList();
            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, entry => entry.Name == "dir3");
            Assert.Contains(entries, entry => entry.Name == "file1.txt");
        }
        finally
        {
            DeletePaths(srcMediaPath, destMediaPath);
        }
    }
    
    [Fact]
    public async Task When_MovingADirFromLocalDirectoryToMbrFat32PartitionWithPattern_Then_DirIsMoved()
    {
        // arrange - create paths
        var srcMediaPath = $"{Guid.NewGuid()}-local";
        var srcPath = Path.Combine(srcMediaPath, "*");
        var destMediaPath = $"{Guid.NewGuid()}.vhd";    
        var destPath = Path.Combine(destMediaPath, "mbr", "1");

        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create src media directory with files
            Directory.CreateDirectory(srcMediaPath);
            await LocalTestHelper.CreateDirectoriesAndFiles(srcMediaPath);

            // arrange - create dest media path with dirs and files
            await commandHelper.AddTestMedia(destMediaPath);
            await MbrTestHelper.CreateMbrFatFormattedDisk(commandHelper, destMediaPath);
            
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath);

            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());

            // assert - src media directory is empty
            var srcDirEntries = Directory.GetDirectories(srcMediaPath, "*", SearchOption.AllDirectories);
            var srcFileEntries = Directory.GetFiles(srcMediaPath, "*", SearchOption.AllDirectories);
            Assert.Empty(srcDirEntries);
            Assert.Empty(srcFileEntries);

            // assert - dest root directory contains dir1 and dir2 entries
            var entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, destMediaPath,
                0, [])).ToList();
            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, entry => entry.Name == "dir1");
            Assert.Contains(entries, entry => entry.Name == "dir2");
            
            // assert - dest dir1 directory contains dir3 and file1.txt entries
            entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(commandHelper, destMediaPath,
                0, ["dir1"])).ToList();
            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, entry => entry.Name == "dir3");
            Assert.Contains(entries, entry => entry.Name == "file1.txt");
        }
        finally
        {
            DeletePaths(srcMediaPath, destMediaPath);
        }
    }
}