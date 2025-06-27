using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Compression.Lzx;
using Hst.Imager.Core.PathComponents;
using Hst.Imager.Core.UaeMetadatas;
using Entry = Models.FileSystems.Entry;

public class LzxArchiveEntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly IMediaPath mediaPath;
    private readonly string rootPath;
    private string[] rootPathComponents;
    private PathComponentMatcher pathComponentMatcher;
    private readonly LzxArchive lzxArchive;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private bool disposed;
    private readonly IDictionary<string, byte[]> lzxEntryIndex;

    public LzxArchiveEntryIterator(Stream stream, string rootPath, LzxArchive lzxArchive, bool recursive)
    {
        this.stream = stream;
        this.mediaPath = MediaPath.LzxArchivePath;
        this.rootPath = rootPath;
        this.lzxArchive = lzxArchive;
        this.recursive = recursive;
        this.nextEntries = new Stack<Entry>();
        this.currentEntry = null;
        this.isFirst = true;
        this.lzxEntryIndex = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        var pathComponents = GetPathComponents(rootPath);
        var hasPattern = pathComponents.Length > 0 &&
            pathComponents[^1].IndexOf("*", StringComparison.OrdinalIgnoreCase) >= 0;
        this.rootPathComponents =
            hasPattern ? pathComponents.Take(pathComponents.Length - 1).ToArray() : pathComponents;
        this.pathComponentMatcher = new PathComponentMatcher(pathComponents, recursive);
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

    public Media Media => null;
    public string RootPath => rootPath;

    public Entry Current => currentEntry;
    
    public bool HasMoreEntries => nextEntries.Count > 0;
    public bool IsSingleFileEntryNext => 1 == nextEntries.Count && 
                                         nextEntries.All(x => x.Type == Models.FileSystems.EntryType.File);

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
        if (!lzxEntryIndex.ContainsKey(entry.RawPath))
        {
            throw new IOException($"Entry '{entry.RawPath}' not found");
        }

        return Task.FromResult<Stream>(new MemoryStream(lzxEntryIndex[entry.RawPath]));
    }
    
    private async Task EnqueueEntries()
    {
        var uniqueEntries = new Dictionary<string, Entry>();

        while (await lzxArchive.Next() is { } lzxEntry)
        {
            var entryPath = lzxEntry.Name;

            // extract lzx entry
            using (var memoryStream = new MemoryStream())
            {
                await lzxArchive.Extract(memoryStream);
                lzxEntryIndex.Add(entryPath, memoryStream.ToArray());
            }
            
            var isDir = entryPath.EndsWith("//");

            var protectionBits = GetProtectionBits(lzxEntry.Attributes);
            var properties = new Dictionary<string, string>
            {
                { Core.Constants.EntryPropertyNames.ProtectionBits, ((int)protectionBits ^ 0xf).ToString() }
            };

            if (!string.IsNullOrEmpty(lzxEntry.Comment))
            {
                properties.Add(Core.Constants.EntryPropertyNames.Comment, lzxEntry.Comment);
            }

            var dirAttributes = EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(0));

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, rootPathComponents,
                recursive, entryPath, lzxEntry.Name, isDir, lzxEntry.Date, lzxEntry.UnpackedSize,
                EntryFormatter.FormatProtectionBits(protectionBits), properties, dirAttributes).ToList();

            foreach (var entry in entries)
            {
                uniqueEntries[entry.Name] = entry;
            }
        }

        foreach (var entry in uniqueEntries.Values.OrderByDescending(x => x.Name))
        {
            nextEntries.Push(entry);
        }
    }

    private static Entry CreateFileEntry(IMediaPath mediaPath, LzxEntry lzxEntry,
        string[] fullPathComponents, string[] relativePathComponents)
    {
        var protectionBits = GetProtectionBits(lzxEntry.Attributes);
        var properties = new Dictionary<string, string>
        {
            { Core.Constants.EntryPropertyNames.ProtectionBits, ((int)protectionBits ^ 0xf).ToString() }
        };

        if (!string.IsNullOrEmpty(lzxEntry.Comment))
        {
            properties.Add(Core.Constants.EntryPropertyNames.Comment, lzxEntry.Comment);
        }

        var entryRelativePath = mediaPath.Join(relativePathComponents.ToArray());

        return new Entry
        {
            Name = entryRelativePath,
            FormattedName = entryRelativePath,
            RawPath = lzxEntry.Name,
            FullPathComponents = fullPathComponents.ToArray(),
            RelativePathComponents = relativePathComponents.ToArray(),
            Date = lzxEntry.Date,
            Size = lzxEntry.UnpackedSize,
            Type = Models.FileSystems.EntryType.File,
            Attributes = EntryFormatter.FormatProtectionBits(protectionBits),
            Properties = properties
        };
    }

    private static ProtectionBits GetProtectionBits(AttributesEnum attributes)
    {
        var protectionBits = ProtectionBits.None;
        
        if (attributes.HasFlag(AttributesEnum.HeldResident))
        {
            protectionBits |= ProtectionBits.HeldResident;
        }
        
        if (attributes.HasFlag(AttributesEnum.Script))
        {
            protectionBits |= ProtectionBits.Script;
        }
        
        if (attributes.HasFlag(AttributesEnum.Pure))
        {
            protectionBits |= ProtectionBits.Pure;
        }
        
        if (attributes.HasFlag(AttributesEnum.Archive))
        {
            protectionBits |= ProtectionBits.Archive;
        }
        
        if (attributes.HasFlag(AttributesEnum.Read))
        {
            protectionBits |= ProtectionBits.Read;
        }
        
        if (attributes.HasFlag(AttributesEnum.Write))
        {
            protectionBits |= ProtectionBits.Write;
        }
        
        if (attributes.HasFlag(AttributesEnum.Executable))
        {
            protectionBits |= ProtectionBits.Executable;
        }
        
        if (attributes.HasFlag(AttributesEnum.Delete))
        {
            protectionBits |= ProtectionBits.Delete;
        }
        
        return protectionBits;
    }
    
    public string[] GetPathComponents(string path) => mediaPath.Split(path);

    public bool UsesPattern => this.pathComponentMatcher?.UsesPattern ?? false;

    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public UaeMetadata UaeMetadata { get; set; }
}