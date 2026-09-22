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

public class FsMoveCommand(
    ILogger<FsMoveCommand> logger,
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string fromPath,
    string toPath,
    bool forceOverwrite = false,
    UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb)
    : FsCommandBase(commandHelper, physicalDrives)
{
    private readonly ILogger<FsMoveCommand> logger = logger;

    public override async Task<Result> Execute(CancellationToken token)
    {
        // get destination entry writer
        var destEntryWriterResult = await GetEntryWriter(toPath, false, false, forceOverwrite);
        if (destEntryWriterResult.IsFaulted)
        {
            return new Result(destEntryWriterResult.Error);
        }
        
        using var destEntryWriter = destEntryWriterResult.Value;
        var destMediaIsDir = destEntryWriter.PathComponents.Length == destEntryWriter.DirPathComponents.Length &&
                             destEntryWriter.PathComponents.SequenceEqual(destEntryWriter.DirPathComponents);

        // get source entry iterator
        var srcEntryIteratorResult = await GetEntryIterator(fromPath);
        if (srcEntryIteratorResult.IsFaulted)
        {
            return new Result(srcEntryIteratorResult.Error);
        }
        
        using var srcEntryIterator = srcEntryIteratorResult.Value;
        var srcMediaIsDir = srcEntryIterator.PathComponents.Length == srcEntryIterator.DirPathComponents.Length &&
                             srcEntryIterator.PathComponents.SequenceEqual(srcEntryIterator.DirPathComponents);

        if (destEntryWriter.ArePathComponentsSelfCopy(srcEntryIterator))
        {
            return new Result(new SelfCopyError(
                $"Unable to move from source path '{fromPath}' to destination path '{toPath}' onto itself"));
        }
        
        if (destEntryWriter.ArePathComponentsCyclic(srcEntryIterator))
        {
            return new Result(new CyclicPathError(
                $"Unable to move cyclic path from source path '{fromPath}' to destination path '{toPath}'"));
        }

        srcEntryIterator.UaeMetadata = srcEntryIterator.SupportsUaeMetadata && uaeMetadata != UaeMetadata.None
            ? uaeMetadata
            : UaeMetadata.None;
        destEntryWriter.UaeMetadata = destEntryWriter.SupportsUaeMetadata && uaeMetadata != UaeMetadata.None
            ? uaeMetadata
            : UaeMetadata.None;

        var stopwatch = new Stopwatch();

        stopwatch.Start();

        OnInformationMessage($"Moving '{fromPath}' to '{toPath}'");

        var sameMedia = srcEntryIterator.Media.Equals(destEntryWriter.Media);

        var filesCount = 0;
        var dirsCount = 0;
        var totalBytes = 0L;

        var deleteEntries = new List<Entry>();
        string[] destRootPathComponents = [];
        var srcDirsMovedIndex = new HashSet<string>();
        var iterateSrcEntries = true;
        
        var entries = new List<Entry>();

        if (srcMediaIsDir && destMediaIsDir)
        {
            var destRootEntryName = srcEntryIterator.PathComponents.Last();
            var rootEntry = new Entry
            {
                Name = destRootEntryName,
                Type = EntryType.Dir,
                RelativePathComponents = [],
                FullPathComponents = srcEntryIterator.PathComponents
            };
            destRootPathComponents = [destRootEntryName];
            
            if (sameMedia && !srcEntryIterator.UsesPattern)
            {
                var moveResult = await MoveEntry(destEntryWriter, destRootPathComponents, rootEntry,
                    srcEntryIterator.IsSingleFileEntryNext);
                if (moveResult.IsFaulted)
                {
                    return new Result(moveResult.Error);
                }
                srcDirsMovedIndex.Add(string.Join("|", rootEntry.FullPathComponents));
                iterateSrcEntries = false;
                dirsCount++;
            }
            else
            {
                await destEntryWriter.CreateDirectory(rootEntry, destRootPathComponents,
                    false, srcEntryIterator.IsSingleFileEntryNext);
                deleteEntries.Add(rootEntry);
            }
        }
        
        var count = 0;
        
        while (iterateSrcEntries && await srcEntryIterator.Next())
        {
            count++;
            if (count >= 200)
            {
                count = 0;
                await srcEntryIterator.Flush();
            }

            var entry = srcEntryIterator.Current;

            // skip directory entries when there are no entry path components or when it's a single file.
            if (entry.Type == EntryType.Dir &&
                (srcEntryIterator.IsSingleFileEntryNext || entry.RelativePathComponents.Length == 0))
            {
                continue;
            }
            
            entries.Add(entry);
        }

        count = 0;
        await srcEntryIterator.Flush();

        var dirPathComponents = srcEntryIterator.DirPathComponents.Length > 0
            ? new[] { srcEntryIterator.DirPathComponents[^1] }
            : [];
        
        foreach (var entry in entries.OrderBy(e => e.RelativePathComponents.Length))
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
            
            var entryPathComponents = destRootPathComponents.Concat(entry.RelativePathComponents).ToArray();
            var pathComponents = srcEntryIterator.IsSingleFileEntryNext
                ? entryPathComponents
                : dirPathComponents.Concat(entryPathComponents).ToArray();
            
            OnInformationMessage($"{srcEntryIterator.MediaPath.Join(pathComponents)}");

            if (sameMedia)
            {
                var isSrcEntryPathMoved = false;
                
                for(var i = 1; i < entry.FullPathComponents.Length; i++)
                {
                    var srcEntryPathComponents = entry.FullPathComponents.Take(i).ToArray();
                    var srcEntryPathKey = string.Join("|", srcEntryPathComponents);
                    if (srcDirsMovedIndex.Contains(srcEntryPathKey))
                    {
                        isSrcEntryPathMoved = true;
                        break;
                    }
                }
                
                if (!isSrcEntryPathMoved)
                {
                    var moveResult = await MoveEntry(destEntryWriter, destRootPathComponents, entry,
                        srcEntryIterator.IsSingleFileEntryNext);
                    if (moveResult.IsFaulted)
                    {
                        return new Result(moveResult.Error);
                    }
                    var srcEntryPathKey = string.Join("|", entry.FullPathComponents);
                    srcDirsMovedIndex.Add(srcEntryPathKey);
                }
            }
            else
            {
                var copyResult = await CopyEntry(srcEntryIterator, entry, destRootPathComponents, destEntryWriter,
                    token);
                if (copyResult.IsFaulted)
                {
                    return new Result(copyResult.Error);
                }

                switch (entry.Type)
                {
                    case EntryType.File:
                    case EntryType.LinkFile:
                        filesCount++;
                        totalBytes += entry.Size;

                        var result = await srcEntryIterator.DeleteEntry(entry.FullPathComponents);
                        if (result.IsFaulted)
                        {
                            return result;
                        }

                        break;
                    case EntryType.Dir:
                    case EntryType.LinkDir:
                        dirsCount++;

                        deleteEntries.Add(entry);
                        break;
                }
            }
            
            count++;

            if (count <= 200)
            {
                continue;
            }

            count = 0;
            await srcEntryIterator.Flush();
            await destEntryWriter.Flush();
        }

        foreach (var entry in deleteEntries.OrderByDescending(x => x.FullPathComponents.Length))
        {
            token.ThrowIfCancellationRequested();
            var result = await srcEntryIterator.DeleteEntry(entry.FullPathComponents);
            if (result.IsFaulted)
            {
                return result;
            }
        }

        await srcEntryIterator.Flush();
        await destEntryWriter.Flush();

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
        stats.Add($"{totalBytes.FormatBytes()} moved in {stopwatch.Elapsed.FormatElapsed()}");

        OnInformationMessage(string.Join(", ", stats));
        
        return new Result();
    }

    /// <summary>
    /// Is single file or uses pattern examines if the operation involves 1 file or if the operation uses pattern.
    /// </summary>
    /// <param name="entry">Entry to examine.</param>
    /// <param name="entryIterator">Entry iterator to examine.</param>
    /// <returns>True, if entry is a file and there are no more entries or if there is only a single file entry next or if the entry iterator uses a pattern. Otherwise, false.</returns>
    private static bool IsSingleFileOrUsesPattern(Entry entry, IEntryIterator entryIterator) =>
        (entry.Type == EntryType.File && !entryIterator.HasMoreEntries) ||
        entryIterator.IsSingleFileEntryNext ||
        entryIterator.UsesPattern;

    private async Task<Result> MoveEntry(IEntryWriter destWriter, string[] destRootPathComponents, Entry entry,
        bool singleFile)
    {
        var destEntryPathComponents = destRootPathComponents.Concat(entry.RelativePathComponents).ToArray();

        var result = await destWriter.MoveEntry(entry, destEntryPathComponents, singleFile);

        return result.IsFaulted ? result : new Result();
    }

    private static async Task<Result> CopyEntry(IEntryIterator source, Entry entry,
        string[] destPathComponents, IEntryWriter destination, CancellationToken token)
    {
        if (entry.Type == EntryType.Dir && (source.IsSingleFileEntryNext || entry.RelativePathComponents.Length == 0))
        {
            return new Result();
        }

        var path = destPathComponents.Concat(entry.RelativePathComponents).ToArray();

        Result result;
        if (entry.Type is EntryType.Dir or EntryType.LinkDir)
        {
            result = await destination.CreateDirectory(entry, path, false, source.IsSingleFileEntryNext);
        }
        else
        {
            await using var stream = await source.OpenEntry(entry);
            result = await destination.CreateFile(entry, path, stream, false, source.IsSingleFileEntryNext);
        }

        return result.IsFaulted ? result : new Result();
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
