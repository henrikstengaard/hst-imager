using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Hst.Imager.Core.PathComponents;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsDirCommandWithZip
{
    [Theory]
    [InlineData("", true)]
    [InlineData("", false)]
    [InlineData("*", true)]
    [InlineData("*", false)]
    [InlineData("file1.txt", true)]
    [InlineData("file1.txt", false)]
    [InlineData("dir1", true)]
    [InlineData("dir1", false)]
    [InlineData("dir1\\t*", true)]
    [InlineData("dir1\\t*", false)]
    [InlineData("dir1\\test.txt", true)]
    [InlineData("dir1\\test.txt", false)]
    [InlineData("dir1\\dir2", true)]
    [InlineData("dir1\\dir2", false)]
    [InlineData("dir1\\dir2\\*", true)]
    [InlineData("dir1\\dir2\\*", false)]
    public async Task When_ListingEntriesInExisting_Then_EntriesAreListed(string path, bool recursive)
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.zip";
        var dirPath = Path.Combine(new[]{mediaPath}.Concat(MediaPath.GenericMediaPath.Split(path)).ToArray());
        
        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create zip file with directories and files
            CreateZipFileWithDirectoriesAndFiles(mediaPath);
            
            // arrange - create fs dir command
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            EntriesInfo entriesInfo = null;
            fsDirCommand.EntriesRead += (_, args) =>
            {
                entriesInfo = args.EntriesInfo;
            };

            // act - execute fs dir command
            var result = await fsDirCommand.Execute(CancellationToken.None);
        
            // assert - result is success with one entry
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.NotNull(entriesInfo);
            Assert.NotEmpty(entriesInfo.Entries);
        }
        finally
        {
            if (File.Exists(mediaPath))
            {
                File.Delete(mediaPath);
            }
        }
    }

    [Theory]
    [InlineData("x*")]
    [InlineData("test4*")]
    [InlineData("dir1\\e*")]
    public async Task When_ListingEntriesInExistingDirectoryWithPatternNotMatching_Then_NoEntriesAreListed(string path)
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.zip";
        var dirPath = Path.Combine(new[]{mediaPath}.Concat(MediaPath.GenericMediaPath.Split(path)).ToArray());
        const bool recursive = false;

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create zip file with directories and files
            CreateZipFileWithDirectoriesAndFiles(mediaPath);
            
            // arrange - create fs dir command
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            EntriesInfo entriesInfo = null;
            fsDirCommand.EntriesRead += (_, args) =>
            {
                entriesInfo = args.EntriesInfo;
            };

            // act - execute fs dir command
            var result = await fsDirCommand.Execute(CancellationToken.None);
        
            // assert - result is success with no entries
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.NotNull(entriesInfo);
            Assert.Empty(entriesInfo.Entries);
        }
        finally
        {
            if (File.Exists(mediaPath))
            {
                File.Delete(mediaPath);
            }
        }
    }

    [Theory]
    [InlineData("x*")]
    [InlineData("test4*")]
    [InlineData("dir1\\es*")]
    public async Task When_ListingEntriesInExistingDirectoryWithPatternNotMatchingRecursively_Then_NoEntriesAreListed(string path)
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.zip";
        var dirPath = Path.Combine(new[]{mediaPath}.Concat(MediaPath.GenericMediaPath.Split(path)).ToArray());
        const bool recursive = true;

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create zip file with directories and files
            CreateZipFileWithDirectoriesAndFiles(mediaPath);
            
            // arrange - create fs dir command
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            EntriesInfo entriesInfo = null;
            fsDirCommand.EntriesRead += (_, args) =>
            {
                entriesInfo = args.EntriesInfo;
            };

            // act - execute fs dir command
            var result = await fsDirCommand.Execute(CancellationToken.None);
        
            // assert - result is success with no file entries
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.NotNull(entriesInfo);
            Assert.DoesNotContain(entriesInfo.Entries, entry => entry.Type == EntryType.File);
        }
        finally
        {
            if (File.Exists(mediaPath))
            {
                File.Delete(mediaPath);
            }
        }
    }
    
    [Theory]
    [InlineData("x", true)]
    [InlineData("x", false)]
    [InlineData("test4", true)]
    [InlineData("test4", false)]
    [InlineData("dir1\\e", true)]
    [InlineData("dir1\\e", false)]
    public async Task When_ListingEntriesInNonExistingDirectory_Then_ErrorIsReturned(string path, bool recursive)
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.zip";
        var dirPath = Path.Combine(new[]{mediaPath}.Concat(MediaPath.GenericMediaPath.Split(path)).ToArray());

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create zip file with directories and files
            CreateZipFileWithDirectoriesAndFiles(mediaPath);
            
            // arrange - create fs dir command
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);

            // act - execute fs dir command
            var result = await fsDirCommand.Execute(CancellationToken.None);
        
            // assert - result is faulted with path not found error
            Assert.NotNull(result);
            Assert.True(result.IsFaulted); 
            Assert.IsType<PathNotFoundError>(result.Error);
        }
        finally
        {
            if (File.Exists(mediaPath))
            {
                File.Delete(mediaPath);
            }
        }
    }
    
    private void CreateZipFileWithDirectoriesAndFiles(string path)
    {
        using var stream = File.Open(path, FileMode.Create, FileAccess.ReadWrite);
        using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Create);
        zipArchive.CreateEntry("file1.txt");
        zipArchive.CreateEntry("file2.txt");
        zipArchive.CreateEntry("dir1/file3.txt");
        zipArchive.CreateEntry("dir1/test.txt");
        zipArchive.CreateEntry("dir1/dir2/file4.txt");
    }
}