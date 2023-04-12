namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Entry = Models.FileSystems.Entry;
using EntryType = Models.FileSystems.EntryType;

public class ZipArchiveEntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly string rootPath;
    private string[] rootPathComponents;
    private PathComponentMatcher pathComponentMatcher;
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
        this.rootPathComponents = GetPathComponents(rootPath);
        this.pathComponentMatcher = null;
        this.zipArchive = zipArchive;
        this.recursive = recursive;
        this.nextEntries = new Stack<Entry>();
        this.currentEntry = null;
        this.isFirst = true;
        this.zipEntryIndex = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
    }

    private void ResolvePathComponentMatcher()
    {
        var pathComponents = GetPathComponents(rootPath);

        if (pathComponents.Length == 0)
        {
            this.rootPathComponents = pathComponents;
            this.pathComponentMatcher = new PathComponentMatcher(pathComponents, recursive: recursive);
            return;
        }

        var hasPattern = pathComponents[^1].IndexOf("*", StringComparison.OrdinalIgnoreCase) >= 0;
        this.rootPathComponents =
            hasPattern ? pathComponents.Take(pathComponents.Length - 1).ToArray() : pathComponents;
        this.pathComponentMatcher =
            new PathComponentMatcher(rootPathComponents, hasPattern ? pathComponents[^1] : null, recursive);
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
            ResolvePathComponentMatcher();
            EnqueueEntries();
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
        return path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private void EnqueueEntries()
    {
        var dirs = new HashSet<string>();
        var currentPathComponents = new List<string>(this.rootPathComponents).ToArray();
        var zipEntries = this.zipArchive.Entries.OrderBy(x => x.FullName).ToList();

        var entries = new List<Entry>();

        for (var i = zipEntries.Count - 1; i >= 0; i--)
        {
            var zipEntry = zipEntries[i];

            var entryPath = GetEntryName(zipEntry.FullName);

            var entryFullPathComponents = entryPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var entryRelativePathComponents = entryFullPathComponents.Skip(currentPathComponents.Length).ToArray();

            var isDir = zipEntry.FullName.EndsWith("\\") || zipEntry.FullName.EndsWith("/");

            // add directory entry, if entry is a path (ends with directory separator)
            if (isDir)
            {
                var dirFullPath = string.Join("/", entryFullPathComponents);
                if (!dirs.Contains(dirFullPath))
                {
                    dirs.Add(dirFullPath);
                    var dirRelativePath = string.Join("/", entryRelativePathComponents);
                    var dirEntry = new Entry
                    {
                        Name = dirRelativePath,
                        FormattedName = dirRelativePath,
                        RawPath = dirFullPath,
                        FullPathComponents = entryFullPathComponents,
                        RelativePathComponents = entryRelativePathComponents,
                        Date = zipEntry.LastWriteTime.LocalDateTime,
                        Size = zipEntry.Length,
                        Type = EntryType.Dir,
                        Attributes =
                            EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(0)),
                        Properties = new Dictionary<string, string>()
                    };

                    if (this.pathComponentMatcher.IsMatch(dirEntry.FullPathComponents))
                    {
                        entries.Add(dirEntry);
                    }
                }

                continue;
            }
            
            // if entry path components is equal or larger then root path components + 2,
            // then add dirs for entry
            if (entryFullPathComponents.Length >= currentPathComponents.Length + 2)
            {
                var maxComponents = recursive ? entryFullPathComponents.Length - currentPathComponents.Length - 1 : 1;
                for (var component = 1; component <= maxComponents; component++)
                {
                    var dirFullPathComponents = entryFullPathComponents.Take(component).ToArray();
                    var dirRelativePathComponents = entryFullPathComponents.Skip(currentPathComponents.Length)
                        .Take(component).ToArray();
                    
                    // skip, if relative dir path compoents to directory is zero
                    if (dirRelativePathComponents.Length == 0)
                    {
                        continue;
                    }
                    
                    var dirFullPath = string.Join("/", dirFullPathComponents);
                    
                    // skip, if full dir path is empty or already added
                    if (string.IsNullOrEmpty(dirFullPath) || dirs.Contains(dirFullPath))
                    {
                        continue;
                    }

                    dirs.Add(dirFullPath);
                    var dirRelativePath = string.Join("/", dirRelativePathComponents);
                    
                    var dirEntry = new Entry
                    {
                        Name = dirRelativePath,
                        FormattedName = dirRelativePath,
                        RawPath = dirFullPath,
                        FullPathComponents = dirFullPathComponents,
                        RelativePathComponents = GetPathComponents(dirRelativePath),
                        Date = zipEntry.LastWriteTime.LocalDateTime,
                        Size = 0,
                        Type = EntryType.Dir,
                        Attributes =
                            EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(0)),
                        Properties = new Dictionary<string, string>()
                    };
                    
                    if (this.pathComponentMatcher.IsMatch(dirEntry.FullPathComponents))
                    {
                        entries.Add(dirEntry);
                    }
                }

                if (!recursive)
                {
                    continue;
                }
            }

            zipEntryIndex.Add(entryPath, zipEntry);
            
            var fileRelativePathComponents = entryFullPathComponents.Skip(currentPathComponents.Length - (entryFullPathComponents.Length == currentPathComponents.Length ? 1 : 0)).ToArray();
            var fileRelativePath = string.Join("/", fileRelativePathComponents);

            var fileEntry = new Entry
            {
                Name = fileRelativePath,
                FormattedName = fileRelativePath,
                RawPath = entryPath,
                FullPathComponents = entryFullPathComponents,
                RelativePathComponents = fileRelativePathComponents,
                Date = zipEntry.LastWriteTime.LocalDateTime,
                Size = zipEntry.Length,
                Type = EntryType.File,
                Attributes =
                    EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(0)),
                Properties = new Dictionary<string, string>()
            };

            if (this.pathComponentMatcher.IsMatch(fileEntry.FullPathComponents))
            {
                entries.Add(fileEntry);
            }
        }
        
        foreach (var entry in entries.OrderByDescending(x => x.Name))
        {
            nextEntries.Push(entry);
        }
    }

    private string GetEntryName(string name)
    {
        var entryName = name.Replace("/", "\\");

        return entryName.EndsWith("\\") ? entryName[..^1] : entryName;
    }
    
    public bool UsesPattern => this.pathComponentMatcher?.UsesPattern ?? false;
    
    public Task Flush()
    {
        return Task.CompletedTask;
    }
}