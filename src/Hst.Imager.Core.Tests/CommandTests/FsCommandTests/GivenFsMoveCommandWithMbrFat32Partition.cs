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
}