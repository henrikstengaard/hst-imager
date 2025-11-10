using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenFsCopyCommandCopyingSingleFileFromAndToSameAdfMedia : FsCommandTestBase
{
    [Fact]
    public async Task When_CopyingToDirFromAndToSameAdfMedia_Then_FileIsCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir1", "dir3");
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create adf formatted disk
            await AdfTestHelper.CreateFormattedAdfDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await AdfTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

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
            var entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                ["dir1", "dir3"])).ToList();
            Assert.Equal(["file1.txt"], entries.Select(x => x.Name).Order());
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CopyingToNewNameFromAndToSameAdfMedia_Then_FileIsCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir1", "file1_copy.txt");
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create adf formatted disk
            await AdfTestHelper.CreateFormattedAdfDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await AdfTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, false, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - dir1 directory contains 3 entries
            var entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                ["dir1"])).ToList();
            Assert.Equal(["dir3", "file1_copy.txt", "file1.txt"], entries.Select(x => x.Name).Order());
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CopyingToNonExistingRootDirectoryFromAndToSameAdfMedia_Then_FileIsCopiedAndRenamed()
    {
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir4");
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create adf formatted disk
            await AdfTestHelper.CreateFormattedAdfDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await AdfTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, false, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - root directory contains 2 dir entries
            var entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                [])).ToList();
            Assert.Equal(["dir1", "dir2"], entries.Where(x => x.Type == EntryType.Dir).Select(x => x.Name).Order());
            
            // assert - root directory contains 1 file entry
            entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                [])).ToList();
            Assert.Equal(["dir4"], entries.Where(x => x.Type == EntryType.File).Select(x => x.Name).Order());
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CopyingToNonExistingSubDirectoryFromAndToSameAdfMedia_Then_ErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir4", "dir5");
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create adf formatted disk
            await AdfTestHelper.CreateFormattedAdfDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await AdfTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            
            // assert - error is returned and error is path not found
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

    [Fact]
    public async Task When_CopyingToNonExistingSubDirectoryFromAndToSameAdfMediaWithCreateDestDir_Then_FileIsCopied()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir4", "dir5");
        const bool createDestDir = true;
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create adf formatted disk
            await AdfTestHelper.CreateFormattedAdfDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await AdfTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true, makeDirectory: createDestDir);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            
            // assert - copy is successful
            Assert.True(result.IsSuccess);
            
            // assert - dir4, dir5 directory contains 1 file entry
            var entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                ["dir4", "dir5"])).ToList();
            Assert.Equal(["file1.txt"], entries.Where(x => x.Type == EntryType.File).Select(x => x.Name).Order());
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
}