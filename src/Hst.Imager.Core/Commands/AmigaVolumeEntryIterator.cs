namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Entry = Models.FileSystems.Entry;
using FileMode = Amiga.FileSystems.FileMode;

public class AmigaVolumeEntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly string rootPath;
    private string[] rootPathComponents;
    private PatternMatcher patternMatcher;
    private readonly IFileSystemVolume fileSystemVolume;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private bool disposed;

    public AmigaVolumeEntryIterator(Stream stream, string rootPath, IFileSystemVolume fileSystemVolume, bool recursive)
    {
        this.stream = stream;
        this.rootPath = rootPath;
        this.rootPathComponents = Array.Empty<string>();
        this.patternMatcher = null;
        this.fileSystemVolume = fileSystemVolume;
        this.recursive = recursive;
        this.nextEntries = new Stack<Entry>();
        this.currentEntry = null;
        this.isFirst = true;
    }

    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            stream.Dispose();
        }

        disposed = true;
    }

    public void Dispose() => Dispose(true);

    public string RootPath => rootPath;

    public Entry Current => currentEntry;

    public async Task<bool> Next()
    {
        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            await ResolveRootPath(this.rootPath);
            await EnqueueDirectory(this.rootPathComponents);
        }

        if (this.nextEntries.Count <= 0)
        {
            return false;
        }

        bool skipEntry;
        do
        {
            skipEntry = false;
            currentEntry = this.nextEntries.Pop();
            if (this.recursive && currentEntry.Type == Models.FileSystems.EntryType.Dir)
            {
                await EnqueueDirectory(currentEntry.FullPathComponents);
                skipEntry = this.patternMatcher != null && !this.patternMatcher.IsMatch(currentEntry.Name);
            }
        } while (currentEntry.Type == Models.FileSystems.EntryType.Dir && skipEntry);

        return true;
    }

    private async Task ResolveRootPath(string path)
    {
        var pathComponents = GetPathComponents(path);
        
        await fileSystemVolume.ChangeDirectory("/");

        var dirComponents = 0;
        foreach (var pathComponent in pathComponents)
        {
            var result = await fileSystemVolume.FindEntry(pathComponent);

            if (!result.PartsNotFound.Any() && result.Entry.Type == EntryType.Dir)
            {
                dirComponents++;
                await fileSystemVolume.ChangeDirectory(pathComponent);
                continue;
            }
            
            if (dirComponents == pathComponents.Length - 1)
            {
                this.patternMatcher = new PatternMatcher(pathComponent);
                break;
            }
            
            if (result.PartsNotFound.Any())
            {
                throw new IOException(
                    $"Path not found '{string.Join("/", pathComponents.Take(dirComponents).Concat(result.PartsNotFound))}'");
            }
        }

        rootPathComponents = pathComponents.Take(dirComponents).ToArray();
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

    public bool UsesPattern => this.patternMatcher != null;
    
    public async Task Flush()
    {
        await this.fileSystemVolume.Flush();
    }

    private async Task EnqueueDirectory(string[] currentPathComponents)
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
            var relativePathComponents = fullPathComponents.Skip(this.rootPathComponents.Length).ToArray();

            var entryName = string.Join("/", relativePathComponents);

            switch (entry.Type)
            {
                case EntryType.DirLink:
                case EntryType.Dir:
                    directories.Add(new Entry
                    {
                        Name = entry.Name,
                        FormattedName = entryName,
                        RawPath = string.Join("/", fullPathComponents),
                        RelativePathComponents = relativePathComponents,
                        FullPathComponents = fullPathComponents,
                        Date = entry.Date,
                        Size = 0,
                        Type = Models.FileSystems.EntryType.Dir,
                        Properties = new Dictionary<string, string>
                        {
                            { "Comment", entry.Comment }
                        },
                        Attributes = EntryFormatter.FormatProtectionBits(entry.ProtectionBits)
                    });
                    break;
                case EntryType.FileLink:
                case EntryType.File:
                    // skip file entry, if pattern matcher is set and entry name doesn't match
                    if (this.patternMatcher != null &&
                        !this.patternMatcher.IsMatch(entry.Name))
                    {
                        continue;
                    }
            
                    files.Add(new Entry
                    {
                        Name = entry.Name,
                        FormattedName = entryName,
                        RawPath = string.Join("/", fullPathComponents),
                        RelativePathComponents = relativePathComponents,
                        FullPathComponents = fullPathComponents,
                        Date = entry.Date,
                        Size = entry.Size,
                        Type = Models.FileSystems.EntryType.File,
                        Properties = new Dictionary<string, string>
                        {
                            { "Comment", entry.Comment }
                        },
                        Attributes = EntryFormatter.FormatProtectionBits(entry.ProtectionBits)
                    });
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
    }
}