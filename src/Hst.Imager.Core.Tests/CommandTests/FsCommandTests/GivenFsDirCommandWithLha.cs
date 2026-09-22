using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Compression.Lha;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Hst.Imager.Core.PathComponents;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsDirCommandWithLha
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
        var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
        var mediaPath = $"{Guid.NewGuid()}.lha";
        var dirPath = Path.Combine(new[]{mediaPath}.Concat(MediaPath.GenericMediaPath.Split(path)).ToArray());
        
        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - copy lha test data to media path
            File.Copy(lhaPath, mediaPath, true);
            
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
        var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
        var mediaPath = $"{Guid.NewGuid()}.lha";
        var dirPath = Path.Combine(new[]{mediaPath}.Concat(MediaPath.GenericMediaPath.Split(path)).ToArray());
        const bool recursive = false;

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - copy lha test data to media path
            File.Copy(lhaPath, mediaPath, true);
            
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
        var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
        var mediaPath = $"{Guid.NewGuid()}.lha";
        var dirPath = Path.Combine(new[]{mediaPath}.Concat(MediaPath.GenericMediaPath.Split(path)).ToArray());
        const bool recursive = true;

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - copy lha test data to media path
            File.Copy(lhaPath, mediaPath, true);
            
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
        var lhaPath = Path.Combine("TestData", "Lha", "amiga.lha");
        var mediaPath = $"{Guid.NewGuid()}.lha";
        var dirPath = Path.Combine(new[]{mediaPath}.Concat(MediaPath.GenericMediaPath.Split(path)).ToArray());

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - copy lha test data to media path
            File.Copy(lhaPath, mediaPath, true);
            
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

    [Fact]
    public async Task When_ListingEntriesWithoutFilename_Then_EntriesAreSkipped()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.lha";
        const bool recursive = true;

        // arrange - create lha entry without filename
        var lhaData = new byte[24];
        lhaData[0] = 22; // header size (entry length -2, without header size and checksum bytes)
        lhaData[1] = 0; // checksum, calculated
        lhaData[2] = 0x2d; // method byte 1 '-'
        lhaData[3] = 0x6c; // method byte 2 'l'
        lhaData[4] = 0x68; // method byte 3 'h'
        lhaData[5] = 0x30; // method byte 4 '0'
        lhaData[6] = 0x2d; // method byte 5 '-'
        lhaData[7] = 0; // packed size 1
        lhaData[8] = 0; // packed size 2
        lhaData[9] = 0; // packed size 3
        lhaData[10] = 0; // packed size 4
        lhaData[11] = 0; // original size 1
        lhaData[12] = 0; // original size 2
        lhaData[13] = 0; // original size 3
        lhaData[14] = 0; // original size 4
        lhaData[15] = 0x52; // modified unix timestamp 1
        lhaData[16] = 0x73; // modified unix timestamp 2
        lhaData[17] = 0x48; // modified unix timestamp 3
        lhaData[18] = 0; // modified unix timestamp 4
        lhaData[19] = 0; // attribute
        lhaData[20] = 0; // header level 0
        lhaData[21] = 0; // name length
        lhaData[22] = 0; // extend header 1, unused for unit test
        lhaData[23] = 0; // extend header 2, unused for unit test

        // arrange - calculate checksum for lha entry
        lhaData[1] = (byte)ChecksumHelper.CalcSum(lhaData, 2, lhaData.Length - 2);
        
        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - write lha data
            await File.WriteAllBytesAsync(mediaPath, lhaData);
            
            // arrange - create fs dir command
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                mediaPath, recursive);
            EntriesInfo entriesInfo = null;
            fsDirCommand.EntriesRead += (_, args) =>
            {
                entriesInfo = args.EntriesInfo;
            };

            // act - execute fs dir command
            var result = await fsDirCommand.Execute(CancellationToken.None);
            
            // assert - result is success
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            
            // assert - no entries are returned / entries are skipped
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

    [Fact]
    public async Task When_ListingEntriesWithFilename_Then_EntriesAreListed()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.lha";
        const bool recursive = true;

        // arrange - create lha entry with filename
        var lhaData = new byte[28];
        lhaData[0] = 26; // header size (entry length -2, without header size and checksum bytes)
        lhaData[1] = 0; // checksum, calculated
        lhaData[2] = 0x2d; // method byte 1 '-'
        lhaData[3] = 0x6c; // method byte 2 'l'
        lhaData[4] = 0x68; // method byte 3 'h'
        lhaData[5] = 0x30; // method byte 4 '0'
        lhaData[6] = 0x2d; // method byte 5 '-'
        lhaData[7] = 0; // packed size 1
        lhaData[8] = 0; // packed size 2
        lhaData[9] = 0; // packed size 3
        lhaData[10] = 0; // packed size 4
        lhaData[11] = 0; // original size 1
        lhaData[12] = 0; // original size 2
        lhaData[13] = 0; // original size 3
        lhaData[14] = 0; // original size 4
        lhaData[15] = 0x52; // modified unix timestamp 1
        lhaData[16] = 0x73; // modified unix timestamp 2
        lhaData[17] = 0x48; // modified unix timestamp 3
        lhaData[18] = 0; // modified unix timestamp 4
        lhaData[19] = 0; // attribute
        lhaData[20] = 0; // header level 0
        lhaData[21] = 4; // name length
        lhaData[22] = 0x66; // name 1 'f'
        lhaData[23] = 0x69; // name 2 'i'
        lhaData[24] = 0x6c; // name 3 'l'
        lhaData[25] = 0x65; // name 4 'e'
        lhaData[26] = 0; // extend header 1, unused for unit test
        lhaData[27] = 0; // extend header 2, unused for unit test

        // arrange - calculate checksum for lha entry
        lhaData[1] = (byte)ChecksumHelper.CalcSum(lhaData, 2, lhaData.Length - 2);
        
        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - write lha data
            await File.WriteAllBytesAsync(mediaPath, lhaData);
            
            // arrange - create fs dir command
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                mediaPath, recursive);
            EntriesInfo entriesInfo = null;
            fsDirCommand.EntriesRead += (_, args) =>
            {
                entriesInfo = args.EntriesInfo;
            };

            // act - execute fs dir command
            var result = await fsDirCommand.Execute(CancellationToken.None);
            
            // assert - result is success
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            
            // assert - 1 entry is returned
            Assert.NotNull(entriesInfo);
            Assert.Single(entriesInfo.Entries);
            var fileEntry = entriesInfo.Entries.FirstOrDefault(x => x.Type == EntryType.File && x.Name == "file");
            Assert.NotNull(fileEntry);
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