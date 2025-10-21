using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Core.Extensions;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Tests;

public static class PiStormRdbTestHelper
{
    public static async Task CreatePiStormRdbDisk(TestCommandHelper testCommandHelper, string mediaPath)
    {
        // disk sizes
        var mbrDiskSize = 100.MB();
        var rdbDiskSize = 20.MB();

        // add mbr disk media
        testCommandHelper.AddTestMedia(mediaPath, mbrDiskSize);

        // add rdb disk media
        var rdbDiskPath = $"rdb_{Guid.NewGuid()}.vhd";
        testCommandHelper.AddTestMedia(rdbDiskPath, rdbDiskSize);

        // calculate mbr partition start and end sectors
        var mbrPartition1StartSector = 63;
        var mbrPartition1EndSector = mbrPartition1StartSector + 16384;
        var mbrPartition2StartSector = mbrPartition1EndSector + 1;
        var mbrPartition2EndSector = (mbrDiskSize / 512) - 10;
        
        await MbrTestHelper.CreateMbrDisk(testCommandHelper, mediaPath, mbrDiskSize);
        await MbrTestHelper.AddMbrPartition(testCommandHelper, mediaPath,
            mbrPartition1StartSector, mbrPartition1EndSector);
        await MbrTestHelper.AddMbrPartition(testCommandHelper, mediaPath,
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
        var mbrMediaResult = await testCommandHelper.GetWritableMedia([], mediaPath);
        if (!mbrMediaResult.IsSuccess)
        {
            throw new Exception(mbrMediaResult.Error.Message);
        }

        // copy rdb media to mbr partition 2 creating pistorm rdb hard disk
        using var mbrMedia = mbrMediaResult.Value;
        var mbrStream = mbrMedia is DiskMedia diskMedia
            ? diskMedia.Disk.Content
            : mbrMedia.Stream;

        mbrStream.Seek(512 * mbrPartition2StartSector, SeekOrigin.Begin);

        using var rdbMedia = rdbMediaResult.Value;

        var rdbStream = rdbMedia is DiskMedia rdbDiskMedia
            ? rdbDiskMedia.Disk.Content
            : rdbMedia.Stream;

        rdbStream.Position = 0;
        var buffer = new byte[4096];

        int bytesRead;
        do
        {
            bytesRead = await rdbStream.ReadAsync(buffer, 0, buffer.Length);
            await mbrStream.WriteAsync(buffer, 0, bytesRead);
        } while (bytesRead != 0);
    }

    public static async Task<(Media, IFileSystemVolume)> MountFileSystemVolume(TestCommandHelper testCommandHelper,
        string mediaPath, bool writable = false)
    {
        var resolvedResult = testCommandHelper.ResolveMedia(mediaPath);
        if (resolvedResult.IsFaulted)
        {
            throw new IOException(resolvedResult.Error.Message);
        }
        
        var mediaResult = writable
            ? await testCommandHelper.GetWritableFileMedia(resolvedResult.Value.MediaPath)
            : await testCommandHelper.GetReadableFileMedia(resolvedResult.Value.MediaPath);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }
            
        var media = mediaResult.Value;

        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(media, resolvedResult.Value.FileSystemPath,
            resolvedResult.Value.DirectorySeparatorChar);
        if (!piStormRdbMediaResult.HasPiStormRdb)
        {
            throw new IOException($"Path '{resolvedResult.Value.MediaPath}' is not a valid PiStorm RDB media");
        }
        
        var piStormRdbMedia = piStormRdbMediaResult.Media;

        var parts = (piStormRdbMediaResult.FileSystemPath ?? string.Empty).Split( 
            [resolvedResult.Value.DirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length < 2)
        {
            throw new IOException($"Path '{piStormRdbMediaResult.FileSystemPath}' does not contain partition table and partition number");
        }
        
        if (parts[0].ToLowerInvariant() != "rdb")
        {
            throw new IOException($"Path '{resolvedResult.Value.FileSystemPath}' does not contain partition table (rdb)");
        }

        if (!int.TryParse(parts[1], out var partitionNumber))
        {
            throw new IOException($"Path '{piStormRdbMediaResult.FileSystemPath}' does not contain valid partition number");
        }

        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(piStormRdbMedia);
        
        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
        
        if (partitionNumber < 1 || partitionNumber > partitionBlocks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(partitionNumber), 
                $"Partition number {partitionNumber} is out of range. Available partitions: {partitionBlocks.Count}");
        }
        
        var partitionBlock = partitionBlocks[partitionNumber - 1];
        
        return (piStormRdbMedia, await RdbTestHelper.MountFileSystemVolume(piStormRdbMedia.Stream,
            partitionBlock));
    }

    /// <summary>
    /// Create directories and files in following structure:
    /// - dir1
    ///   - dir3
    ///   - file1.txt
    /// - dir2
    /// </summary>
    /// <param name="testCommandHelper"></param>
    /// <param name="path"></param>
    /// <exception cref="IOException"></exception>
    public static async Task CreateDirectoriesAndFiles(TestCommandHelper testCommandHelper, string path)
    {
        var (media, fileSystemVolume) = await MountFileSystemVolume(testCommandHelper, path, true);
        
        await fileSystemVolume.CreateDirectory("dir1");
        await fileSystemVolume.CreateDirectory("dir2");
        await fileSystemVolume.ChangeDirectory("dir1");
        await fileSystemVolume.CreateDirectory("dir3");
        await fileSystemVolume.CreateFile("file1.txt", true, true);
        
        fileSystemVolume.Dispose();
        media.Dispose();
    }
    
