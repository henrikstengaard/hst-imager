using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.UaeMetadatas;

namespace Hst.Imager.Core.Tests.VolumeTests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Commands;
using Models.FileSystems;
using Hst.Imager.Core.Tests.CommandTests;
using Hst.Imager.Core.Volumes;
using Xunit;

public class GivenLocalDirectoryVolumeTarget : FsCommandTestBase
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
            
            // arrange - test app cache and uae metadata helper
            using var appCache = new TestAppCache();
            var uaeMetadataHelper = new UaeMetadataHelper(appCache);
            
            // arrange - create local directory volume target
            var localDirectoryMedia = await MediaHelper.CreateLocalDirectoryMediaFromPath(path,
                UaeMetadata.None, uaeMetadataHelper);
            var localDirectoryVolume = new LocalDirectoryVolume(localDirectoryMedia, uaeMetadataHelper) { UaeMetadata = UaeMetadata.None };
            var writer = new VolumeTarget(localDirectoryVolume, LocalDirectoryVolume.ToPathComponents(path), false,
                false, false);

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
    public async Task When_InitializedWithOnePathComponentNotExisting_Then_VolumeTargetIsInitialized(bool fullPath)
    {
        // arrange - path for volume target
        var path = Guid.NewGuid().ToString();
        
        try
        {
            // arrange - test app cache and uae metadata helper
            using var appCache = new TestAppCache();
            var uaeMetadataHelper = new UaeMetadataHelper(appCache);
            
            // arrange - create local directory volume target
            var localDirectoryPath = fullPath ? Path.GetFullPath(path) : path;
            var localDirectoryMedia = await MediaHelper.CreateLocalDirectoryMediaFromPath(
                localDirectoryPath, UaeMetadata.None, uaeMetadataHelper);
            var localDirectoryVolume = new LocalDirectoryVolume(localDirectoryMedia, uaeMetadataHelper) { UaeMetadata = UaeMetadata.None };
            var writer = new VolumeTarget(localDirectoryVolume, LocalDirectoryVolume.ToPathComponents(localDirectoryPath), false,
                false, false);

            // act - initialize the writer
            var initializeResult = await writer.Initialize();

            // assert - local directory volume target is initialized
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
        // arrange - path for volume target
        var path = Path.Combine(Guid.NewGuid().ToString(), "dir");
        
        try
        {
            // arrange - test app cache and uae metadata helper
            using var appCache = new TestAppCache();
            var uaeMetadataHelper = new UaeMetadataHelper(appCache);
            
            // arrange - create local directory volume target
            var localDirectoryPath = fullPath ? Path.GetFullPath(path) : path;
            var localDirectoryMedia = await MediaHelper.CreateLocalDirectoryMediaFromPath(
                localDirectoryPath, UaeMetadata.None, uaeMetadataHelper);
            var localDirectoryVolume = new LocalDirectoryVolume(localDirectoryMedia, uaeMetadataHelper) { UaeMetadata = UaeMetadata.None };
            var writer = new VolumeTarget(localDirectoryVolume, LocalDirectoryVolume.ToPathComponents(localDirectoryPath), false,
                false, false);

            // act - initialize the writer
            var initializeResult = await writer.Initialize();

            // assert - local directory volume target returns error
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