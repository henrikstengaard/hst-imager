using System.Linq;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UaeMetadatas;
using ICSharpCode.SharpZipLib.Lzw;
using Models.FileSystems;

public class LzwArchiveEntryIterator : IEntryIterator
{
    private readonly Stack<Entry> nextEntries;
    private readonly string filePath;
    private Entry currentEntry;
    private bool isFirst;

    public LzwArchiveEntryIterator(string path)
    {
        this.nextEntries = new Stack<Entry>();
        this.filePath = Path.GetFullPath(path);
        this.RootPath = Path.GetDirectoryName(this.filePath);
        this.isFirst = true;
    }

    public void Dispose()
    {
    }

    public Media Media => null;
    public string RootPath { get; }

    public Entry Current => currentEntry;

    public bool HasMoreEntries => nextEntries.Count > 0;
    public bool IsSingleFileEntryNext => 1 == nextEntries.Count && 
                                         nextEntries.All(x => x.Type == Models.FileSystems.EntryType.File);

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
        return Task.FromResult<Stream>(new LzwInputStream(File.OpenRead(this.filePath)));
    }

    private static readonly Regex LzwExtensionRegex =
        new Regex("\\.Z$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private void EnqueueFile()
    {
        var fileInfo = new FileInfo(this.filePath);
        
        var entryName = LzwExtensionRegex.Replace(fileInfo.Name, "");
        
        this.nextEntries.Push(new Entry
        {
            Name = entryName,
            FormattedName = entryName,
            RawPath = fileInfo.FullName,
            RelativePathComponents = new[]{entryName},
            Date = fileInfo.LastWriteTime,
            Size = fileInfo.Length,
            Type = EntryType.File
        });        
    }
    
    public string[] GetPathComponents(string path)
    {
        return path.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);
    }

    public bool UsesPattern => false;
    
    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public UaeMetadata UaeMetadata { get; set; }
}