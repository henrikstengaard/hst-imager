using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.UaeMetadatas;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsDelCommandWithLocalDirectory
{
    [Fact]
    public async Task When_DeletingAFile_Then_FileIsDeleted()
    {
        // arrange - paths
        var mediaPath = Guid.NewGuid().ToString();
        var deletePath = Path.Combine(mediaPath, "dir1", "file1.txt");
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();
            
            // arrange - create local directory with directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            
            // arrange - create fs del command
            var fsDelCommand = new FsDelCommand(new NullLogger<FsDelCommand>(), testCommandHelper, [],
                deletePath, uaeMetadata);
            
            // act - execute fs del command
            var result = await fsDelCommand.Execute(CancellationToken.None);
            
            // assert - result is success
            Assert.True(result.IsSuccess);
            
            // arrange - clear active medias to ensure changes to media is flushed
            testCommandHelper.ClearActiveMedias();
            
            // assert - file1.txt is deleted and dir3 exists in dir1
            var entries = Directory.GetFileSystemEntries(Path.Combine(mediaPath, "dir1"), "*.*", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x)
                .ToList();
            Assert.Single(entries);
            string[] expectedEntries =
            [
                Path.Combine(mediaPath, "dir1", "dir3")
            ];
            Assert.Equal(expectedEntries, entries);
        }
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_DeletingASubDirectory_Then_DirectoryIsDeleted()
    {
        // arrange - paths
        var mediaPath = Guid.NewGuid().ToString();
        var deletePath = Path.Combine(mediaPath, "dir1", "dir3");
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create local directory with directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            
            // arrange - create fs del command
            var fsDelCommand = new FsDelCommand(new NullLogger<FsDelCommand>(), testCommandHelper, [],
                deletePath, uaeMetadata);
            
            // act - execute fs del command
            var result = await fsDelCommand.Execute(CancellationToken.None);
            
            // assert - result is success
            Assert.True(result.IsSuccess);
            
            // arrange - clear active medias to ensure changes to media is flushed
            testCommandHelper.ClearActiveMedias();
            
            // assert - file1.txt is deleted and dir3 exists in dir1
            var entries = Directory.GetFileSystemEntries(Path.Combine(mediaPath, "dir1"), "*.*", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x)
                .ToList();
            Assert.Single(entries);
            string[] expectedEntries =
            [
                Path.Combine(mediaPath, "dir1", "file1.txt")
            ];
            Assert.Equal(expectedEntries, entries);
        }
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_DeletingADirectory_Then_DirectoryIsDeleted()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var deletePath = Path.Combine(mediaPath, "dir1");
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create local directory with directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);

            // arrange - create fs del command
            var fsDelCommand = new FsDelCommand(new NullLogger<FsDelCommand>(), testCommandHelper, [],
                deletePath, uaeMetadata);
            
            // act - execute fs del command
            var result = await fsDelCommand.Execute(CancellationToken.None);
            
            // assert - result is success
            Assert.True(result.IsSuccess);

            // arrange - clear active medias to ensure changes to media is flushed
            testCommandHelper.ClearActiveMedias();
            
            // assert - dir1 is deleted and dir2 exists in root
            var entries = Directory.GetFileSystemEntries(mediaPath, "*.*", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x)
                .ToList();
            Assert.Single(entries);
            string[] expectedEntries =
            [
                Path.Combine(mediaPath, "dir2")
            ];
            Assert.Equal(expectedEntries, entries);
        }
        finally
        {
            TestHelper.DeletePaths(mediaPath);
        }
    }
}