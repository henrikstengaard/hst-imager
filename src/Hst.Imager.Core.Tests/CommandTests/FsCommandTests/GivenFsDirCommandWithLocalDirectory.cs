using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Hst.Amiga.FileSystems;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.UaeMetadatas;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsDirCommandWithLocalDirectory
{
    [Fact]
    public async Task When_ListingEntriesInNonExistingDirectory_Then_ErrorIsReturned()
    {
        // arrange - paths
        var dirPath = $"local_{Guid.NewGuid()}";
        const bool recursive = false;

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();
        
        // arrange - create fs dir command
        var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(),
            dirPath, recursive);

        // act - execute fs dir command
        var result = await fsDirCommand.Execute(CancellationToken.None);
        
        // assert - result is faulted
        Assert.NotNull(result);
        Assert.True(result.IsFaulted);
        
        // assert - error is path not found error
        Assert.IsType<PathNotFoundError>(result.Error);
    }
    
    [Fact]
    public async Task When_ListingEntriesInDirectoryWithUaeFsDbMetadata_Then_EntriesAreReturnedUsingUaeMetadata()
    {
        // arrange - paths
        var localPath = $"local_{Guid.NewGuid()}";
        const bool recursive = false;
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create local directory
            Directory.CreateDirectory(localPath);
            
            // arrange - create safe and normal names for file1
            var file1SafeName = UaeFsDbNodeHelper.MakeSafeFilename("file1ß");
            var file1NormalName = UaeFsDbNodeHelper.CreateUniqueNormalName(localPath, file1SafeName);
            
            // arrange - create file1 in local directory
            await File.WriteAllBytesAsync(Path.Combine(localPath, file1NormalName), []);
            
            // arrange - create uaefsdb version 1 nodes for file1 in local directory
            var uaeFsDbPath = Path.Combine(localPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            using (var uaeFsDbStream = File.OpenWrite(uaeFsDbPath))
            {
                await uaeFsDbStream.WriteBytes(UaeFsDbWriter.Build(new UaeFsDbNode
                {
                    Version = UaeFsDbNode.NodeVersion.Version1,
                    Valid = 1,
                    AmigaName = "file1ß",
                    NormalName = file1NormalName,
                    Mode = ProtectionBitsConverter.ToProtectionValue(ProtectionBits.Read | ProtectionBits.Script),
                    Comment = "file1ß comment"
                }));
            }

            // arrange - create fs dir command
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), localPath, recursive, uaeMetadata: uaeMetadata);
            
            // arrange - capture entries read event
            EntriesInfo entriesInfo = null;
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var result = await fsDirCommand.Execute(CancellationToken.None);
        
            // assert - result is successful
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            
            // assert - entries info is captured
            Assert.NotNull(entriesInfo);
            
            // assert - single entry is returned with correct name from uae fs db metadata
            var entries = entriesInfo.Entries.ToList();
            Assert.Single(entries);
            var entry = entries[0];
            Assert.Equal("file1ß", entry.Name);
        }
        finally
        {
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
            }
        }
    }

    [Fact]
    public async Task When_ListingSingleEntryInDirectoryWithUaeFsDbMetadata_Then_EntriesAreReturnedUsingUaeMetadata()
    {
        // arrange - paths
        var localPath = $"local_{Guid.NewGuid()}";
        var dirPath = Path.Combine(localPath, "file1ß");
        const bool recursive = false;
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create local directory
            Directory.CreateDirectory(localPath);

            // arrange - create dir1 in local directory
            var dir1Path = Path.Combine(localPath, "dir1");
            Directory.CreateDirectory(dir1Path);
            
            // arrange - create safe and normal names for file1
            var file1SafeName = UaeFsDbNodeHelper.MakeSafeFilename("file1ß");
            var file1NormalName = UaeFsDbNodeHelper.CreateUniqueNormalName(localPath, file1SafeName);
            
            // arrange - create file1 in local directory
            await File.WriteAllBytesAsync(Path.Combine(localPath, file1NormalName), []);
            
            // arrange - create uaefsdb version 1 nodes for file1 in local directory
            var uaeFsDbPath = Path.Combine(localPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            using (var uaeFsDbStream = File.OpenWrite(uaeFsDbPath))
            {
                await uaeFsDbStream.WriteBytes(UaeFsDbWriter.Build(new UaeFsDbNode
                {
                    Version = UaeFsDbNode.NodeVersion.Version1,
                    Valid = 1,
                    AmigaName = "file1ß",
                    NormalName = file1NormalName,
                    Mode = ProtectionBitsConverter.ToProtectionValue(ProtectionBits.Read | ProtectionBits.Script),
                    Comment = "file1ß comment"
                }));
            }

            // arrange - create fs dir command
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), dirPath, recursive, uaeMetadata: uaeMetadata);
            
            // arrange - capture entries read event
            EntriesInfo entriesInfo = null;
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var result = await fsDirCommand.Execute(CancellationToken.None);
        
            // assert - result is successful
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            
            // assert - entries info is captured
            Assert.NotNull(entriesInfo);
            
            // assert - single entry is returned with correct name from uae fs db metadata
            var entries = entriesInfo.Entries.ToList();
            Assert.Single(entries);
            var entry = entries[0];
            Assert.Equal("file1ß", entry.Name);
        }
        finally
        {
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
            }
        }
    }

    [Fact]
    public async Task When_ListingSingleEntryInSubDirectoryWithUaeFsDbMetadata_Then_EntriesAreReturnedUsingUaeMetadata()
    {
        // arrange - paths
        var localPath = $"local_{Guid.NewGuid()}";
        var dirPath = Path.Combine(localPath, "dir1ß", "file1ß");
        const bool recursive = false;
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create local directory
            Directory.CreateDirectory(localPath);

            // arrange - create safe and normal names for dir1
            var dir1SafeName = UaeFsDbNodeHelper.MakeSafeFilename("dir1ß");
            var dir1NormalName = UaeFsDbNodeHelper.CreateUniqueNormalName(localPath, dir1SafeName);
            
            // arrange - create dir1 in local directory
            var dir1Path = Path.Combine(localPath, dir1NormalName);
            Directory.CreateDirectory(dir1Path);
            
            // arrange - create uaefsdb version 1 nodes for dir1 in local directory
            var localUaeFsDbPath = Path.Combine(localPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            using (var localUaeFsDbStream = File.OpenWrite(localUaeFsDbPath))
            {
                await localUaeFsDbStream.WriteBytes(UaeFsDbWriter.Build(new UaeFsDbNode
                {
                    Version = UaeFsDbNode.NodeVersion.Version1,
                    Valid = 1,
                    AmigaName = "dir1ß",
                    NormalName = dir1NormalName,
                    Mode = ProtectionBitsConverter.ToProtectionValue(ProtectionBits.Read | ProtectionBits.Script),
                    Comment = "dir1ß comment"
                }));
            }
            
            // arrange - create safe and normal names for file1
            var file1SafeName = UaeFsDbNodeHelper.MakeSafeFilename("file1ß");
            var file1NormalName = UaeFsDbNodeHelper.CreateUniqueNormalName(dir1Path, file1SafeName);
            
            // arrange - create file1 in local directory
            await File.WriteAllBytesAsync(Path.Combine(dir1Path, file1NormalName), []);
            
            // arrange - create uaefsdb version 1 nodes for file1 in dir1 directory
            var dir1UaeFsDbPath = Path.Combine(dir1Path, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            using (var dir1UaeFsDbStream = File.OpenWrite(dir1UaeFsDbPath))
            {
                await dir1UaeFsDbStream.WriteBytes(UaeFsDbWriter.Build(new UaeFsDbNode
                {
                    Version = UaeFsDbNode.NodeVersion.Version1,
                    Valid = 1,
                    AmigaName = "file1ß",
                    NormalName = file1NormalName,
                    Mode = ProtectionBitsConverter.ToProtectionValue(ProtectionBits.Read | ProtectionBits.Script),
                    Comment = "file1ß comment"
                }));
            }

            // arrange - create fs dir command
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), dirPath, recursive, uaeMetadata: uaeMetadata);
            
            // arrange - capture entries read event
            EntriesInfo entriesInfo = null;
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var result = await fsDirCommand.Execute(CancellationToken.None);
        
            // assert - result is successful
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            
            // assert - entries info is captured
            Assert.NotNull(entriesInfo);
            
            // assert - single entry is returned with correct name from uae fs db metadata
            var entries = entriesInfo.Entries.ToList();
            Assert.Single(entries);
            var entry = entries[0];
            Assert.Equal("file1ß", entry.Name);
        }
        finally
        {
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
            }
        }
    }
    
    [Fact]
    public async Task When_ListingSingleEntryInSubDirectoryWithoutUaeMetadata_Then_ErrorIsReturned()
    {
        // arrange - paths
        var localPath = $"local_{Guid.NewGuid()}";
        var dirPath = Path.Combine(localPath, "dir1ß");
        const bool recursive = false;
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create local directory
            Directory.CreateDirectory(localPath);

            // arrange - create safe and normal names for dir1
            var dir1SafeName = UaeFsDbNodeHelper.MakeSafeFilename("dir1ß");
            var dir1NormalName = UaeFsDbNodeHelper.CreateUniqueNormalName(localPath, dir1SafeName);
            
            // arrange - create dir1 in local directory
            var dir1Path = Path.Combine(localPath, dir1NormalName);
            Directory.CreateDirectory(dir1Path);
            
            // arrange - create fs dir command
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), dirPath, recursive, uaeMetadata: uaeMetadata);
            
            // act - execute fs dir command
            var result = await fsDirCommand.Execute(CancellationToken.None);
        
            // assert - result is successful
            Assert.NotNull(result);
            Assert.True(result.IsFaulted);
            
            // assert - error is path not found error
            Assert.IsType<PathNotFoundError>(result.Error);
        }
        finally
        {
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
            }
        }
    }
}