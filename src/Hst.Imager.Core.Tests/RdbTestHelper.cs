using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.Extensions;
using Hst.Amiga.FileSystems;
using Hst.Amiga.FileSystems.FastFileSystem;
using Hst.Amiga.FileSystems.Pfs3;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Tests;

public static class RdbTestHelper
{
    public static async Task AddPfs3RdbPartition(TestCommandHelper testCommandHelper, string path, 
        string driveName, long partitionSize)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
        using var media = mediaResult.Value;
        
        var stream = media.Stream;

        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

        rigidDiskBlock.AddPartition(driveName, partitionSize);
        
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
    }
    
    public static async Task Pfs3FormatRdbPartition(TestCommandHelper testCommandHelper, string path, int partitionNumber,
        string volumeName = "Workbench")
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
        using var media = mediaResult.Value;
        var stream = media.Stream;

        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

        if (rigidDiskBlock == null)
        {
            throw new IOException($"Media '{path}' is not a valid Amiga Rigid Disk Block (RDB) disk.");
        }
        
        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
        
        if (partitionNumber < 0 || partitionNumber >= partitionBlocks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(partitionNumber), 
                $"Partition number {partitionNumber} is out of range. Available partitions: {partitionBlocks.Count}");
        }
        
        await Pfs3Formatter.FormatPartition(stream, partitionBlocks[partitionNumber], volumeName);
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
        var (media, fileSystemVolume) = await MountFileSystemVolume(testCommandHelper, path, 0, true);
        
        await fileSystemVolume.CreateDirectory("dir1");
        await fileSystemVolume.CreateDirectory("dir2");
        await fileSystemVolume.ChangeDirectory("dir1");
        await fileSystemVolume.CreateDirectory("dir3");
        await fileSystemVolume.CreateFile("file1.txt", true, true);
        
        fileSystemVolume.Dispose();
        media.Dispose();
    }

    public static async Task CreateFile(TestCommandHelper testCommandHelper, string mediaPath, string[] pathComponents)
    {
        var (media, fileSystemVolume) = await MountFileSystemVolume(testCommandHelper, mediaPath, 0, true);

        if (pathComponents.Length > 1)
        {
            foreach (var pathComponent in pathComponents.Take(pathComponents.Length - 1))
            {
                await fileSystemVolume.CreateDirectory(pathComponent);
                await fileSystemVolume.ChangeDirectory(pathComponent);
            }
        }
        
        await fileSystemVolume.CreateFile(pathComponents[^1], true, true);
        
        fileSystemVolume.Dispose();
        media.Dispose();
    }
    
    public static async Task<IFileSystemVolume> MountFileSystemVolume(Stream stream, PartitionBlock partitionBlock) =>
        partitionBlock.DosTypeFormatted.ToLowerInvariant() switch
        {
            "pds\\3" or "pfs\\3" => await Pfs3Volume.Mount(stream, partitionBlock),
            "dos\\3" or "dos\\7" => await FastFileSystemVolume.MountPartition(stream, partitionBlock),
            _ => throw new IOException($"Unsupported dos type '{partitionBlock.DosTypeFormatted}'")
        };

    public static async Task<(Media, IFileSystemVolume)> MountFileSystemVolume(TestCommandHelper testCommandHelper, string mediaPath,
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
            
        var rigidDiskBlock = await RigidDiskBlockReader.Read(media.Stream);

        if (rigidDiskBlock == null)
        {
            throw new IOException($"Media '{mediaPath}' is not a valid Amiga Rigid Disk Block (RDB) disk.");
        }
            
        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
            
        if (partitionNumber < 0 || partitionNumber >= partitionBlocks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(partitionNumber), 
                $"Partition number {partitionNumber} is out of range. Available partitions: {partitionBlocks.Count}");
        }
            
        var partitionBlock = partitionBlocks[partitionNumber];
            
        return (media, await MountFileSystemVolume(mediaResult.Value.Stream, partitionBlock));
    }

    public static async Task CreateDirectory(
        TestCommandHelper testCommandHelper, string mediaPath, int partitionNumber, string[] dirPathComponents)
    {
        var (media, fileSystemVolume) = await MountFileSystemVolume(testCommandHelper, mediaPath, partitionNumber);

        foreach (var dirPathComponent in dirPathComponents)
        {
            await fileSystemVolume.CreateDirectory(dirPathComponent);
            await fileSystemVolume.ChangeDirectory(dirPathComponent);
        }
        
        fileSystemVolume.Dispose();
        media.Dispose();
    }
    
    public static async Task<IEnumerable<Amiga.FileSystems.Entry>> GetEntriesFromFileSystemVolume(TestCommandHelper testCommandHelper, string mediaPath,
        int partitionNumber, string[] dirPathComponents, bool writable = false)
    {
        var (media, fileSystemVolume) = await MountFileSystemVolume(testCommandHelper, mediaPath, partitionNumber, writable);

        foreach (var dirPathComponent in dirPathComponents)
        {
            await fileSystemVolume.ChangeDirectory(dirPathComponent);
        }

        var entries = await fileSystemVolume.ListEntries();
        
        fileSystemVolume.Dispose();
        media.Dispose();

        return entries;
    }
    
    public static async Task AddFileSystem(TestCommandHelper testCommandHelper, string path, 
        string dosType, byte[] fileSystemBytes)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
        using var media = mediaResult.Value;
        var stream = media.Stream;

        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

        rigidDiskBlock.AddFileSystem(DosTypeHelper.FormatDosType(dosType), fileSystemBytes);
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
    }
}