namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Models.FileSystems;

public class FileEntryIterator : IEntryIterator
{
    private readonly Stack<Entry> nextEntries;
    private readonly string filePath;
    private Entry currentEntry;
    private bool isFirst;

    public FileEntryIterator(string path)
    {
        this.nextEntries = new Stack<Entry>();
        this.filePath = Path.GetFullPath(path);
        this.RootPath = Path.GetDirectoryName(this.filePath);
        this.isFirst = true;
    }

    public string RootPath { get; }

    public Entry Current => currentEntry;    

    public Task<bool> Next()
    {
        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            EnqueueFile();
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
        return Task.FromResult<Stream>(File.OpenRead(entry.RawPath));
    }
    
    public void Dispose()
    {
    }

    private void EnqueueFile()
    {
        var fileInfo = new FileInfo(this.filePath);
        
        this.nextEntries.Push(new Entry
        {
            Name = fileInfo.Name,
            RawPath = fileInfo.FullName,
            PathComponents = fileInfo.FullName.Split('\\', '/', StringSplitOptions.RemoveEmptyEntries),
            Date = fileInfo.LastWriteTime,
            Size = fileInfo.Length,
            Type = EntryType.File
        });        
    }
    
    public string[] GetPathComponents(string path)
    {
        return path.Split('\\', '/', StringSplitOptions.RemoveEmptyEntries);
    }
}