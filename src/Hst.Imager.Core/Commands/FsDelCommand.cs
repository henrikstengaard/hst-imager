using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.Caching;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.Models.FileSystems;
using Hst.Imager.Core.UaeMetadatas;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands;

public class FsDelCommand(
    ILogger<FsDelCommand> logger,
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string path,
    bool recursive,
    UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb)
    : FsCommandBase(commandHelper, physicalDrives)
{
    public override async Task<Result> Execute(CancellationToken token)
    {
        // first get an iterator to get a list of all files and direcgtories to delete.
        // then iterate over the list and delete each file and directory. this is to avoid iterating while deleting.
        // update iterator with a delete function?


        var entryIteratorResult = await GetEntryIterator(path);
        if (entryIteratorResult.IsFaulted)
        {
            return new Result(entryIteratorResult.Error);
        }
        
        var entryIterator = entryIteratorResult.Value;

        var entries = new List<Entry>();

        while (await entryIterator.Next())
        {
            var entry = entryIterator.Current;
            entries.Add(entry);
        }

        return new Result();
    }
    
    private async Task<Result<IEntryIterator>> GetEntryIterator(string path)
    {
        // resolve media path
        var mediaResult = commandHelper.ResolveMedia(path);
        
        // return directory entry iterator, if media path doesn't exist. otherwise return error
        if (mediaResult.IsFaulted)
        {
            if (mediaResult.Error is not PathNotFoundError)
            {
                return new Result<IEntryIterator>(mediaResult.Error);
            }

            return await GetDirectoryEntryIterator(path, recursive, uaeMetadata,
                new MemoryAppCache());
        }
        
        if (string.IsNullOrWhiteSpace(mediaResult.Value.FileSystemPath) &&
            (Directory.Exists(path) || File.Exists(path)))
        {
            return await GetDirectoryEntryIterator(path, recursive, uaeMetadata,
                new MemoryAppCache());
        }

        OnDebugMessage($"Media Path: '{mediaResult.Value.MediaPath}'");
        OnDebugMessage($"Virtual Path: '{mediaResult.Value.FileSystemPath}'");
        
        var writableMediaResult = await commandHelper.GetWritableMedia(physicalDrives, mediaResult.Value.MediaPath, mediaResult.Value.Modifiers);
        if (writableMediaResult.IsFaulted)
        {
            return new Result<IEntryIterator>(writableMediaResult.Error);
        }

        var fileSystemPath = mediaResult.Value.FileSystemPath ?? string.Empty;
        var directorySeparatorChar = mediaResult.Value.DirectorySeparatorChar;

        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(
            writableMediaResult.Value, fileSystemPath, directorySeparatorChar);

        var media = piStormRdbMediaResult.Media;
        fileSystemPath = piStormRdbMediaResult.FileSystemPath;

        var parts = fileSystemPath.Split(directorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        if (media.Type == Media.MediaType.Floppy)
        {
            return await GetFloppyEntryIterator(media, parts);
        }
        
        if (parts.Length < 3)
        {
            return new Result<IEntryIterator>(new Error($"Path '{path}' oesn't contain partition table, partition number and entry to delete"));
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "mbr":
                return await GetMbrPartitionEntryIterator(media, parts.Skip(1).ToArray());
            case "gpt":
                return await GetGptPartitionEntryIterator(media, parts.Skip(1).ToArray());
            case "rdb":
                return await GetRdbPartitionEntryIterator(media, parts.Skip(1).ToArray());
        }

        return new Result<IEntryIterator>(new Error($"Unsupported partition table '{parts[0]}' in path '{path}'"));
    }
    
    private async Task<Result<IEntryIterator>> GetFloppyEntryIterator(Media media, string[] parts)
    {
        var mbrFileSystemResult = await MountFileSystem(media.Stream);
        if (mbrFileSystemResult.IsFaulted)
        {
            return new Result<IEntryIterator>(mbrFileSystemResult.Error);
        }

        var entryIterator = new FileSystemEntryIterator(media, PartitionTableType.None, 0, mbrFileSystemResult.Value, parts, recursive);
        var initializeResult = await entryIterator.Initialize();
        return initializeResult.IsFaulted
            ? new Result<IEntryIterator>(initializeResult.Error)
            : new Result<IEntryIterator>(entryIterator);
    }

    private async Task<Result<IEntryIterator>> GetMbrPartitionEntryIterator(Media media, string[] parts)
    {
        var disk = await MediaHelper.ResolveVirtualDisk(media);
            
        var mbrFileSystemResult = await MountMbrFileSystem(disk, parts[0]);
        if (mbrFileSystemResult.IsFaulted)
        {
            return new Result<IEntryIterator>(mbrFileSystemResult.Error);
        }

        var (partitionNumber, fileSystem) = mbrFileSystemResult.Value;
        
        var rootPathComponents = parts.Skip(1).ToArray();
        var entryIterator = new FileSystemEntryIterator(media, PartitionTableType.MasterBootRecord, partitionNumber,
            fileSystem, rootPathComponents, recursive);
        var initializeResult = await entryIterator.Initialize();
        return initializeResult.IsFaulted
            ? new Result<IEntryIterator>(initializeResult.Error)
            : new Result<IEntryIterator>(entryIterator);
    }

    private async Task<Result<IEntryIterator>> GetGptPartitionEntryIterator(Media media, string[] parts)
    {
        var disk = await MediaHelper.ResolveVirtualDisk(media);
            
        var gptFileSystemResult = await MountGptFileSystem(disk, parts[0]);
        if (gptFileSystemResult.IsFaulted)
        {
            return new Result<IEntryIterator>(gptFileSystemResult.Error);
        }

        var (partitionNumber, fileSystem) = gptFileSystemResult.Value;

        var rootPathComponents = parts.Skip(1).ToArray();
        var entryIterator = new FileSystemEntryIterator(media, PartitionTableType.GuidPartitionTable, partitionNumber,
            fileSystem, rootPathComponents, recursive);
        var initializeResult = await entryIterator.Initialize();
        return initializeResult.IsFaulted
            ? new Result<IEntryIterator>(initializeResult.Error)
            : new Result<IEntryIterator>(entryIterator);
    }

    private async Task<Result<IEntryIterator>> GetRdbPartitionEntryIterator(Media media, string[] parts)
    {
        var volumeResult = await MountRdbFileSystemVolume(media, parts[0]);
        if (volumeResult.IsFaulted)
        {
            return new Result<IEntryIterator>(volumeResult.Error);
        }

        var (partitionNumber, fileSystemVolume) = volumeResult.Value;

        var rootPathComponents = parts.Skip(1).ToArray();
        var entryIterator = new AmigaVolumeEntryIterator(media, PartitionTableType.RigidDiskBlock, partitionNumber,
            fileSystemVolume, rootPathComponents, recursive);
        var initializeResult = await entryIterator.Initialize();
        return initializeResult.IsFaulted
            ? new Result<IEntryIterator>(initializeResult.Error)
            : new Result<IEntryIterator>(entryIterator);
    }
}