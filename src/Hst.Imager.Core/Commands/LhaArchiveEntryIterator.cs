namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Compression.Lha;
using Hst.Imager.Core.PathComponents;
using Hst.Imager.Core.UaeMetadatas;
using Entry = Models.FileSystems.Entry;

public class LhaArchiveEntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly IMediaPath mediaPath;
    private readonly string rootPath;
    private string[] rootPathComponents;
    private PathComponentMatcherV3 pathComponentMatcher;
    private readonly LhaArchive lhaArchive;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private bool disposed;
    private readonly IDictionary<string, LzHeader> lhaEntryIndex;

    public LhaArchiveEntryIterator(Stream stream, string rootPath, LhaArchive lhaArchive, bool recursive)
    {
        this.stream = stream;
        this.mediaPath = MediaPath.LhaArchivePath;
        this.rootPath = rootPath;
        this.lhaArchive = lhaArchive;
        this.recursive = recursive;
        this.nextEntries = new Stack<Entry>();
        this.currentEntry = null;
        this.isFirst = true;
        this.lhaEntryIndex = new Dictionary<string, LzHeader>(StringComparer.OrdinalIgnoreCase);

        var pathComponents = GetPathComponents(rootPath);
        this.pathComponentMatcher = new PathComponentMatcherV3(pathComponents, recursive);
        this.rootPathComponents = this.pathComponentMatcher.PathComponents;
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
            await EnqueueEntries();
        }

        if (this.nextEntries.Count <= 0)
        {
            return false;
        }

        currentEntry = this.nextEntries.Pop();

        return true;
    }

    public Task<Stream> OpenEntry(Entry entry)
    {
        if (!lhaEntryIndex.ContainsKey(entry.RawPath))
        {
            throw new IOException($"Entry '{entry.RawPath}' not found");
        }

        var output = new MemoryStream();
        this.lhaArchive.Extract(lhaEntryIndex[entry.RawPath], output);
        output.Position = 0;
        return Task.FromResult<Stream>(output);
    }

    private async Task EnqueueEntries()
    {
        var lhaEntries = (await lhaArchive.Entries()).ToList();

        var uniqueEntries = new Dictionary<string, Entry>();

        foreach (var lhaEntry in lhaEntries)
        {
            var entryPath = GetEntryName(lhaEntry.Name);

            lhaEntryIndex.Add(entryPath, lhaEntry);

            var isDir = entryPath.EndsWith("\\");

            var protectionBits = ProtectionBitsConverter.ToProtectionBits(lhaEntry.Attribute);
            var properties = new Dictionary<string, string>
            {
                { "ProtectionBits", ((int)protectionBits ^ 0xf).ToString() }
            };
            var comment = GetEntryComment(lhaEntry.Name);
            if (!string.IsNullOrEmpty(comment))
            {
                properties.Add("Comment", comment);
            }

            var dirAttributes = EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(0));

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, rootPathComponents,
            recursive, entryPath, entryPath, isDir, lhaEntry.UnixLastModifiedStamp, lhaEntry.OriginalSize,
            EntryFormatter.FormatProtectionBits(protectionBits), properties, dirAttributes).ToList();

            foreach (var entry in entries)
            {
                if (entry.Type == Models.FileSystems.EntryType.Dir && rootPath.Equals(entry.RawPath))
                {
                    continue;
                }
                
                uniqueEntries[entry.Name] = entry;
            }
        }

        foreach (var entry in uniqueEntries.Values.OrderByDescending(x => x.Name))
        {
            nextEntries.Push(entry);
        }
    }

    // get lha entry name by stripping away other chars after zero byte
    private string GetEntryName(string name)
    {
        int i;
        for (i = 0; i < name.Length; i++)
        {
            if (name[i] == 0)
            {
                break;
            }
        }

        return name.Substring(0, i);
    }

    // get lha entry name by stripping away other chars after zero byte
    private static string GetEntryComment(string name)
    {
        int i;
        for (i = 0; i < name.Length; i++)
        {
            if (name[i] == 0)
            {
                break;
            }
        }

        return i < name.Length - 1 ? name.Substring(i + 1) : string.Empty;
    }

    public string[] GetPathComponents(string path) => mediaPath.Split(path);

    public bool UsesPattern => this.pathComponentMatcher?.UsesPattern ?? false;

    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public UaeMetadata UaeMetadata { get; set; }
}