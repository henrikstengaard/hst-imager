using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Hst.Imager.Core.Volumes;
using Xunit;

namespace Hst.Imager.Core.Tests.VolumeTests;

public class GivenVolumeTarget
{
    private static Entry CreateEntry(EntryType type, params string[] relativePathComponents) => new()
    {
        Name = string.Join("/", relativePathComponents),
        FormattedName = string.Join("/", relativePathComponents),
        RawPath = string.Join("/", relativePathComponents),
        Type = type,
        Date = DateTime.Now,
        Size = 0,
        RelativePathComponents = relativePathComponents,
        FullPathComponents = relativePathComponents
    };

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_RootExistsCreatingDirectoryAndFile_Then_TheyAreCreatedInRoot(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();
        var (volume, root) = await context.Open(VolumeAccess.Write, "dir1");

        // arrange - create target for dir1
        var targetResult = await VolumeTarget.Create(volume, root, false, false, false);
        Assert.True(targetResult.IsSuccess);
        var target = targetResult.Value;

        // act - create directory and file in nested directory
        var createDirectoryResult = await target.CreateDirectory(CreateEntry(EntryType.Dir, "new"), ["new"],
            false, false);
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("nested"));
        var createFileResult = await target.CreateFile(CreateEntry(EntryType.File, "new", "sub", "file.txt"),
            ["new", "sub", "file.txt"], stream, false, false);

