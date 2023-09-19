using System.Threading.Tasks;

namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.IO;
using System.Text;
using Commands;
using DiscUtils;
using DiscUtils.Fat;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Models;
using PartitionInfo = DiscUtils.Partitions.PartitionInfo;

public static class DiskFileSystemHelper
{
    public static void CreateGptFatDirectoriesAndFiles(VirtualDisk disk, int partitionIndex = 0)
    {
        CreateFileSystemDirectoriesAndFiles(GetGptFatFileSystem(disk, partitionIndex));
    }

    public static void CreateGptNtfsDirectoriesAndFiles(VirtualDisk disk, int partitionIndex = 0)
    {
        CreateFileSystemDirectoriesAndFiles(GetGptNtfsFileSystem(disk, partitionIndex));
    }

    public static IFileSystem GetGptFatFileSystem(VirtualDisk disk, int partitionIndex = 0)
    {
        return new FatFileSystem(GetGptPartition(disk, partitionIndex).Open());
    }
    
    public static IFileSystem GetGptNtfsFileSystem(VirtualDisk disk, int partitionIndex = 0)
    {
        return new NtfsFileSystem(GetGptPartition(disk, partitionIndex).Open());
    }

    public static PartitionInfo GetGptPartition(VirtualDisk disk, int partitionIndex = 0)
    {
        var guidPartitionTable = new GuidPartitionTable(disk);

        if (guidPartitionTable.Partitions.Count == 0)
        {
            throw new IOException("No partitions in Guid Partition Table");
        }
        
        return guidPartitionTable.Partitions[partitionIndex];
    }

    public static async Task<Media> GetDiskMedia(ICommandHelper commandHelper, string path)
    {
        var mediaResult = await commandHelper.GetWritableFileMedia(path);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }

        var media = mediaResult.Value;
        if (media is DiskMedia diskMedia)
        {
            return diskMedia;
        }
        
        var disk = new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
        return new DiskMedia(path, Path.GetFileName(path), disk.Capacity, Media.MediaType.Raw, false, disk, 
            media.Byteswap, media.Stream);
    }

    public static VirtualDisk ToDisk(Media media)
    {
        return media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(media.Stream, Ownership.Dispose);
    }

    public static void CreateLocalDirectoriesAndFiles(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        File.WriteAllText(Path.Combine(path, "file1.txt"), "test", Encoding.UTF8);
        File.WriteAllBytes(Path.Combine(path, "file2.txt"), Array.Empty<byte>());
        var dir1Path = Path.Combine(path, "dir1");
        if (!Directory.Exists(dir1Path))
        {
            Directory.CreateDirectory(dir1Path);
        }
        File.WriteAllBytes(Path.Combine(dir1Path, "file3.txt"), Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(dir1Path, "test.txt"), Array.Empty<byte>());
    }
    
    public static void CreateFileSystemDirectoriesAndFiles(IFileSystem fileSystem)
    {
        using (var file1 = fileSystem.OpenFile("file1.txt", FileMode.Create))
        {
            using (var streamWriter = new StreamWriter(file1, Encoding.UTF8))
            {
                streamWriter.Write("test");
            }
        }

        using (fileSystem.OpenFile("file2.txt", FileMode.Create))
        {
        }

        fileSystem.CreateDirectory("dir1");

        using (fileSystem.OpenFile("dir1\\file3.txt", FileMode.Create))
        {
        }

        using (fileSystem.OpenFile("dir1\\test.txt", FileMode.Create))
        {
        }
    }
}