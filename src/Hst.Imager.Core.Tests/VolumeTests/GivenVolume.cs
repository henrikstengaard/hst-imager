using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Xunit;

namespace Hst.Imager.Core.Tests.VolumeTests;

public class GivenVolume
{
    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_CreatingDirectoriesAndFiles_Then_EntriesAreListedAndFilesCanBeRead(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);

        // arrange - create directories and files
        await context.CreateDirectoriesAndFiles();

        // act - reopen and list root and dir1 entries
        context.Reopen();
        var (volume, root) = await context.Open();
        var rootEntriesResult = await volume.ListEntries(root);
        var dir1EntriesResult = await volume.ListEntries([..root, "dir1"]);

        // assert - root entries are listed
        Assert.True(rootEntriesResult.IsSuccess);
        var rootEntries = rootEntriesResult.Value.ToList();
        Assert.Equal(["dir1", "file1.txt", "file2.bin"],
            rootEntries.Select(x => x.Name.ToLowerInvariant()).OrderBy(x => x).ToArray());
        Assert.Equal(EntryType.Dir, rootEntries.First(x => x.Name.ToLowerInvariant() == "dir1").Type);
        Assert.All(rootEntries, x => Assert.Equal([..root, x.Name], x.FullPathComponents));

        // assert - dir1 entries are listed
        Assert.True(dir1EntriesResult.IsSuccess);
        Assert.Equal(["dir2", "file2.txt"],
            dir1EntriesResult.Value.Select(x => x.Name.ToLowerInvariant()).OrderBy(x => x).ToArray());

        // assert - files can be read
        Assert.Equal("file1", await VolumeTestContext.ReadFile(volume, [..root, "file1.txt"]));
        Assert.Equal("dir2 file3", await VolumeTestContext.ReadFile(volume, [..root, "dir1", "dir2", "file3.txt"]));
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_GettingEntries_Then_DirAndFileAreReturnedAndMissingIsPathNotFound(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();
        var (volume, root) = await context.Open();

        // act - get entries
        var dirResult = await volume.GetEntry([..root, "dir1"]);
        var fileResult = await volume.GetEntry([..root, "dir1", "file2.txt"]);
        var missingResult = await volume.GetEntry([..root, "dir1", "missing.txt"]);
        var missingDirResult = await volume.GetEntry([..root, "missing", "file.txt"]);

        // assert - dir and file entries are returned
        Assert.True(dirResult.IsSuccess);
        Assert.Equal(EntryType.Dir, dirResult.Value.Type);
        Assert.True(fileResult.IsSuccess);
        Assert.Equal(EntryType.File, fileResult.Value.Type);
        Assert.Equal(10, fileResult.Value.Size);

        // assert - missing entries returns path not found error
        Assert.True(missingResult.IsFaulted);
        Assert.IsType<PathNotFoundError>(missingResult.Error);
        Assert.True(missingDirResult.IsFaulted);
        Assert.IsType<PathNotFoundError>(missingDirResult.Error);
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_WritingExistingFileWithoutOverwrite_Then_FileExistsErrorIsReturned(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();
        var (volume, root) = await context.Open();

        // act - write existing file without and with overwrite
        using var stream1 = new MemoryStream(Encoding.ASCII.GetBytes("new"));
        var writeResult = await volume.WriteFile([..root, "file1.txt"], stream1, false);
        using var stream2 = new MemoryStream(Encoding.ASCII.GetBytes("new"));
        var overwriteResult = await volume.WriteFile([..root, "file1.txt"], stream2, true);

        // assert - file exists error is returned without overwrite
        Assert.True(writeResult.IsFaulted);
        Assert.IsType<FileExistsError>(writeResult.Error);

        // assert - file is overwritten with overwrite
        Assert.True(overwriteResult.IsSuccess);
        Assert.Equal("new", await VolumeTestContext.ReadFile(volume, [..root, "file1.txt"]));
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_DeletingFileAndMovingDirectory_Then_EntriesAreUpdated(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();
        var (volume, root) = await context.Open();

        // act - delete file and move dir2 to root as dir3
        await VolumeTestContext.AssertSuccess(volume.Delete([..root, "file2.bin"]));
        await VolumeTestContext.AssertSuccess(volume.Move([..root, "dir1", "dir2"], [..root, "dir3"], false));
        await volume.Flush();

        // assert - root entries are updated
        var rootEntriesResult = await volume.ListEntries(root);
        Assert.True(rootEntriesResult.IsSuccess);
        Assert.Equal(["dir1", "dir3", "file1.txt"],
            rootEntriesResult.Value.Select(x => x.Name.ToLowerInvariant()).OrderBy(x => x).ToArray());

        // assert - moved file can be read
        Assert.Equal("dir2 file3", await VolumeTestContext.ReadFile(volume, [..root, "dir3", "file3.txt"]));
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_CreatingDirectoryInMissingParent_Then_PathNotFoundErrorIsReturned(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        var (volume, root) = await context.Open();

        // act - create directory in missing parent
        var result = await volume.CreateDirectory([..root, "missing", "dir"]);

        // assert - path not found error is returned
        Assert.True(result.IsFaulted);
        Assert.IsType<PathNotFoundError>(result.Error);
    }
}