        // assert - directory and file are created
        Assert.True(createDirectoryResult.IsSuccess, createDirectoryResult.IsFaulted ? createDirectoryResult.Error.ToString() : null);
        Assert.True(createFileResult.IsSuccess, createFileResult.IsFaulted ? createFileResult.Error.ToString() : null);
        Assert.Equal("nested", await VolumeTestContext.ReadFile(volume, [..root, "new", "sub", "file.txt"]));
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_RootDoesntExistCreatingDirectory_Then_PathNotFoundErrorIsReturned(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        var (volume, root) = await context.Open(VolumeAccess.Write, "missing", "dir");

        // act - create target for missing dir
        var targetResult = await VolumeTarget.Create(volume, root, false, false, false);

        // assert - path not found error is returned
        Assert.True(targetResult.IsFaulted);
        Assert.IsType<PathNotFoundError>(targetResult.Error);
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_RootDoesntExistWithCreateDirectory_Then_DirectoriesAreCreated(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        var (volume, root) = await context.Open(VolumeAccess.Write, "new1", "new2");

        // act - create target with create directory
        var targetResult = await VolumeTarget.Create(volume, root, true, true, false);

        // assert - directories are created
        Assert.True(targetResult.IsSuccess, targetResult.IsFaulted ? targetResult.Error.ToString() : null);
        var entryResult = await volume.GetEntry(root);
        Assert.True(entryResult.IsSuccess);
        Assert.Equal(EntryType.Dir, entryResult.Value.Type);
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_CopyingSingleFileToNewName_Then_FileIsCreatedWithNewName(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();
        var (srcVolume, srcRoot) = await context.Open(VolumeAccess.Read, "dir1", "file2.txt");
        var (destVolume, destRoot) = await context.Open(VolumeAccess.Write, "dir1", "dir2", "renamed.txt");

        // arrange - create walker for single file and target for new name
        var walker = (await VolumeEntryWalker.Create(srcVolume, srcRoot, false)).Value;
        var targetResult = await VolumeTarget.Create(destVolume, destRoot, false, false, false);
        Assert.True(targetResult.IsSuccess);
        var target = targetResult.Value;

        // act - copy entries from walker to target
        while (await walker.Next())
        {
            var entry = walker.Current;
            var streamResult = await walker.Volume.OpenFile(entry.FullPathComponents);
            await using var stream = streamResult.Value;
            var result = await target.CreateFile(entry, entry.RelativePathComponents, stream, false,
                walker.IsSingleFileEntryNext);
            Assert.True(result.IsSuccess, result.IsFaulted ? result.Error.ToString() : null);
        }

        // assert - file is copied with new name
        Assert.Same(srcVolume, destVolume);
        Assert.Equal("dir1 file2", await VolumeTestContext.ReadFile(destVolume, destRoot));
        Assert.False(target.IsSelfCopy(walker));
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_CopyingSingleFileOntoItself_Then_IsSelfCopy(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();
        var (srcVolume, srcRoot) = await context.Open(VolumeAccess.Read, "dir1", "file2.txt");
        var (destVolume, destRoot) = await context.Open(VolumeAccess.Write, "dir1", "file2.txt");

        // act - create walker and target
        var walker = (await VolumeEntryWalker.Create(srcVolume, srcRoot, false)).Value;
        var target = (await VolumeTarget.Create(destVolume, destRoot, false, false, false)).Value;

        // assert - self copy
        Assert.True(target.IsSelfCopy(walker));
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_CopyingDirectoryRecursiveIntoSubdirectory_Then_IsCyclic(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();
        var (srcVolume, srcRoot) = await context.Open(VolumeAccess.Read, "dir1");
        var (destVolume, destRoot) = await context.Open(VolumeAccess.Write, "dir1", "dir2");

        // act - create walker and target
        var walker = (await VolumeEntryWalker.Create(srcVolume, srcRoot, true)).Value;
        var target = (await VolumeTarget.Create(destVolume, destRoot, true, false, false)).Value;

        // assert - cyclic
        Assert.True(target.IsCyclic(walker));
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_MovingFile_Then_FileIsMoved(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();
        var (srcVolume, srcRoot) = await context.Open(VolumeAccess.Write, "file1.txt");
        var (destVolume, destRoot) = await context.Open(VolumeAccess.Write, "dir1", "dir2");

        // arrange - create walker and target
        var walker = (await VolumeEntryWalker.Create(srcVolume, srcRoot, false)).Value;
        var target = (await VolumeTarget.Create(destVolume, destRoot, false, false, false)).Value;

        // act - move file
        Assert.True(await walker.Next());
        var entry = walker.Current;
        var result = await target.MoveEntry(entry, entry.RelativePathComponents, walker.IsSingleFileEntryNext);

        // assert - file is moved
        Assert.True(result.IsSuccess, result.IsFaulted ? result.Error.ToString() : null);
        Assert.True((await destVolume.GetEntry(srcRoot)).IsFaulted);
        Assert.Equal("file1", await VolumeTestContext.ReadFile(destVolume, [..destRoot, "file1.txt"]));
    }

    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_CopyingDirectoryRecursiveToOtherVolume_Then_EntriesAreCopied(VolumeType volumeType)
    {
        using var srcContext = await VolumeTestContext.Create(VolumeType.Amiga);
        await srcContext.CreateDirectoriesAndFiles();
        using var destContext = await VolumeTestContext.Create(volumeType);
        var (srcVolume, srcRoot) = await srcContext.Open(VolumeAccess.Read);
        var (destVolume, destRoot) = await destContext.Open();

        // arrange - create walker and target
        var walker = (await VolumeEntryWalker.Create(srcVolume, srcRoot, true)).Value;
        var target = (await VolumeTarget.Create(destVolume, destRoot, true, false, false)).Value;

        // act - copy entries
        while (await walker.Next())
        {
            var entry = walker.Current;
            if (entry.Type == EntryType.Dir)
            {
                var dirResult = await target.CreateDirectory(entry, entry.RelativePathComponents, false, false);
                Assert.True(dirResult.IsSuccess, dirResult.IsFaulted ? dirResult.Error.ToString() : null);
                continue;
            }

            await using var stream = (await walker.Volume.OpenFile(entry.FullPathComponents)).Value;
            var fileResult = await target.CreateFile(entry, entry.RelativePathComponents, stream, false, false);
            Assert.True(fileResult.IsSuccess, fileResult.IsFaulted ? fileResult.Error.ToString() : null);
        }

        await destVolume.Flush();

        // assert - entries are copied
        var destWalker = (await VolumeEntryWalker.Create(destVolume, destRoot, true)).Value;
        var entries = await VolumeTestContext.ReadAll(destWalker);
        Assert.Equal(
            ["dir1", "dir1/dir2", "dir1/dir2/file3.txt", "dir1/file2.txt", "file1.txt", "file2.bin"],
            VolumeTestContext.RelativePaths(entries).Select(x => x.ToLowerInvariant()).ToArray());
        Assert.Equal("dir2 file3",
            await VolumeTestContext.ReadFile(destVolume, [..destRoot, "dir1", "dir2", "file3.txt"]));
    }
}
