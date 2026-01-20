using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using Hst.Core.Extensions;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.Models.FileSystems;

namespace Hst.Imager.Core.Tests;

public static class MbrTestHelper
{
    public static async Task CreateMbrFatFormattedDisk(TestCommandHelper testCommandHelper, string path,
        long diskSize = 10 * 1024 * 1024)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, size: diskSize, create: true);
        using var media = mediaResult.Value;

        var disk = await MediaHelper.ResolveVirtualDisk(media);
        var biosPartitionTable = BiosPartitionTable.Initialize(disk);
        var partitionIndex = biosPartitionTable.CreatePrimaryBySector(1, (disk.Capacity / disk.SectorSize) - 1,
            BiosPartitionTypes.Fat32Lba, true);
        FatFileSystem.FormatPartition(disk, partitionIndex, "FATDISK");
    }
    
    public static async Task CreateMbrDisk(TestCommandHelper testCommandHelper, string path,
        long diskSize)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, size: diskSize, create: true);
        using var media = mediaResult.Value;

        var disk = await MediaHelper.ResolveVirtualDisk(media);

        BiosPartitionTable.Initialize(disk);
    }

    public static async Task AddMbrPartition(TestCommandHelper testCommandHelper, string path,
        long startSector, long endSector, byte partitionType = BiosPartitionTypes.Fat16)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
        using var media = mediaResult.Value;

        var disk = await MediaHelper.ResolveVirtualDisk(media);

        var biosPartitionTable = new BiosPartitionTable(disk);

        biosPartitionTable.CreatePrimaryBySector(startSector, endSector, partitionType, true);
    }

    public static async Task FatFormatMbrPartition(TestCommandHelper testCommandHelper, string mediaPath,
        int partitionNumber)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(mediaPath);
        using var media = mediaResult.Value;

        var disk = await MediaHelper.ResolveVirtualDisk(media);

        var biosPartitionTable = new BiosPartitionTable(disk);

        if (partitionNumber < 0 || partitionNumber >= biosPartitionTable.Partitions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(partitionNumber), 
                $"Partition number {partitionNumber} is out of range. Available partitions: {biosPartitionTable.Partitions.Count}");
        }
        
        FatFileSystem.FormatPartition(disk, partitionNumber, "EMPTY");
    }
    
    public static async Task CreateMbrDiskWithFat16AndPiStormRdbPartitions(TestCommandHelper testCommandHelper, string mbrDiskPath)
    {
        // disk sizes
        var mbrDiskSize = 100.MB();
        var rdbDiskSize = 20.MB();

        // add mbr disk media
        testCommandHelper.AddTestMedia(mbrDiskPath, 0);

        // add rdb disk media
        var rdbDiskPath = $"rdb_{Guid.NewGuid()}.vhd";
        testCommandHelper.AddTestMedia(rdbDiskPath, 0);

        // calculate mbr partition start and end sectors
        var mbrPartition1StartSector = 63;
        var mbrPartition1EndSector = mbrPartition1StartSector + 16384;
        var mbrPartition2StartSector = mbrPartition1EndSector + 1;
        var mbrPartition2EndSector = (mbrDiskSize / 512) - 10;

        // mbr disk
        await TestHelper.CreateMbrDisk(testCommandHelper, mbrDiskPath, mbrDiskSize);
        await AddMbrPartition(testCommandHelper, mbrDiskPath,
            mbrPartition1StartSector, mbrPartition1EndSector, BiosPartitionTypes.Fat16);
        await AddMbrPartition(testCommandHelper, mbrDiskPath,
            mbrPartition2StartSector, mbrPartition2EndSector, Constants.BiosPartitionTypes.PiStormRdb);

        // rdb disk
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, rdbDiskPath, rdbDiskSize);

        // get readable media for rdb disk
        var rdbMediaResult = await testCommandHelper.GetReadableMedia([], rdbDiskPath);
        if (!rdbMediaResult.IsSuccess)
        {
            throw new Exception(rdbMediaResult.Error.Message);
        }

        // get writable media for mbr disk
        var mbrMediaResult = await testCommandHelper.GetWritableMedia([], mbrDiskPath);
        if (!mbrMediaResult.IsSuccess)
        {
            throw new Exception(mbrMediaResult.Error.Message);
        }

        // copy rdb media to mbr partition 2 creating pistorm rdb hard disk
        using var mbrMedia = mbrMediaResult.Value;
        var mbrStream = mbrMedia.Stream;

        mbrStream.Seek(512 * mbrPartition2StartSector, SeekOrigin.Begin);

        using var rdbMedia = rdbMediaResult.Value;

        var rdbStream = rdbMedia.Stream;

        rdbStream.Position = 0;
        var buffer = new byte[4096];

        int bytesRead;
        do
        {
            bytesRead = rdbStream.Read(buffer, 0, buffer.Length);
            mbrStream.Write(buffer, 0, bytesRead);
        } while (bytesRead != 0);
    }
    
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

        media.Dispose();
    }

    public static async Task CreateFile(TestCommandHelper testCommandHelper, string path, string[] pathComponents)
    {
        var (media, fileSystem) = await MountFileSystem(testCommandHelper, path, 0, true);

        if (pathComponents.Length > 1)
        {
            fileSystem.CreateDirectory(string.Join("\\",  pathComponents.Take(pathComponents.Length - 1)));
        }
        
        await using var file = fileSystem.OpenFile(string.Join("\\",  pathComponents), FileMode.Create, FileAccess.Write);

        media.Dispose();
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
        
        var disk = await MediaHelper.ResolveVirtualDisk(media);
        var biosPartitionTable = new BiosPartitionTable(disk);
            
        var partitions = biosPartitionTable.Partitions;
            
        if (partitionNumber < 0 || partitionNumber >= partitions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(partitionNumber), 
                $"Partition number {partitionNumber} is out of range. Available partitions: {partitions.Count}");
        }
            
        var partition = partitions[partitionNumber];

        return (new DiskMedia(media, disk, null), new FatFileSystem(partition.Open()));
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