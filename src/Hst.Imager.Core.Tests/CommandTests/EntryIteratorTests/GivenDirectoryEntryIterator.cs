using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.UaeMetadatas;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.EntryIteratorTests;

public class GivenDirectoryEntryIterator
{
    [Fact]
    public async Task When_IteratingLocalDirectoryWithEmptyUaeFsDbVersion1File_Then_CachedUaeMetadata()
    {
        // arrange - paths
        var mediaPath = Guid.NewGuid().ToString();

        // arrange - test app cache
        using var appCache = new TestAppCache();
        
        try
        {
            // arrange - create test files
            Directory.CreateDirectory(mediaPath);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "test1.txt"), []);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "test2.txt"), []);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "test3.txt"), []);
            
            // arrange - create empty uaefsdb version 1 file
            var uaeFsDbPath = Path.Combine(mediaPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            await File.WriteAllBytesAsync(uaeFsDbPath, []);
            
            // arrange - directory entry iterator
            var directoryEntryIterator = new DirectoryEntryIterator(mediaPath, false, appCache);
            directoryEntryIterator.UaeMetadata = UaeMetadata.UaeFsDb;

            // arrange - initialize directory entry iterator
            await directoryEntryIterator.Initialize();

            // act - iterate entries
            while(await directoryEntryIterator.Next())
            {
            }
            
            // assert - cache add count is 4
            Assert.Equal(4, appCache.AddHistory.Count);
            
            // assert - cache contains uae-metadata entry and nodes and each file entry
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("UAE-METADATA-NODES:")));
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("ENTRY:test1.txt")));
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("ENTRY:test2.txt")));
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("ENTRY:test3.txt")));
            
            // assert - cache get count is greater than add count
            Assert.True(appCache.GetHistory.Count > appCache.AddHistory.Count);
        }
        finally
        {
            if (Directory.Exists(mediaPath))
            {
                Directory.Delete(mediaPath, true);
            }
        }
        
    }

    [Fact]
    public async Task When_IteratingLocalDirectoryWithoutAnyUaeFsDbMetadata_Then_CachedUaeMetadata()
    {
        // arrange - paths
        var mediaPath = Guid.NewGuid().ToString();

        // arrange - test app cache
        using var appCache = new TestAppCache();
        
        try
        {
            // arrange - create test files
            Directory.CreateDirectory(mediaPath);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "test1.txt"), []);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "test2.txt"), []);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "test3.txt"), []);
            
            // arrange - directory entry iterator
            var directoryEntryIterator = new DirectoryEntryIterator(mediaPath, false, appCache);
            directoryEntryIterator.UaeMetadata = UaeMetadata.UaeFsDb;

            // arrange - initialize directory entry iterator
            await directoryEntryIterator.Initialize();

            // act - iterate entries
            while(await directoryEntryIterator.Next())
            {
            }
            
            // assert - cache add count is 4
            Assert.Equal(4, appCache.AddHistory.Count);
            
            // assert - cache contains uae-metadata entry and nodes and each file entry
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("UAE-METADATA-NODES:")));
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("ENTRY:test1.txt")));
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("ENTRY:test2.txt")));
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("ENTRY:test3.txt")));
            
            // assert - cache get count is greater than add count
            Assert.True(appCache.GetHistory.Count > appCache.AddHistory.Count);
        }
        finally
        {
            if (Directory.Exists(mediaPath))
            {
                Directory.Delete(mediaPath, true);
            }
        }
    }
}