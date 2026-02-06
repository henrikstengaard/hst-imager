namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Commands;
using Models.FileSystems;
using Xunit;

public class GivenDirectoryEntryWriter : FsCommandTestBase
{
    [Fact]
    public async Task WhenWriteEntryWithSpaceInFilenameThenSpaceInPreserved()
    {
        // arrange - entry to write
        var entry = new Entry
        {
            Name = "File 1",
            Size = 0,
            Date = DateTime.Now,
            Type = EntryType.File,
            RawPath = "File 1",
            FormattedName = "File 1",
            RelativePathComponents = new []{ "File 1" },
            FullPathComponents = new []{ "File 1" }
        };

        // arrange - path to write entry to
        var path = $"{Guid.NewGuid()}";
        
        try
        {
            // arrange - create directory
            Directory.CreateDirectory(path);
            
            // arrange - create app cache
            using var appCache = new TestAppCache();
            
            // arrange - create directory entry writer
            var writer = new DirectoryEntryWriter(path, false, false, false, appCache);

            // arrange - initialize the writer
            var initializeResult = await writer.Initialize();
            Assert.True(initializeResult.IsSuccess);
            
            // act - write directory entry
            var t =await writer.CreateFile(entry, entry.RelativePathComponents, new MemoryStream(), false, false);
            
            // assert - get written files
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);

            // assert - 1 file was written
            Assert.Single(files);
            
            // assert - file 1 was written
            var file1Path = Path.Combine(path, "File 1");
            Assert.Equal(file1Path,
                files.FirstOrDefault(x => x.Equals(file1Path, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_InitializedWithOnePathComponentNotExisting_Then_DirectoryEntryWriterIsInitialized(bool fullPath)
    {
        // arrange - path for directory entry writer
        var path = Guid.NewGuid().ToString();
        
        try
        {
            // arrange - create app cache
            using var appCache = new TestAppCache();
            
            // arrange - create directory entry writer
            var writer = new DirectoryEntryWriter(fullPath ? Path.GetFullPath(path) : path, false, 
                false, false, appCache);

            // act - initialize the writer
            var initializeResult = await writer.Initialize();

            // assert - directory entry writer is initialized
            Assert.True(initializeResult.IsSuccess);
        }
        finally
        {
            DeletePaths(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_InitializedWithTwoPathComponentsNotExisting_Then_ErrorIsReturned(bool fullPath)
    {
        // arrange - path for directory entry writer
        var path = Path.Combine(Guid.NewGuid().ToString(), "dir");
        
        try
        {
            // arrange - create app cache
            using var appCache = new TestAppCache();
            
            // arrange - create directory entry writer
            var writer = new DirectoryEntryWriter(fullPath ? Path.GetFullPath(path) : path, false, 
                false, false, appCache);

            // act - initialize the writer
            var initializeResult = await writer.Initialize();

            // assert - directory entry writer returns error
            Assert.True(initializeResult.IsFaulted);
            Assert.False(initializeResult.IsSuccess);
            Assert.IsType<PathNotFoundError>(initializeResult.Error);
        }
        finally
        {
            DeletePaths(path);
        }
    }
}