    public static async Task CreateDirectory(
        TestCommandHelper testCommandHelper, string path)
    {
        var resolvedResult = testCommandHelper.ResolveMedia(path);
        if (resolvedResult.IsFaulted)
        {
            throw new IOException(resolvedResult.Error.Message);
        }
        
        var readableMediaResult = await testCommandHelper.GetReadableMedia([], path);
        if (readableMediaResult.IsFaulted)
        {
            throw new IOException(readableMediaResult.Error.Message);
        }
        
        using var media = readableMediaResult.Value;

        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(media, resolvedResult.Value.FileSystemPath,
            resolvedResult.Value.DirectorySeparatorChar);
        if (!piStormRdbMediaResult.HasPiStormRdb)
        {
            throw new IOException($"Path '{resolvedResult.Value.MediaPath}' is not a valid PiStorm RDB media");
        }

        var parts = (piStormRdbMediaResult.FileSystemPath ?? string.Empty).Split( 
            [resolvedResult.Value.DirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length < 2)
        {
            throw new IOException($"Path '{piStormRdbMediaResult.FileSystemPath}' does not contain partition table and partition number");
        }
        
        if (parts[0].ToLowerInvariant() != "rdb")
        {
            throw new IOException($"Path '{resolvedResult.Value.FileSystemPath}' does not contain partition table (rdb)");
        }

        if (!int.TryParse(parts[1], out var partitionNumber))
        {
            throw new IOException($"Path '{piStormRdbMediaResult.FileSystemPath}' does not contain valid partition number");
        }

        var dirPathComponents = parts.Skip(2).ToArray();
        
        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(piStormRdbMediaResult.Media);
        
        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
        
        if (partitionNumber < 1 || partitionNumber > partitionBlocks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(partitionNumber), 
                $"Partition number {partitionNumber} is out of range. Available partitions: {partitionBlocks.Count}");
        }
        
        var partitionBlock = partitionBlocks[partitionNumber - 1];
        
        await using var fileSystemVolume = await RdbTestHelper.MountFileSystemVolume(piStormRdbMediaResult.Media.Stream,
            partitionBlock);
        
        foreach (var dirPathComponent in dirPathComponents)
        {
            var entries = (await fileSystemVolume.ListEntries()).ToList();
            
            if (!entries.Any(entry => entry.Type == Amiga.FileSystems.EntryType.Dir &&
                                      entry.Name.Equals(dirPathComponent, StringComparison.OrdinalIgnoreCase)))
            {
                await fileSystemVolume.CreateDirectory(dirPathComponent);
            }
            
            await fileSystemVolume.ChangeDirectory(dirPathComponent);
        }
    }

    public static async Task<IEnumerable<Amiga.FileSystems.Entry>> GetEntriesFromFileSystemVolume(
        TestCommandHelper testCommandHelper, string path, bool writable = false)
    {
        var resolvedResult = testCommandHelper.ResolveMedia(path);
        if (resolvedResult.IsFaulted)
        {
            throw new IOException(resolvedResult.Error.Message);
        }
        
        var readableMediaResult = await testCommandHelper.GetReadableMedia([], path);
        if (readableMediaResult.IsFaulted)
        {
            throw new IOException(readableMediaResult.Error.Message);
        }
        
        using var media = readableMediaResult.Value;

        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(media, resolvedResult.Value.FileSystemPath,
            resolvedResult.Value.DirectorySeparatorChar);
        if (!piStormRdbMediaResult.HasPiStormRdb)
        {
            throw new IOException($"Path '{resolvedResult.Value.MediaPath}' is not a valid PiStorm RDB media");
        }

        var parts = (piStormRdbMediaResult.FileSystemPath ?? string.Empty).Split( 
            [resolvedResult.Value.DirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length < 2)
        {
            throw new IOException($"Path '{piStormRdbMediaResult.FileSystemPath}' does not contain partition table and partition number");
        }
        
        if (parts[0].ToLowerInvariant() != "rdb")
        {
            throw new IOException($"Path '{resolvedResult.Value.FileSystemPath}' does not contain partition table (rdb)");
        }

        if (!int.TryParse(parts[1], out var partitionNumber))
        {
            throw new IOException($"Path '{piStormRdbMediaResult.FileSystemPath}' does not contain valid partition number");
        }

        var dirPathComponents = parts.Skip(2).ToArray();
        
        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(piStormRdbMediaResult.Media);
        
        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
        
        if (partitionNumber < 1 || partitionNumber > partitionBlocks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(partitionNumber), 
                $"Partition number {partitionNumber} is out of range. Available partitions: {partitionBlocks.Count}");
        }
        
        var partitionBlock = partitionBlocks[partitionNumber - 1];
        
        await using var fileSystemVolume = await RdbTestHelper.MountFileSystemVolume(piStormRdbMediaResult.Media.Stream,
            partitionBlock);
        
        foreach (var dirPathComponent in dirPathComponents)
        {
            await fileSystemVolume.ChangeDirectory(dirPathComponent);
        }
        
        return await fileSystemVolume.ListEntries();
    }
}