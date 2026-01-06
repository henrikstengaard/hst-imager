using Hst.Core;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using PathComponents;
using UaeMetadatas;
using Entry = Models.FileSystems.Entry;
using FileMode = Amiga.FileSystems.FileMode;

/// <summary>
/// Amiga volume entry iterator.
/// </summary>
/// <param name="media">Media used by iterator.</param>
/// <param name="partitionTableType">Partition table type used by iterator.</param>
/// <param name="partitionNumber">Partition number used by iterator.</param>
/// <param name="fileSystemVolume">Amiga file system volume to iterate through.</param>
/// <param name="rootPathComponents">Root path components to root of iterator.</param>
/// <param name="recursive">Iterate recursively.</param>
public class AmigaVolumeEntryIterator(
    Media media,
    PartitionTableType partitionTableType,
    int partitionNumber,
    IFileSystemVolume fileSystemVolume,
    string[] rootPathComponents,
    bool recursive)
    : IEntryIterator
{
    private readonly IMediaPath mediaPath = MediaPath.AmigaOsPath;
    private PathComponentMatcher pathComponentMatcher;
    private readonly Stack<Entry> nextEntries = new();
    private bool isFirst = true;
    private Entry currentEntry = null;

    public PartitionTableType PartitionTableType => partitionTableType;
    public int PartitionNumber => partitionNumber;

    public string[] PathComponents => rootPathComponents;

    /// <summary>
    /// Dir path components from root path components that exist and is set during initialization.
    /// </summary>
    public string[] DirPathComponents { get; private set; } = [];

    public void Dispose()
    {
    }

    public Media Media => media;

    public Entry Current => currentEntry;

    public bool HasMoreEntries => nextEntries.Count > 0;

    public bool IsSingleFileEntryNext { get; private set; } = false;

    public async Task<Result> Initialize()
    {
        await fileSystemVolume.ChangeDirectory("/");

        var dirComponents = 0;
        var usePattern = false;
        foreach (var pathComponent in rootPathComponents)
        {
            var findEntryResult = await fileSystemVolume.FindEntry(pathComponent);
            
            var exists = !findEntryResult.PartsNotFound.Any();
            var isDir = findEntryResult.Entry is { Type: EntryType.Dir } or { Type: EntryType.DirLink };
            var isFile = findEntryResult.Entry is { Type: EntryType.File } or { Type: EntryType.FileLink };
            
            // change directory if directory exists for path component
            if (exists && isDir)
            {
                dirComponents++;
                await fileSystemVolume.ChangeDirectory(pathComponent);
                continue;
            }
            
            // last part component
            if (dirComponents == rootPathComponents.Length - 1)
            {
                usePattern = true;
                IsSingleFileEntryNext = exists && isFile && PathComponents.Length > 0;
                if (IsSingleFileEntryNext || PathComponentHelper.HasWildcard(pathComponent))
                {
                    break;
                }
            }
            
            var path = string.Join("/",
                rootPathComponents.Take(dirComponents).Concat(findEntryResult.PartsNotFound));
            return new Result(new PathNotFoundError(
                $"Path not found '{path}'", path));
        }

        DirPathComponents = rootPathComponents.Take(dirComponents).ToArray();
        pathComponentMatcher = new PathComponentMatcher(usePattern ? rootPathComponents : [], 
            isFile: IsSingleFileEntryNext, recursive: recursive);

        return new Result();
    }

    public async Task<bool> Next()
    {
        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            await EnqueueDirectory(DirPathComponents);
        }

        if (nextEntries.Count <= 0)
        {
            return false;
        }

        bool skipEntry;
        do
        {
            skipEntry = false;
            currentEntry = nextEntries.Pop();

            if (currentEntry.Type == Models.FileSystems.EntryType.File)
            {
                return true;
            }

            if (recursive)
            {
                var (dirEntriesEnqueued, fileEntriesEnqueued) = await EnqueueDirectory(currentEntry.FullPathComponents);
                skipEntry = pathComponentMatcher.UsesPattern && dirEntriesEnqueued + fileEntriesEnqueued == 0;
            }
            else
            {
                skipEntry = !EntryIteratorFunctions.IsRelativePathComponentsValid(pathComponentMatcher,
                    currentEntry.RelativePathComponents, recursive);
            }
        } while (nextEntries.Count > 0 && skipEntry);

        return true;
    }

    public async Task<Stream> OpenEntry(Entry entry)
    {
        await fileSystemVolume.ChangeDirectory("/");

        for (var i = 0; i < entry.FullPathComponents.Length - 1; i++)
        {
            await fileSystemVolume.ChangeDirectory(entry.FullPathComponents[i]);
        }

        return await fileSystemVolume.OpenFile(entry.FullPathComponents[^1], FileMode.Read, true);
    }

    public string[] GetPathComponents(string path)
    {
        return path.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);
    }

    public bool UsesPattern => pathComponentMatcher.UsesPattern;

    public async Task Flush()
    {
        await fileSystemVolume.Flush();
    }

    public bool SupportsUaeMetadata => true;

    /// <summary>
    /// Enqueue directory by iterating entries in path.
    /// </summary>
    /// <param name="currentPathComponents">Path to enqueue.</param>
    /// <returns>Number of entries enqueued.</returns>
    private async Task<(int, int)> EnqueueDirectory(string[] currentPathComponents)
    {
        await fileSystemVolume.ChangeDirectory("/");

        foreach (var name in currentPathComponents)
        {
            await fileSystemVolume.ChangeDirectory(name);
        }

        var entries = (await fileSystemVolume.ListEntries()).OrderBy(x => x.Name).ToList();
        var directories = new List<Entry>();
        var files = new List<Entry>();

        foreach (var entry in entries)
        {
            var fullPathComponents = currentPathComponents.Concat(new[] { entry.Name }).ToArray();

            var entryPath = mediaPath.Join(fullPathComponents);

            var isDir = entry.Type == EntryType.Dir || entry.Type == EntryType.DirLink;

            var dirAttributes = EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(0));

            var attributes = EntryFormatter.FormatProtectionBits(entry.ProtectionBits);
            var properties = new Dictionary<string, string>
            {
                { Constants.EntryPropertyNames.Comment, entry.Comment },
                { Constants.EntryPropertyNames.ProtectionBits, ((int)entry.ProtectionBits ^ 0xf).ToString() }
            };

            var iteratorEntry = EntryIteratorFunctions.CreateEntry(mediaPath, DirPathComponents,
                recursive, entryPath, entryPath, isDir, entry.Date, entry.Size,
                attributes, properties, dirAttributes);

            // skip if no entry was created or entry is a file and is not valid
            var isValid = EntryIteratorFunctions.IsRelativePathComponentsValid2(iteratorEntry.RelativePathComponents, recursive) && 
                          pathComponentMatcher.IsMatch(iteratorEntry.FullPathComponents);
            if (iteratorEntry == null ||
                (iteratorEntry.Type == Models.FileSystems.EntryType.File &&
                 !isValid))
            {
                continue;
            }
            
            switch (iteratorEntry.Type)
            {
                case Models.FileSystems.EntryType.Dir:
                    directories.Add(iteratorEntry);
                    break;
                case Models.FileSystems.EntryType.File:
                    files.Add(iteratorEntry);
                    break;
            }
        }

        for (var i = files.Count - 1; i >= 0; i--)
        {
            nextEntries.Push(files[i]);
        }

        for (var i = directories.Count - 1; i >= 0; i--)
        {
            nextEntries.Push(directories[i]);
        }

        return (directories.Count, files.Count);
    }

    public UaeMetadata UaeMetadata { get; set; }
}