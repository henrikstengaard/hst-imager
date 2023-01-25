﻿namespace Hst.Imager.Core.Commands;

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
            await EnqueueDirectory(rootPath.Split('/', StringSplitOptions.RemoveEmptyEntries));
        }

        if (this.nextEntries.Count <= 0)
        {
            return false;
        }

        currentEntry = this.nextEntries.Pop();
        if (this.recursive && currentEntry.Type == Models.FileSystems.EntryType.Dir)
        {
            await EnqueueDirectory(currentEntry.PathComponents);
        }

        return true;
    }

    public async Task<Stream> OpenEntry(Entry entry)
    {
        await fileSystemVolume.ChangeDirectory("/");
        
        for (var i = 0; i < entry.PathComponents.Length - 1; i++)
        {
            await fileSystemVolume.ChangeDirectory(entry.PathComponents[i]);
        }

        return await fileSystemVolume.OpenFile(entry.PathComponents[^1], FileMode.Read, true);
    }

    public string[] GetPathComponents(string path)
    {
        return path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private async Task EnqueueDirectory(string[] currentPathComponents)
    {
        // var currentPathComponents = 
        
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
            var entryPathComponents = currentPathComponents.Concat(new[] { entry.Name }).ToArray();
            var entryName = string.Join("/", entryPathComponents);
            if (!string.IsNullOrEmpty(rootPath))
            {
                entryName = entryName.Substring(rootPath.Length + 1);
            }
            
            switch (entry.Type)
            {
                case EntryType.DirLink:
                case EntryType.Dir:
                    directories.Add(new Entry
                    {
                        Name = entry.Name,
                        FormattedName = entryName,
                        RawPath = string.Join("/", entryPathComponents),
                        PathComponents = entryPathComponents,
                        Date = entry.Date,
                        Size = 0,
                        Type = Models.FileSystems.EntryType.Dir,
                        Properties = new Dictionary<string, string>
                        {
                            {"Comment", entry.Comment }
                        },
                        Attributes = EntryFormatter.FormatProtectionBits(entry.ProtectionBits)
                    });
                    break;
                case EntryType.FileLink:
                case EntryType.File:
                    files.Add(new Entry
                    {
                        Name = entry.Name,
                        FormattedName = entryName,
                        RawPath = string.Join("/", entryPathComponents),
                        PathComponents = entryPathComponents,
                        Date = entry.Date,
                        Size = entry.Size,
                        Type = Models.FileSystems.EntryType.File,
                        Properties = new Dictionary<string, string>
                        {
                            {"Comment", entry.Comment }
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