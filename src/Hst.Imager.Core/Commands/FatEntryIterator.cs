namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils.Fat;
using Models;
using Entry = Models.FileSystems.Entry;

public class FatEntryIterator : IEntryIterator
{
    private readonly Media media;
    private readonly string rootPath;
    private string[] rootPathComponents;
    private PathComponentMatcher pathComponentMatcher;
    private readonly FatFileSystem fatFileSystem;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private bool disposed;

    public FatEntryIterator(Media media, FatFileSystem fatFileSystem, string rootPath, bool recursive)
    {
        this.media = media;
        this.rootPath = rootPath.EndsWith("\\") ? rootPath : string.Concat(rootPath, "\\");
        this.rootPathComponents = GetPathComponents(this.rootPath);
        this.pathComponentMatcher = null;
        this.recursive = recursive;
        this.fatFileSystem = fatFileSystem;
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
            fatFileSystem.Dispose();
            media.Dispose();
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
            ResolveRootPath();
            EnqueueDirectory(this.rootPathComponents);
        }
        
        if (this.nextEntries.Count <= 0)
        {
            return Task.FromResult(false);
        }
        
        if (this.pathComponentMatcher.UsesPattern)
        {
            do
            {
                if (this.nextEntries.Count <= 0)
                {
                    return Task.FromResult(false);
                }
                currentEntry = this.nextEntries.Pop();
                if (this.recursive && currentEntry.Type == Models.FileSystems.EntryType.Dir)
                {
                    EnqueueDirectory(currentEntry.FullPathComponents);
                }
            } while (currentEntry.Type == Models.FileSystems.EntryType.Dir);
        }
        else
        {
            currentEntry = this.nextEntries.Pop();
            if (this.recursive && currentEntry.Type == Models.FileSystems.EntryType.Dir)
            {
                EnqueueDirectory(currentEntry.FullPathComponents);
            }
        }

        return Task.FromResult(true);
    }
    
    private void ResolveRootPath()
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
    
    private void EnqueueDirectory(string[] currentPathComponents)
    {
        var currentPath = string.Join("\\", currentPathComponents);

        try
        {
            foreach (var dirPath in fatFileSystem.GetDirectories(currentPath).OrderByDescending(x => x).ToList())
            {
                var fullPathComponents = GetPathComponents(dirPath);
                var relativePathComponents = fullPathComponents.Skip(this.rootPathComponents.Length).ToArray();
                var relativePath = string.Join(Path.DirectorySeparatorChar, relativePathComponents);

                DateTime? lastWriteTime = null;
                FileAttributes? fileAttributes = null;
                try
                {
                    lastWriteTime = fatFileSystem.GetLastWriteTime(dirPath);
                    fileAttributes = fatFileSystem.GetAttributes(dirPath);
                }
                catch (Exception)
                {
                    // ignored
                }

                var dirEntry = new Entry
                {
                    Name = relativePath,
                    FormattedName = relativePath,
                    RawPath = dirPath,
                    FullPathComponents = fullPathComponents,
                    RelativePathComponents = relativePathComponents,
                    Attributes = Format(fileAttributes),
                    Date = lastWriteTime,
                    Size = 0,
                    Type = Models.FileSystems.EntryType.Dir
                };
            
                if (recursive || this.pathComponentMatcher.IsMatch(dirEntry.FullPathComponents))
                {
                    this.nextEntries.Push(dirEntry);
                }
            }
        }
        catch (Exception)
        {
            // ignored
        }

        try
        {
            foreach (var filePath in fatFileSystem.GetFiles(currentPath).OrderByDescending(x => x).ToList())
            {
                var fullPathComponents = GetPathComponents(filePath);
                var relativePathComponents = fullPathComponents.Skip(this.rootPathComponents.Length).ToArray();
                var relativePath = string.Join(Path.DirectorySeparatorChar, relativePathComponents);

                DateTime? lastWriteTime = null;
                long size = 0;
                FileAttributes? fileAttributes = null;
                try
                {
                    lastWriteTime = fatFileSystem.GetLastWriteTime(filePath);
                    size = fatFileSystem.GetFileLength(filePath);
                    fileAttributes = fatFileSystem.GetAttributes(filePath);
                }
                catch (Exception)
                {
                    // ignored
                }
                
                var fileEntry = new Entry
                {
                    Name = relativePath,
                    FormattedName = relativePath,
                    RawPath = filePath,
                    FullPathComponents = fullPathComponents,
                    RelativePathComponents = relativePathComponents,
                    Attributes = Format(fileAttributes),
                    Date = lastWriteTime,
                    Size = size,
                    Type = Models.FileSystems.EntryType.File
                };
                
                if (this.pathComponentMatcher.IsMatch(fileEntry.FullPathComponents))
                {
                    this.nextEntries.Push(fileEntry);
                }
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private string Format(FileAttributes? fileAttributes)
    {
        return String.Concat(
            fileAttributes != null && (fileAttributes & FileAttributes.Archive) != 0 ? "A" : "-",
            fileAttributes != null && (fileAttributes & FileAttributes.ReadOnly) != 0 ? "R" : "-",
            fileAttributes != null && (fileAttributes & FileAttributes.Hidden) != 0 ? "H" : "-",
            fileAttributes != null && (fileAttributes & FileAttributes.System) != 0 ? "S" : "-");
    }
    
    public Task<Stream> OpenEntry(Entry entry)
    {
        return entry.Size == 0
            ? Task.FromResult(new MemoryStream() as Stream)
            : Task.FromResult(fatFileSystem.OpenFile(entry.RawPath, FileMode.Open) as Stream);
    }

    public string[] GetPathComponents(string path)
    {
        return path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
    }

    public bool UsesPattern => this.pathComponentMatcher?.UsesPattern ?? false;

    public Task Flush()
    {
        return Task.CompletedTask;
    }
}