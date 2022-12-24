namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Models.FileSystems;

public class DirectoryEntryIterator : IEntryIterator
{
    private readonly Stack<Entry> nextEntries;
    private readonly string rootPath;
    private readonly bool recursive;
    private Entry currentEntry;
    private bool isFirst;

    public DirectoryEntryIterator(string path, bool recursive)
    {
        this.nextEntries = new Stack<Entry>();
        this.rootPath = Path.GetFullPath(path);
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

        currentEntry = this.nextEntries.Pop();
        if (this.recursive && currentEntry.Type == EntryType.Dir)
        {
            EnqueueDirectory(currentEntry.Path);
        }

        return Task.FromResult(true);
    }

    public Task<Stream> OpenEntry(Entry entry)
    {
        return Task.FromResult<Stream>(File.OpenRead(Path.Combine(this.rootPath, entry.Path)));
    }

    private string GetEntryPath(string entryPath)
    {
        return entryPath.Length >= this.rootPath.Length + 1
            ? entryPath.Substring(this.rootPath.Length + 1)
            : string.Empty;
    }

    private void EnqueueDirectory(string currentPath)
    {
        var currentDir = new DirectoryInfo(currentPath); 
        
        foreach (var dirInfo in currentDir.GetDirectories().OrderByDescending(x => x.Name).ToList())
        {
            this.nextEntries.Push(new Entry
            {
                Name = dirInfo.Name,
                Path = dirInfo.FullName,
                Date = dirInfo.LastWriteTime,
                Size = 0,
                Type = EntryType.Dir
            });
        }
        
        foreach (var fileInfo in currentDir.GetFiles().OrderByDescending(x => x.Name).ToList())
        {
            this.nextEntries.Push(new Entry
            {
                Name = fileInfo.Name,
                Path = fileInfo.FullName,
                Date = fileInfo.LastWriteTime,
                Size = fileInfo.Length,
                Type = EntryType.File
            });
        }
    }

    public void Dispose()
    {
    }
}