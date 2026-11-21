using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Hst.Imager.Core.Volumes;
using Xunit;

namespace Hst.Imager.Core.Tests.VolumeTests;

public class GivenArchiveVolume
{
    private static async Task<MemoryStream> CreateZipStream()
    {
        var stream = new MemoryStream();
        using (var zipArchive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            zipArchive.CreateEntry("dir1/");
            await using (var writer = new StreamWriter(zipArchive.CreateEntry("dir1/file2.txt").Open()))
            {
                await writer.WriteAsync("file2");
            }

            // file in directory without directory entry
            await using (var writer = new StreamWriter(zipArchive.CreateEntry("dir2/sub/file3.txt").Open()))
            {
                await writer.WriteAsync("file3");
            }

            await using (var writer = new StreamWriter(zipArchive.CreateEntry("file1.txt").Open()))
            {
                await writer.WriteAsync("file1");
            }
        }

        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task When_ReadingZip_Then_EntriesIncludingImplicitDirectoriesAreListed()
    {
        // arrange - read zip archive volume
        using var volume = await ArchiveVolumeReader.ReadZip(await CreateZipStream());

        // act - walk zip recursive
        var walker = (await VolumeEntryWalker.Create(volume, [], true)).Value;
        var entries = await VolumeTestContext.ReadAll(walker);

        // assert - entries including implicit directories are listed
        Assert.Equal(
            ["dir1", "dir1/file2.txt", "dir2", "dir2/sub", "dir2/sub/file3.txt", "file1.txt"],
            VolumeTestContext.RelativePaths(entries));
        Assert.Equal("file3", await VolumeTestContext.ReadFile(volume, ["dir2", "sub", "file3.txt"]));
    }

    [Fact]
    public async Task When_WalkingSingleFileInZip_Then_OnlyFileIsReturned()
    {
        // arrange - read zip archive volume
        using var volume = await ArchiveVolumeReader.ReadZip(await CreateZipStream());

        // act - walk single file
        var walker = (await VolumeEntryWalker.Create(volume, ["dir1", "file2.txt"], false)).Value;
        var entries = await VolumeTestContext.ReadAll(walker);

        // assert - only file is returned
        Assert.Equal(["file2.txt"], VolumeTestContext.RelativePaths(entries));
    }

    [Fact]
    public async Task When_WritingToArchive_Then_ReadOnlyErrorIsReturned()
    {
        // arrange - read zip archive volume
        using var volume = await ArchiveVolumeReader.ReadZip(await CreateZipStream());

        // act - write to archive
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("new"));
        var writeResult = await volume.WriteFile(["new.txt"], stream, false);
        var deleteResult = await volume.Delete(["file1.txt"]);
        var targetResult = await VolumeTarget.Create(volume, [], false, false, false);

        // assert - read-only errors are returned
        Assert.True(volume.IsReadOnly);
        Assert.IsType<VolumeReadOnlyError>(writeResult.Error);
        Assert.IsType<VolumeReadOnlyError>(deleteResult.Error);
        Assert.IsType<VolumeReadOnlyError>(targetResult.Error);
    }

    [Fact]
    public async Task When_GettingMissingEntry_Then_PathNotFoundErrorIsReturned()
    {
        // arrange - create archive volume from items
        using var volume = new ArchiveVolume(Core.PathComponents.MediaPath.ZipArchivePath,
            Models.AttributesMode.Auto, false,
            [
                new ArchiveItem(["dir", "file.txt"], false, DateTime.Now, 3, null, "dir/file.txt",
                    () => Task.FromResult<Stream>(new MemoryStream(Encoding.ASCII.GetBytes("abc"))))
            ]);

        // act - get entries
        var dirResult = await volume.GetEntry(["dir"]);
        var missingResult = await volume.GetEntry(["dir", "missing.txt"]);

        // assert - implicit directory exists and missing entry is path not found
        Assert.True(dirResult.IsSuccess);
        Assert.Equal(EntryType.Dir, dirResult.Value.Type);
        Assert.IsType<PathNotFoundError>(missingResult.Error);
        Assert.Equal(["file.txt"], (await volume.ListEntries(["dir"])).Value.Select(x => x.Name));
    }
}
