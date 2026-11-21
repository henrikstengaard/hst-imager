using System.Linq;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Hst.Imager.Core.Volumes;
using Xunit;

namespace Hst.Imager.Core.Tests.VolumeTests;

public class GivenVolumeEntryWalker
{
    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_WalkingRootNonRecursive_Then_DirectoriesBeforeFilesAreReturned(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();
        var (volume, root) = await context.Open(VolumeAccess.Read);

        // act - walk root non-recursive
        var walkerResult = await VolumeEntryWalker.Create(volume, root, false);
        Assert.True(walkerResult.IsSuccess);
        var entries = await VolumeTestContext.ReadAll(walkerResult.Value);

        // assert - directories are returned before files ordered by name
        Assert.Equal(["dir1", "file1.txt", "file2.bin"],
            VolumeTestContext.RelativePaths(entries).Select(x => x.ToLowerInvariant()).ToArray());
        Assert.Equal([EntryType.Dir, EntryType.File, EntryType.File], entries.Select(x => x.Type).ToArray());
        Assert.False(walkerResult.Value.IsSingleFileEntryNext);
        Assert.False(walkerResult.Value.UsesPattern);
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_WalkingRootRecursive_Then_AllEntriesAreReturnedDepthFirst(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();
        var (volume, root) = await context.Open(VolumeAccess.Read);

        // act - walk root recursive
        var walkerResult = await VolumeEntryWalker.Create(volume, root, true);
        Assert.True(walkerResult.IsSuccess);
        var entries = await VolumeTestContext.ReadAll(walkerResult.Value);

        // assert - all entries are returned depth first
        Assert.Equal(
            ["dir1", "dir1/dir2", "dir1/dir2/file3.txt", "dir1/file2.txt", "file1.txt", "file2.bin"],
            VolumeTestContext.RelativePaths(entries).Select(x => x.ToLowerInvariant()).ToArray());
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_WalkingSubdirectory_Then_EntriesAreRelativeToSubdirectory(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();
        var (volume, root) = await context.Open(VolumeAccess.Read, "dir1");

        // act - walk dir1 recursive
        var walkerResult = await VolumeEntryWalker.Create(volume, root, true);
        Assert.True(walkerResult.IsSuccess);
        var entries = await VolumeTestContext.ReadAll(walkerResult.Value);

        // assert - entries are relative to dir1
        Assert.Equal(["dir2", "dir2/file3.txt", "file2.txt"],
            VolumeTestContext.RelativePaths(entries).Select(x => x.ToLowerInvariant()).ToArray());
        Assert.Equal(root, walkerResult.Value.DirPathComponents);
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_WalkingSingleFile_Then_OnlyFileIsReturned(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();
        var (volume, root) = await context.Open(VolumeAccess.Read, "dir1", "file2.txt");

        // act - walk single file
        var walkerResult = await VolumeEntryWalker.Create(volume, root, false);
        Assert.True(walkerResult.IsSuccess);
        var walker = walkerResult.Value;
        Assert.True(walker.IsSingleFileEntryNext);
        var entries = await VolumeTestContext.ReadAll(walker);

        // assert - only file is returned
        Assert.Equal(["file2.txt"], VolumeTestContext.RelativePaths(entries).Select(x => x.ToLowerInvariant()));
        Assert.Equal(root.Take(root.Length - 1), walker.DirPathComponents);
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_WalkingPattern_Then_MatchingFilesAreReturned(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();
        var (volume, root) = await context.Open(VolumeAccess.Read, "*.txt");

        // act - walk pattern non-recursive
        var walkerResult = await VolumeEntryWalker.Create(volume, root, false);
        Assert.True(walkerResult.IsSuccess);
        Assert.True(walkerResult.Value.UsesPattern);
        var entries = await VolumeTestContext.ReadAll(walkerResult.Value);

        // assert - only matching files in root are returned
        Assert.Equal(["file1.txt"], VolumeTestContext.RelativePaths(entries)
            .Where(x => !x.Equals("dir1", System.StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ToLowerInvariant()));
        Assert.DoesNotContain(entries, x => x.Type == EntryType.File && x.Name.EndsWith(".bin"));
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_WalkingMissingPath_Then_PathNotFoundErrorIsReturned(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();
        var (volume, root) = await context.Open(VolumeAccess.Read, "missing", "dir");

        // act - walk missing path
        var walkerResult = await VolumeEntryWalker.Create(volume, root, false);

        // assert - path not found error is returned
        Assert.True(walkerResult.IsFaulted);
        Assert.IsType<PathNotFoundError>(walkerResult.Error);
    }
}
