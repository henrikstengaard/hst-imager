namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Entry = Models.FileSystems.Entry;

public class ZipArchiveEntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly string rootPath;
    private readonly ZipArchive zipArchive;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private bool disposed;
    private readonly IDictionary<string, ZipArchiveEntry> zipEntryIndex;

    public ZipArchiveEntryIterator(Stream stream, string rootPath, ZipArchive zipArchive, bool recursive)
    {
        this.stream = stream;
        this.rootPath = rootPath;
        this.zipArchive = zipArchive;
        this.recursive = recursive;
        
        this.nextEntries = new Stack<Entry>();
        this.currentEntry = null;
        this.isFirst = true;
        this.zipEntryIndex = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
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

    public Task<bool> Next()
    {
        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            EnqueueEntries(rootPath);
        }

        if (this.nextEntries.Count <= 0)
        {
            return Task.FromResult(false);
        }

        currentEntry = this.nextEntries.Pop();

        return Task.FromResult(true);
    }
    
    public Task<Stream> OpenEntry(Entry entry)
    {
        if (!zipEntryIndex.ContainsKey(entry.RawPath))
        {
            throw new IOException($"Entry '{entry.RawPath}' not found");
        }

        return Task.FromResult(zipEntryIndex[entry.RawPath].Open());
    }

    public string[] GetPathComponents(string path)
    {
        return path.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);
    }

    public bool UsesFileNameMatcher => false;

    private void EnqueueEntries(string currentPath)
    {
        var dirs = new HashSet<string>();
        var currentPathComponents = currentPath.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);
        
        var zipEntries = this.zipArchive.Entries.OrderBy(x => x.FullName).ToList();

        var entries = new List<Entry>();
        
        for (var i = zipEntries.Count - 1; i >= 0; i--)
        {
            var zipEntry = zipEntries[i];

            var entryPath = GetEntryName(zipEntry.FullName);

            if (!string.IsNullOrEmpty(currentPath))
            {
                if (entryPath.Replace("/", "\\")
                        .IndexOf(string.Concat(currentPath, "\\").Replace("/", "\\"), StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
            }

            var entryFullPathComponents = entryPath.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);
            var entryRelativePathComponents = entryFullPathComponents.Skip(currentPathComponents.Length).ToArray();
            
            var isDir = zipEntry.FullName.EndsWith("\\") || zipEntry.FullName.EndsWith("/");

            if (isDir)
            {
                var dirFullPath = string.Join("/", entryFullPathComponents);
                if (!dirs.Contains(dirFullPath))
                {
                    dirs.Add(dirFullPath);
                    var dirRelativePath = string.Join("/", entryRelativePathComponents);
                    entries.Add(new Entry
                    {
                        Name = dirRelativePath,
                        FormattedName = dirRelativePath,
                        RawPath = dirFullPath,
                        FullPathComponents = entryFullPathComponents,
                        RelativePathComponents = entryRelativePathComponents,
                        Date = zipEntry.LastWriteTime.LocalDateTime,
                        Size = zipEntry.Length,
                        Type = Models.FileSystems.EntryType.Dir,
                        Attributes =
                            EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(0)),
                        Properties = new Dictionary<string, string>()
                    });
                }
                
                continue;
            }

            zipEntryIndex.Add(entryPath, zipEntry);
            
            if (entryFullPathComponents.Length > currentPathComponents.Length + 1)
            {
                for (var componentIndex = currentPathComponents.Length;
                     componentIndex < (recursive ? entryFullPathComponents.Length : Math.Min(currentPathComponents.Length + 2, entryFullPathComponents.Length));
                     componentIndex++)
                {
                    var dirFullPathComponents = entryFullPathComponents.Take(componentIndex).ToArray();
                    var dirRelativePathComponents = entryFullPathComponents.Skip(currentPathComponents.Length)
                        .Take(componentIndex - currentPathComponents.Length).ToArray();
                    
                    var dirFullPath = string.Join("/", dirFullPathComponents);
                    if (string.IsNullOrEmpty(dirFullPath) || dirs.Contains(dirFullPath))
                    {
                        continue;
                    }

                    dirs.Add(dirFullPath);
                    var dirRelativePath = string.Join("/", dirRelativePathComponents);
                    
                    entries.Add(new Entry
                    {
                        Name = dirRelativePath,
                        FormattedName = dirRelativePath,
                        RawPath = dirFullPath,
                        FullPathComponents = dirFullPathComponents,
                        RelativePathComponents = GetPathComponents(dirRelativePath),
                        Date = zipEntry.LastWriteTime.LocalDateTime,
                        Size = 0,
                        Type = Models.FileSystems.EntryType.Dir,
                        Attributes =
                            EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(0)),
                        Properties = new Dictionary<string, string>()
                    });
                }

                if (!recursive)
                {
                    continue;
                }
            }
            
            var entryRelativePath = string.Join("/", entryRelativePathComponents);
            
            entries.Add(new Entry
            {
                Name = entryRelativePath,
                FormattedName = entryRelativePath,
                RawPath = entryPath,
                RelativePathComponents = entryRelativePathComponents,
                Date = zipEntry.LastWriteTime.LocalDateTime,
                Size = zipEntry.Length,
                Type = Models.FileSystems.EntryType.File,
                Attributes =
                    EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(0)),
                Properties = new Dictionary<string, string>()
            });
        }
        
        foreach (var entry in entries.OrderByDescending(x => x.Name))
        {
            nextEntries.Push(entry);
        }
    }
    
    private string GetEntryName(string name)
    {
        var entryName = name.Replace("/", "\\");

        return entryName.EndsWith("\\") ? entryName[..^1]: entryName;
    }    
}