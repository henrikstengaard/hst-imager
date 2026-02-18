using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
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
            // arrange - create directory and files
            Directory.CreateDirectory(mediaPath);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "test1.txt"), []);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "test2.txt"), []);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "test3.txt"), []);
            
            // arrange - create empty uaefsdb version 1 file
            var uaeFsDbPath = Path.Combine(mediaPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            await File.WriteAllBytesAsync(uaeFsDbPath, []);
            
            // arrange - directory entry iterator
            var directoryEntryIterator = new DirectoryEntryIterator(mediaPath, false, UaeMetadata.UaeFsDb,
                appCache);

            // arrange - initialize directory entry iterator
            await directoryEntryIterator.Initialize();

            // act - iterate entries
            while(await directoryEntryIterator.Next())
            {
            }
            
            // assert - cache add count is equal or greater than 4
            Assert.True(appCache.AddHistory.Count >= 4);
            
            // assert - cache contains uae-metadata entry and nodes and each file entry
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("NODES-LIST:") &&
                                                           x.EndsWith(mediaPath)));

            // assert - cache contains add for test1.txt entry
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("AMIGA-NAME-ENTRY:") &&
                                                           x.EndsWith("test1.txt")));
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("NORMAL-NAME-ENTRY:") &&
                                                           x.EndsWith("test1.txt")));

            // assert - cache contains add for test2.txt entry
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("AMIGA-NAME-ENTRY:") &&
                                                           x.EndsWith("test2.txt")));
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("NORMAL-NAME-ENTRY:") &&
                                                           x.EndsWith("test2.txt")));
            
            // assert - cache contains add for test3.txt entry
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("AMIGA-NAME-ENTRY:") &&
                                                           x.EndsWith("test3.txt")));
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("NORMAL-NAME-ENTRY:") &&
                                                           x.EndsWith("test3.txt")));
            
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
            // arrange - create directory and files
            Directory.CreateDirectory(mediaPath);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "test1.txt"), []);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "test2.txt"), []);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "test3.txt"), []);
            
            // arrange - directory entry iterator
            var directoryEntryIterator = new DirectoryEntryIterator(mediaPath, false, UaeMetadata.UaeFsDb,
                appCache);

            // arrange - initialize directory entry iterator
            await directoryEntryIterator.Initialize();

            // act - iterate entries
            while(await directoryEntryIterator.Next())
            {
            }
            
            // assert - cache add count is equal or greater than 4
            Assert.True(appCache.AddHistory.Count >= 4);
            
            // assert - cache contains uae-metadata entry and nodes and each file entry
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("NODES-LIST:") &&
                                                           x.EndsWith(mediaPath)));

            // assert - cache contains add for test1.txt entry
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("AMIGA-NAME-ENTRY:") &&
                                                           x.EndsWith("test1.txt")));
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("NORMAL-NAME-ENTRY:") &&
                                                           x.EndsWith("test1.txt")));

            // assert - cache contains add for test2.txt entry
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("AMIGA-NAME-ENTRY:") &&
                                                           x.EndsWith("test2.txt")));
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("NORMAL-NAME-ENTRY:") &&
                                                           x.EndsWith("test2.txt")));
            
            // assert - cache contains add for test3.txt entry
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("AMIGA-NAME-ENTRY:") &&
                                                           x.EndsWith("test3.txt")));
            Assert.Equal(1, appCache.AddHistory.Count(x => x.StartsWith("NORMAL-NAME-ENTRY:") &&
                                                           x.EndsWith("test3.txt")));
            
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

    [Theory]
    [InlineData(UaeMetadata.None)]
    [InlineData(UaeMetadata.UaeFsDb)]
    [InlineData(UaeMetadata.UaeMetafile)]
    public async Task When_IteratingLocalDirectoryWithLastWriteDate_Then_EntriesHaveDate(UaeMetadata uaeMetadata)
    {
        // arrange - paths
        var mediaPath = Guid.NewGuid().ToString();
        var dir1Path = Path.Combine(mediaPath, "dir1");
        var file1Path = Path.Combine(mediaPath, "test1.txt");
        var file2Path = Path.Combine(mediaPath, "test2.txt");
        var file3Path = Path.Combine(mediaPath, "test3.txt");
        var dir1Date = new DateTime(2024, 4, 1, 0, 0, 0);
        var file1Date = new DateTime(2024, 4, 2, 0, 0, 0);
        var file2Date = new DateTime(2024, 4, 3, 0, 0, 0);
        var file3Date = new DateTime(2024, 4, 4, 0, 0, 0);

        // arrange - test app cache
        using var appCache = new TestAppCache();
        
        try
        {
            // arrange - create directory and files with different last write times
            Directory.CreateDirectory(mediaPath);
            Directory.CreateDirectory(dir1Path);
            await File.WriteAllBytesAsync(file1Path, []);
            await File.WriteAllBytesAsync(file2Path, []);
            await File.WriteAllBytesAsync(file3Path, []);
            new DirectoryInfo(dir1Path).LastWriteTime = dir1Date;
            new FileInfo(file1Path).LastWriteTime = file1Date;
            new FileInfo(file2Path).LastWriteTime = file2Date;
            new FileInfo(file3Path).LastWriteTime = file3Date;
            
            // arrange - directory entry iterator
            var directoryEntryIterator = new DirectoryEntryIterator(mediaPath, false, uaeMetadata, appCache);

            // arrange - initialize directory entry iterator
            await directoryEntryIterator.Initialize();

            // act - iterate entries
            var entries = new List<Entry>();
            while(await directoryEntryIterator.Next())
            {
                entries.Add(directoryEntryIterator.Current);
            }
            
            // assert - entries count is 4
            Assert.Equal(4, entries.Count);
            
            // assert - dir1 entry has correct date
            var dir1Entry = entries.FirstOrDefault(x => x.Name == "dir1");
            Assert.NotNull(dir1Entry);
            Assert.Equal(dir1Date, dir1Entry.Date);
            
            // assert - file1.txt entry has correct date
            var file1Entry = entries.FirstOrDefault(x => x.Name == "test1.txt");
            Assert.NotNull(file1Entry);
            Assert.Equal(file1Date, file1Entry.Date);
            
            // assert - file2.txt entry has correct date
            var file2Entry = entries.FirstOrDefault(x => x.Name == "test2.txt");
            Assert.NotNull(file2Entry);
            Assert.Equal(file2Date, file2Entry.Date);
            
            // assert - file3.txt entry has correct date
            var file3Entry = entries.FirstOrDefault(x => x.Name == "test3.txt");
            Assert.NotNull(file3Entry);
            Assert.Equal(file3Date, file3Entry.Date);
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