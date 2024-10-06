namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Compression.Lzx;
using Hst.Imager.Core.UaeMetadatas;
using Entry = Models.FileSystems.Entry;

public class LzxArchiveEntryIterator : IEntryIterator
{
    private readonly Stream stream;
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
        this.rootPath = rootPath;
        this.rootPathComponents = GetPathComponents(rootPath);
        this.pathComponentMatcher = null;
        this.lzxArchive = lzxArchive;
        this.recursive = recursive;
        this.nextEntries = new Stack<Entry>();
        this.currentEntry = null;
        this.isFirst = true;
        this.lzxEntryIndex = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
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
            ResolvePathComponentMatcher();
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
        var dirs = new HashSet<string>();
        var currentPathComponents = new List<string>(this.rootPathComponents).ToArray();

        var entries = new List<Entry>();

        while (await lzxArchive.Next() is { } lzxEntry)
        {
            var entryPath = lzxEntry.Name;

            // extract lzx entry
            using (var memoryStream = new MemoryStream())
            {
                await lzxArchive.Extract(memoryStream);
                lzxEntryIndex.Add(entryPath, memoryStream.ToArray());
            }
            
            var entryFullPathComponents = GetPathComponents(entryPath);
            var entryRelativePathComponents = this.rootPathComponents.SequenceEqual(entryFullPathComponents)
                ? new[] { entryFullPathComponents[^1] }
                : entryFullPathComponents.Skip(currentPathComponents.Length).ToArray();

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
                        Date = lzxEntry.Date,
                        Size = 0,
                        Type = Models.FileSystems.EntryType.Dir,
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

            var protectionBits = GetProtectionBits(lzxEntry.Attributes);
            var properties = new Dictionary<string, string>
            {
                { "ProtectionBits", ((int)protectionBits ^ 0xf).ToString() }
            };
            if (!string.IsNullOrEmpty(lzxEntry.Comment))
            {
                properties.Add("Comment", lzxEntry.Comment);
            }

            var entryRelativePath = string.Join("/", entryRelativePathComponents);

            var fileEntry = new Entry
            {
                Name = entryRelativePath,
                FormattedName = entryRelativePath,
                RawPath = entryPath,
                FullPathComponents = entryFullPathComponents,
                RelativePathComponents = entryRelativePathComponents,
                Date = lzxEntry.Date,
                Size = lzxEntry.UnpackedSize,
                Type = Models.FileSystems.EntryType.File,
                Attributes =
                    EntryFormatter.FormatProtectionBits(protectionBits),
                Properties = properties
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
    
    private ProtectionBits GetProtectionBits(AttributesEnum attributes)
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

    public string[] GetPathComponents(string path)
    {
        return path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
    }

    public bool UsesPattern => this.pathComponentMatcher?.UsesPattern ?? false;

    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public UaeMetadata UaeMetadata { get; set; }
}