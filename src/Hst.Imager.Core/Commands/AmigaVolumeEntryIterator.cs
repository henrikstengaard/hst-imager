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

public class AmigaVolumeEntryIterator(
    Media media,
    Stream stream,
    string rootPath,
    IFileSystemVolume fileSystemVolume,
    bool recursive)
    : IEntryIterator
{
    private readonly Stream stream = stream;
    private readonly IMediaPath mediaPath = MediaPath.AmigaOsPath;
    private string[] rootPathComponents = [];
    private PathComponentMatcher pathComponentMatcher;
    private readonly Stack<Entry> nextEntries = new();
    private bool isFirst = true;
    private Entry currentEntry = null;

    public void Dispose()
    {
    }

    public Media Media => media;
    public string RootPath => rootPath;

    public Entry Current => currentEntry;

    public bool HasMoreEntries => nextEntries.Count > 0;
    public bool IsSingleFileEntryNext => 1 == nextEntries.Count && 
                                         nextEntries.All(x => x.Type == Models.FileSystems.EntryType.File);

    public async Task<bool> Next()
    {
        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            await ResolveRootPath(rootPath);
            await EnqueueDirectory(rootPathComponents);
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
                var entriesEnqueued = await EnqueueDirectory(currentEntry.FullPathComponents);
                skipEntry = pathComponentMatcher.UsesPattern && entriesEnqueued == 0;
            }
            else
            {
                skipEntry = !EntryIteratorFunctions.IsRelativePathComponentsValid(pathComponentMatcher,
                    currentEntry.RelativePathComponents, recursive);
            }
        } while (nextEntries.Count > 0 && skipEntry);

        return true;
    }

    private async Task ResolveRootPath(string path)
    {
        var pathComponents = GetPathComponents(path);
        
        await fileSystemVolume.ChangeDirectory("/");

        var dirComponents = 0;
        var usePattern = false;
        foreach (var pathComponent in pathComponents)
        {
            var findEntryResult = await fileSystemVolume.FindEntry(pathComponent);

            if (!findEntryResult.PartsNotFound.Any() && findEntryResult.Entry.Type == EntryType.Dir)
            {
                dirComponents++;
                await fileSystemVolume.ChangeDirectory(pathComponent);
                continue;
            }
            
            if (dirComponents == pathComponents.Length - 1)
            {
                usePattern = true;
                break;
            }
            
            if (findEntryResult.PartsNotFound.Any())
            {
                throw new IOException(
                    $"Path not found '{string.Join("/", pathComponents.Take(dirComponents).Concat(findEntryResult.PartsNotFound))}'");
            }
        }

        rootPathComponents = pathComponents.Take(dirComponents).ToArray();
        pathComponentMatcher = new PathComponentMatcher(usePattern ? pathComponents : Array.Empty<string>(), recursive);
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

    /// <summary>
    /// Enqueue directory by iterating entries in path.
    /// </summary>
    /// <param name="currentPathComponents">Path to enqueue.</param>
    /// <returns>Number of entries enqueued.</returns>
    private async Task<int> EnqueueDirectory(string[] currentPathComponents)
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

            var iteratorEntry = EntryIteratorFunctions.CreateEntry(mediaPath, rootPathComponents,
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

        return files.Count + directories.Count;
    }

    public UaeMetadata UaeMetadata { get; set; }
}