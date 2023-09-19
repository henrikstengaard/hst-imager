namespace Hst.Imager.Core.Tests.EntryWriterTests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Commands;
using CommandTests;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using Hst.Core.Extensions;
using Models;
using Models.FileSystems;
using Xunit;

public class GivenFileSystemEntryWriterWithFatFormattedDisk : FsCommandTestBase
{
    [Fact]
    public async Task WhenCreateDirectoryAndWriteEntriesThenDirectoryAndFilesExist()
    {
        var path = $"{Guid.NewGuid()}.vhd";

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();
        testCommandHelper.AddTestMedia(path);

        // arrange - source disk image file with directories
        await CreateMbrFatFormattedDisk(testCommandHelper, path, 10.MB());
        
        // arrange - get writeable media
        var mediaResult = await testCommandHelper.GetWritableMedia(Enumerable.Empty<IPhysicalDrive>(), path);
        using var media = mediaResult.Value;
        var diskMedia = media as DiskMedia;
        Assert.NotNull(diskMedia);
        
        // arrange - mount fat file system
        var biosPartitionTable = new BiosPartitionTable(diskMedia.Disk);
        var partitionInfo = biosPartitionTable.Partitions[0];
        var fatFileSystem = new FatFileSystem(partitionInfo.Open());
        
        // arrange - fat entry writer
        var fatEntryWriter = new FileSystemEntryWriter(media, fatFileSystem, Array.Empty<string>());

        // act - create directories and files
        await WriteEntry(fatEntryWriter, "file1.txt", new MemoryStream());
        await WriteEntry(fatEntryWriter, "file2.txt", new MemoryStream());
        await CreateDirectory(fatEntryWriter, "dir1");
        await WriteEntry(fatEntryWriter, "dir1\\file3.txt", new MemoryStream());
        
        // assert - 1 directory was created
        var directories = fatFileSystem.GetDirectories("", "*.*", SearchOption.AllDirectories).ToList();
        Assert.Single(directories);

        // assert - dir1 file was written
        Assert.Equal("dir1", directories.FirstOrDefault(x => x.Equals("dir1", StringComparison.OrdinalIgnoreCase))?.ToLower());
        
        // assert - 3 files was written
        var files = fatFileSystem.GetFiles("", "*.*", SearchOption.AllDirectories).ToList();
        Assert.Equal(3, files.Count);
        
        // assert - file1.txt file was copied
        Assert.Equal("file1.txt", files.FirstOrDefault(x => x.Equals("file1.txt", StringComparison.OrdinalIgnoreCase))?.ToLower());

        // assert - file2.txt file was copied
        Assert.Equal("file2.txt", files.FirstOrDefault(x => x.Equals("file2.txt", StringComparison.OrdinalIgnoreCase))?.ToLower());

        // assert - file3.txt file was copied
        var file3 = Path.Combine("dir1", "file3.txt");
        Assert.Equal(file3, files.FirstOrDefault(x => x.Equals(file3, StringComparison.OrdinalIgnoreCase))?.ToLower());
    }

    private async Task CreateDirectory(IEntryWriter entryWriter, string entryPath)
    {
        var name = Path.GetFileName(entryPath);
        var entryPathComponents = entryPath.Split(new[] { "\\", "/" }, StringSplitOptions.RemoveEmptyEntries);
        await entryWriter.CreateDirectory(new Entry
        {
            Name = name,
            FormattedName = name,
            RawPath = entryPath,
            FullPathComponents = entryPathComponents,
            RelativePathComponents = entryPathComponents,
            Attributes = "A---",
            Date = DateTime.Now,
            Size = 0,
            Type = EntryType.Dir
        }, entryPathComponents, false);
    }

    private async Task WriteEntry(IEntryWriter entryWriter, string entryPath, Stream stream)
    {
        var name = Path.GetFileName(entryPath);
        var entryPathComponents = entryPath.Split(new []{"\\", "/"}, StringSplitOptions.RemoveEmptyEntries);
        await entryWriter.WriteEntry(new Entry
        {
            Name = name,
            FormattedName = name,
            RawPath = entryPath,
            FullPathComponents = entryPathComponents,
            RelativePathComponents = entryPathComponents,
            Attributes = "A---",
            Date = DateTime.Now,
            Size = 0,
            Type = EntryType.File
        }, entryPathComponents, stream, false);
    }
}