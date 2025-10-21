using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.Models.FileSystems;

namespace Hst.Imager.Core.Tests;

public static class GptTestHelper
{
    /// <summary>
    /// Create
    /// - dir1
    ///   - dir3
    ///   - file1.txt
    /// - dir2
    /// 
    /// </summary>
    /// <param name="testCommandHelper"></param>
    /// <param name="path"></param>
    /// <exception cref="IOException"></exception>
    public static async Task CreateDirectoriesAndFiles(TestCommandHelper testCommandHelper, string path)
    {
        var (media, fileSystem) = await MountFileSystem(testCommandHelper, path, 0, true);
        
        fileSystem.CreateDirectory("dir1");
        fileSystem.CreateDirectory("dir2");
        fileSystem.CreateDirectory("dir1\\dir3");
        
        await using var file = fileSystem.OpenFile("dir1\\file1.txt", FileMode.Create, FileAccess.Write);
    }

    public static async Task<(Media, IFileSystem)> MountFileSystem(TestCommandHelper testCommandHelper, string mediaPath,
        int partitionNumber, bool writable = false)
    {
        var mediaResult = writable
            ? await testCommandHelper.GetWritableFileMedia(mediaPath)
            : await testCommandHelper.GetReadableFileMedia(mediaPath);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }
            
        var media = mediaResult.Value;
        
        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
        var guidPartitionTable = new GuidPartitionTable(disk);
            
        var partitions = guidPartitionTable.Partitions;
            
        if (partitionNumber < 0 || partitionNumber >= partitions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(partitionNumber), 
                $"Partition number {partitionNumber} is out of range. Available partitions: {partitions.Count}");
        }
            
        var partition = partitions[partitionNumber];

        return (media, new FatFileSystem(partition.Open()));
    }

    public static async Task CreateDirectory(
        TestCommandHelper testCommandHelper, string mediaPath, int partitionNumber, string[] dirPathComponents)
    {
        var (media, fileSystem) = await MountFileSystem(testCommandHelper, mediaPath, partitionNumber);
        
        fileSystem.CreateDirectory(string.Join("/", dirPathComponents));
        
        media.Dispose();
    }

    public static async Task<IEnumerable<Entry>> GetEntriesFromFileSystemVolume(TestCommandHelper testCommandHelper, string mediaPath,
        int partitionNumber, string[] dirPathComponents, bool writable = false)
    {
        var (media, fileSystem) = await MountFileSystem(testCommandHelper, mediaPath, partitionNumber, writable);

        var path = dirPathComponents.Length == 0 ? string.Empty : Path.Combine(dirPathComponents);

        var dirEntries = fileSystem.GetDirectories(path).Select(x => new Entry
        {
            Type = EntryType.Dir,
            Name = Path.GetFileName(x),
            RawPath = x,
            FullPathComponents = x.Split(['\\', '/'])
        }).ToList();
        
        var fileEntries = fileSystem.GetFiles(path).Select(x => new Entry
        {
            Type = EntryType.File,
            Name = Path.GetFileName(x),
            RawPath = x,
            FullPathComponents = x.Split(['\\', '/'])
        }).ToList();

        media.Dispose();
        
        return dirEntries.Concat(fileEntries);
    }
}