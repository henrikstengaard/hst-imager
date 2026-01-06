using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Hst.Imager.Core.PathComponents;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsDirCommandWithLzx
{
    [Theory]
    [InlineData("", true)]
    [InlineData("", false)]
    [InlineData("*", true)]
    [InlineData("*", false)]
    [InlineData("test.txt", true)]
    [InlineData("test.txt", false)]
    [InlineData("test1", true)]
    [InlineData("test1", false)]
    [InlineData("test1\\t*", true)]
    [InlineData("test1\\t*", false)]
    [InlineData("test1\\test1.txt", true)]
    [InlineData("test1\\test1.txt", false)]
    [InlineData("test1\\test2", true)]
    [InlineData("test1\\test2", false)]
    [InlineData("test1\\test2\\*", true)]
    [InlineData("test1\\test2\\*", false)]
    public async Task When_ListingEntriesInExisting_Then_EntriesAreListed(string path, bool recursive)
    {
        // arrange - paths
        var lzxPath = Path.Combine("TestData", "Lzx", "amiga.lzx");
        var mediaPath = $"{Guid.NewGuid()}.lzx";
        var dirPath = Path.Combine(new[]{mediaPath}.Concat(MediaPath.GenericMediaPath.Split(path)).ToArray());
        
        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - copy lzx test data to media path
            File.Copy(lzxPath, mediaPath, true);
            
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
    [InlineData("test1\\e*")]
    public async Task When_ListingEntriesInExistingDirectoryWithPatternNotMatching_Then_NoEntriesAreListed(string path)
    {
        // arrange - paths
        var lzxPath = Path.Combine("TestData", "Lzx", "amiga.lzx");
        var mediaPath = $"{Guid.NewGuid()}.lzx";
        var dirPath = Path.Combine(new[]{mediaPath}.Concat(MediaPath.GenericMediaPath.Split(path)).ToArray());
        const bool recursive = false;

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - copy lzx test data to media path
            File.Copy(lzxPath, mediaPath, true);
            
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
    [InlineData("test1\\es*")]
    public async Task When_ListingEntriesInExistingDirectoryWithPatternNotMatchingRecursively_Then_NoEntriesAreListed(string path)
    {
        // arrange - paths
        var lzxPath = Path.Combine("TestData", "Lzx", "amiga.lzx");
        var mediaPath = $"{Guid.NewGuid()}.lzx";
        var dirPath = Path.Combine(new[]{mediaPath}.Concat(MediaPath.GenericMediaPath.Split(path)).ToArray());
        const bool recursive = true;

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - copy lzx test data to media path
            File.Copy(lzxPath, mediaPath, true);
            
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
    [InlineData("test1\\e", true)]
    [InlineData("test1\\e", false)]
    public async Task When_ListingEntriesInNonExistingDirectory_Then_ErrorIsReturned(string path, bool recursive)
    {
        // arrange - paths
        var lzxPath = Path.Combine("TestData", "Lzx", "amiga.lzx");
        var mediaPath = $"{Guid.NewGuid()}.lzx";
        var dirPath = Path.Combine(new[]{mediaPath}.Concat(MediaPath.GenericMediaPath.Split(path)).ToArray());

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - copy lzx test data to media path
            File.Copy(lzxPath, mediaPath, true);
            
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
}