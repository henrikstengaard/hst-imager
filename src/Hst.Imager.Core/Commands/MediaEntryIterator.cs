namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Models;

public class MediaEntryIterator : IEntryIterator
{
    private readonly Media media;
    private readonly string path;
    private readonly IFileSystemVolume fileSystemVolume;
    private readonly bool recursive;
    private readonly Queue<Hst.Imager.Core.Models.FileSystems.Entry> dirs;
    private readonly Queue<Hst.Imager.Core.Models.FileSystems.Entry> files;
    private readonly Stack<string> currentPaths;
    private string currentPath;
    private Hst.Imager.Core.Models.FileSystems.Entry currentEntry;
    private bool disposed;
    
    public MediaEntryIterator(Media media, string path, IFileSystemVolume fileSystemVolume, bool recursive)
    {
        this.media = media;
        this.path = path;
        this.fileSystemVolume = fileSystemVolume;
        this.recursive = recursive;
        this.dirs = new Queue<Hst.Imager.Core.Models.FileSystems.Entry>();
        this.files = new Queue<Hst.Imager.Core.Models.FileSystems.Entry>();
        this.currentPaths = new Stack<string>();
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            media.Dispose();
        }

        disposed = true;
    }

    public void Dispose() => Dispose(true);

    public Hst.Imager.Core.Models.FileSystems.Entry Current => currentEntry;

    public async Task<bool> Next()
    {
        // first time, current path is null and enqueue root path
        if (currentPath == null)
        {
            currentEntry = null;
            currentPath = path;
            currentPaths.Push(currentPath);
            await EnqueueCurrentDirectory();
        }
        
        // no more files left in queue, enqueue next directory
        if (this.files.Count == 0)
        {
            if (!this.recursive || this.dirs.Count == 0)
            {
                currentEntry = null;
                return false;
            }

            var nextDirEntry = this.dirs.Dequeue();
            currentPath = nextDirEntry.Path;
            currentEntry = nextDirEntry;
            currentPaths.Push(currentPath);
            await EnqueueCurrentDirectory();
            return true;
        }
        
        // no more files, return null
        if (this.files.Count == 0)
        {
            Console.WriteLine("no more files end");
            currentEntry = null;
            return false;
        }

        var nextFileEntry = this.files.Dequeue();
        currentEntry = nextFileEntry;
        
        return true;
    }
    
    private async Task EnqueueCurrentDirectory()
    {
        await fileSystemVolume.ChangeDirectory("/");
        foreach (var name in currentPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            await fileSystemVolume.ChangeDirectory(name);
        }
        
        var entries = (await fileSystemVolume.ListEntries()).ToList();

        foreach (var entry in entries)
        {
            var entryPath = string.IsNullOrEmpty(currentPath)
                ? entry.Name
                : string.Concat(currentPath, "/", entry.Name);
            
            switch (entry.Type)
            {
                case EntryType.DirLink:
                case EntryType.Dir:
                    if (!this.recursive)
                    {
                        continue;
                    }
                    dirs.Enqueue(new Hst.Imager.Core.Models.FileSystems.Entry
                    {
                        Name = entry.Name,
                        Path = entryPath,
                        Date = entry.Date,
                        Size = 0,
                        Type = Models.FileSystems.EntryType.Dir,
                        Attributes = EntryFormatter.FormatProtectionBits(entry.ProtectionBits)
                    });
                    break;
                case EntryType.FileLink:
                case EntryType.File:
                    files.Enqueue(new Hst.Imager.Core.Models.FileSystems.Entry
                    {
                        Name = entry.Name,
                        Path = entryPath,
                        Date = entry.Date,
                        Size = entry.Size,
                        Type = Models.FileSystems.EntryType.File,
                        Attributes = EntryFormatter.FormatProtectionBits(entry.ProtectionBits)
                    });
                    break;
            }
        }
        Console.WriteLine($"{files.Count} files, {dirs.Count} dirs");
    }    
}