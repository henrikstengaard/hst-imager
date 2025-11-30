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
    public async Task When_CopyingFromOneDirectoryToAnother_Then_FileIsCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir1", "dir3");
        const bool recursive = false;
        
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
                srcPath, destPath, recursive, false, true);
            
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
    public async Task When_CopyingFromOneDirectoryToAnotherRecursive_Then_FileIsCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir4");
        const bool recursive = true; 
        const bool makeDirectory = false;
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create adf formatted disk
            await AdfTestHelper.CreateFormattedAdfDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await AdfTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);
            await AdfTestHelper.CreateDirectory(testCommandHelper, mediaPath, ["dir4"]);
            await AdfTestHelper.CreateFile(testCommandHelper, mediaPath, ["dir1", "dir3","file1.txt"]);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true, makeDirectory: makeDirectory);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            
            // assert - copy successful
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();
            
            // assert - root directory contains 3 entries
            var entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                [])).ToList();
            Assert.Equal(3, entries.Count);

            // assert - root directory contains 3 dir entries and 1 file entry
            Assert.Equal(["dir1", "dir2", "dir4"], entries.Where(x => x.Type == EntryType.Dir).Select(x => x.Name).Order());

            // assert - dir4 directory contains 2 entries
            entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                ["dir4"])).ToList();
            Assert.Equal(2, entries.Count);
            
            // assert - dir4 contains 1 directory and 1 file entry
            Assert.Equal(["dir3"], entries.Where(x => x.Type == EntryType.Dir).Select(x => x.Name).Order());
            Assert.Equal(["file1.txt"], entries.Where(x => x.Type == EntryType.File).Select(x => x.Name).Order());
            
            // assert - dir4, dir3 directory contains 1 entry
            entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath, 
                ["dir4", "dir3"])).ToList();
            Assert.Single(entries);

            // assert - dir4, dir3 contains 1 file entry
            Assert.Equal(["file1.txt"], entries.Where(x => x.Type == EntryType.File).Select(x => x.Name).Order());
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CopyingToNewName_Then_FileIsCopied()
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
    public async Task When_CopyingToNonExistingRootDirectory_Then_FileIsCopiedAndRenamed()
    {
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir4");
        const bool recursive = false;
        
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
                srcPath, destPath, recursive, false, true);
            
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
    public async Task When_CopyingFromRootToSubDirectory_Then_FileIsCopied()
    {
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "file2.txt");
        var destPath = Path.Combine(mediaPath, "dir1");
        const bool recursive = false; 
        const bool makeDirectory = false;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create adf formatted disk
            await AdfTestHelper.CreateFormattedAdfDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await AdfTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);
            await AdfTestHelper.CreateFile(testCommandHelper, mediaPath, ["file2.txt"]);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true, makeDirectory: makeDirectory);

            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);

            // assert - copy successful
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to avoid source and destination being reused between commands
            testCommandHelper.ClearActiveMedias();

            // assert - dir1 directory contains 3 entries
            var entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, mediaPath,
                ["dir1"])).ToList();
            Assert.Equal(3, entries.Count);

            // assert - dir1 contains 1 directory entry
            var expectedDirs =
                new[]
                {
                    "dir3"
                };
            Assert.Equal(expectedDirs, entries.Where(x => x.Type == EntryType.Dir).Select(x => x.Name).Order());

            // assert - dir1 contains 2 file entries
            var expectedFiles =
                new[]
                {
                    "file1.txt",
                    "file2.txt"
                };
            Assert.Equal(expectedFiles, entries.Where(x => x.Type == EntryType.File).Select(x => x.Name).Order());
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingFromRootToSubDirectoryRecursive_Then_CyclicPathErrorIsReturned()
    {
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "file2.txt");
        var destPath = Path.Combine(mediaPath, "dir1");
        const bool recursive = true; 
        const bool makeDirectory = false;
        
        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create adf formatted disk
            await AdfTestHelper.CreateFormattedAdfDisk(testCommandHelper, mediaPath);

            // arrange - create directories and files
            await AdfTestHelper.CreateDirectoriesAndFiles(testCommandHelper, mediaPath);
            await AdfTestHelper.CreateFile(testCommandHelper, mediaPath, ["file2.txt"]);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true, makeDirectory: makeDirectory);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsFaulted);
            Assert.IsType<CyclicPathError>(result.Error);
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
    public async Task When_CopyingToNonExistingSubDirectoryWithCreateDestDir_Then_FileIsCopied()
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