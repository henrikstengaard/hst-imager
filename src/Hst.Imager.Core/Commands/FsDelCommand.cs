using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.Caching;
using Hst.Imager.Core.Extensions;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.MagicBytes;
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
    UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb)
    : FsCommandBase(commandHelper, physicalDrives)
{
    public override async Task<Result> Execute(CancellationToken token)
    {
        var entryIteratorResult = await GetEntryIterator(path);
        if (entryIteratorResult.IsFaulted)
        {
            return new Result(entryIteratorResult.Error);
        }
        
        using var entryIterator = entryIteratorResult.Value;

        var stopwatch = new Stopwatch();

        stopwatch.Start();

        OnInformationMessage($"Deleting path '{path}'");

        var entries = new List<Entry>();

        while (await entryIterator.Next())
        {
            var entry = entryIterator.Current;
            
            // skip directory entries when there are no entry path components or when it's a single file.
            if (entry.Type == EntryType.Dir &&
                (entryIterator.IsSingleFileEntryNext || entry.RelativePathComponents.Length == 0))
            {
                continue;
            }
            
            entries.Add(entry);
        }

        var filesCount = 0;
        var dirsCount = 0;
        var totalBytes = 0L;

        var dirPathComponents = entryIterator.DirPathComponents.Length > 0
            ? new[] { entryIterator.DirPathComponents[^1] }
            : [];
        
        // delete entries starting with the longest path to ensure files are deleted before their parent directories
        foreach (var entry in entries.OrderByDescending(e => e.RelativePathComponents.Length))
        {
            switch (entry.Type)
            {
                case EntryType.Dir:
                case EntryType.LinkDir:
                    dirsCount++;
                    break;
                case EntryType.File:
                    filesCount++;
                    totalBytes += entry.Size;
                    break;
                case EntryType.LinkFile:
                    filesCount++;
                    break;
            }
            
            var pathComponents = entryIterator.IsSingleFileEntryNext
                ? entry.RelativePathComponents
                : dirPathComponents.Concat(entry.RelativePathComponents).ToArray();
            
            OnInformationMessage($"{entryIterator.MediaPath.Join(pathComponents)}");

            await entryIterator.DeleteEntry(entry.FullPathComponents);
        }

        // delete root directory, if present and is not a single file
        if (!entryIterator.IsSingleFileEntryNext &&
            entryIterator.PathComponents.Length == entryIterator.DirPathComponents.Length)
        {
            OnInformationMessage($"{entryIterator.DirPathComponents[^1]}");
            
            await entryIterator.DeleteEntry(entryIterator.DirPathComponents);
        }

        await entryIterator.Flush();

        stopwatch.Stop();

        var stats = new List<string>();
        if (dirsCount > 0 || filesCount == 0)
        {
            stats.Add($"{dirsCount} {(dirsCount > 1 ? "directories" : "directory")}");
        }
        if (filesCount > 0 || dirsCount == 0)
        {
            stats.Add($"{filesCount} {(filesCount == 1 ? "file" : "files")}");
        }
        stats.Add($"{totalBytes.FormatBytes()} deleted in {stopwatch.Elapsed.FormatElapsed()}");

        OnInformationMessage(string.Join(", ", stats));

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

            var directoryEntryIteratorResult = await GetDirectoryEntryIterator(path, true, uaeMetadata,
                new MemoryAppCache());
            if (directoryEntryIteratorResult.IsFaulted)
            {
                return new Result<IEntryIterator>(directoryEntryIteratorResult.Error);
            }
            var initializeResult = await directoryEntryIteratorResult.Value.Initialize();
            return initializeResult.IsFaulted
                ? new Result<IEntryIterator>(initializeResult.Error)
                : directoryEntryIteratorResult;
        }

        var dataType = await commandHelper.DetectDataType(mediaResult.Value.MediaPath);
        
        if (string.IsNullOrWhiteSpace(mediaResult.Value.FileSystemPath) &&
            (Directory.Exists(path) || File.Exists(path)))
        {
            var entryIteratorResult = await GetDirectoryEntryIterator(path, true, uaeMetadata,
                new MemoryAppCache());
            if (entryIteratorResult.IsFaulted)
            {
                return new Result<IEntryIterator>(entryIteratorResult.Error);
            }
            var initializeResult = await entryIteratorResult.Value.Initialize();
            return initializeResult.IsFaulted
                ? new Result<IEntryIterator>(initializeResult.Error)
                : entryIteratorResult;
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
        
        if (dataType == DataType.Adf)
        {
            return await GetAdfPartitionEntryIterator(media, parts);
        }
        
        if (parts.Length < 3)
        {
            return new Result<IEntryIterator>(new Error($"Path '{path}' doesn't contain partition table, partition number and entry to delete"));
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

        var entryIterator = new FileSystemEntryIterator(media, PartitionTableType.None, 0, mbrFileSystemResult.Value, parts, true);
        var initializeResult = await entryIterator.Initialize();
        return initializeResult.IsFaulted
            ? new Result<IEntryIterator>(initializeResult.Error)
            : new Result<IEntryIterator>(entryIterator);
    }

    private async Task<Result<IEntryIterator>> GetAdfPartitionEntryIterator(Media media, string[] parts)
    {
        var fileSystemVolumeResult = await MountAdfFileSystemVolume(media.Stream);
        if (fileSystemVolumeResult.IsFaulted)
        {
            return new Result<IEntryIterator>(fileSystemVolumeResult.Error);
        }

        await using var fileSystemVolume = fileSystemVolumeResult.Value;
        using var entryIterator = new AmigaVolumeEntryIterator(media, PartitionTableType.RigidDiskBlock,
            0, fileSystemVolume, parts, true);

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
            fileSystem, rootPathComponents, true);
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
            fileSystem, rootPathComponents, true);
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
            fileSystemVolume, rootPathComponents, true);
        var initializeResult = await entryIterator.Initialize();
        return initializeResult.IsFaulted
            ? new Result<IEntryIterator>(initializeResult.Error)
            : new Result<IEntryIterator>(entryIterator);
    }
}