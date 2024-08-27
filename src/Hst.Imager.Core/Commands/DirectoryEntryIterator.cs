namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Models.FileSystems;

public class DirectoryEntryIterator : IEntryIterator
{
    private readonly Stack<Entry> nextEntries;
    private readonly string rootPath;
    private readonly string[] rootPathComponents;
    private readonly PathComponentMatcher pathComponentMatcher;
    private readonly bool recursive;
    private Entry currentEntry;
    private bool isFirst;

    public DirectoryEntryIterator(string path, string pattern, bool recursive)
    {
        this.nextEntries = new Stack<Entry>();
        this.rootPath = Path.GetFullPath(path);
        this.rootPathComponents = GetPathComponents(this.rootPath);
        this.pathComponentMatcher = new PathComponentMatcher(rootPathComponents, pattern, recursive: recursive);
        this.recursive = recursive;
        this.isFirst = true;
    }

    public string RootPath => rootPath;
    
    public Entry Current => currentEntry;

    public Task<bool> Next()
    {
        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            EnqueueDirectory(rootPath);
        }
        
        if (this.nextEntries.Count <= 0)
        {
            return Task.FromResult(false);
        }
        
        bool skipEntry;
        do
        {
            currentEntry = this.nextEntries.Pop();

            if (currentEntry.Type == EntryType.File)
            {
                return Task.FromResult(true);
            }

            skipEntry = SkipEntry(currentEntry.FullPathComponents);

            if (recursive)
            {
                EnqueueDirectory(currentEntry.RawPath);
            }
        } while (nextEntries.Count > 0 && skipEntry);

        return Task.FromResult(true);
    }

    public Task<Stream> OpenEntry(Entry entry)
    {
        return Task.FromResult<Stream>(File.OpenRead(entry.RawPath));
    }

    public string[] GetPathComponents(string path)
    {
        return path.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);
    }

    public bool UsesPattern => this.pathComponentMatcher?.UsesPattern ?? false;
    
    public Task Flush()
    {
        return Task.CompletedTask;
    }

    private void EnqueueDirectory(string currentPath)
    {
        var currentDir = new DirectoryInfo(currentPath); 
        
        foreach (var dirInfo in currentDir.GetDirectories().OrderByDescending(x => x.Name).ToList())
        {
            var fullPathComponents = GetPathComponents(dirInfo.FullName);
            var relativePathComponents = fullPathComponents.Skip(this.rootPathComponents.Length).ToArray();
            var relativePath = string.Join(Path.DirectorySeparatorChar, relativePathComponents);

            this.nextEntries.Push(new Entry
            {
                Name = relativePath,
                FormattedName = relativePath,
                RawPath = dirInfo.FullName,
                FullPathComponents = fullPathComponents,
                RelativePathComponents = relativePathComponents,
                Date = dirInfo.LastWriteTime,
                Size = 0,
                Type = EntryType.Dir,
                Properties = new Dictionary<string, string>()
            });
        }
        
        foreach (var fileInfo in currentDir.GetFiles().OrderByDescending(x => x.Name).ToList())
        {
            var fullPathComponents = GetPathComponents(fileInfo.FullName);
            var relativePathComponents = fullPathComponents.Skip(this.rootPathComponents.Length).ToArray();
            var relativePath = string.Join(Path.DirectorySeparatorChar, relativePathComponents);

            if (SkipEntry(fullPathComponents))
            {
                continue;
            }

            this.nextEntries.Push(new Entry
            {
                Name = relativePath,
                FormattedName = relativePath,
                RawPath = fileInfo.FullName,
                FullPathComponents = fullPathComponents,
                RelativePathComponents = relativePathComponents,
                Date = fileInfo.LastWriteTime,
                Size = fileInfo.Length,
                Type = EntryType.File,
                Properties = new Dictionary<string, string>()
            });
        }
    }

    public void Dispose()
    {
    }

    private bool SkipEntry(string[] pathComponents)
    {
        if (!UsesPattern)
        {
            return false;
        }

        return !pathComponentMatcher.IsMatch(pathComponents);
    }
}