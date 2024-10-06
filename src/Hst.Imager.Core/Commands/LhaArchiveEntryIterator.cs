namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Compression.Lha;
using Hst.Imager.Core.UaeMetadatas;
using Entry = Models.FileSystems.Entry;

public class LhaArchiveEntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly string rootPath;
    private string[] rootPathComponents;
    private PathComponentMatcher pathComponentMatcher;
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
        this.rootPath = rootPath;
        this.rootPathComponents = GetPathComponents(rootPath);
        this.pathComponentMatcher = null;
        this.lhaArchive = lhaArchive;
        this.recursive = recursive;
        this.nextEntries = new Stack<Entry>();
        this.currentEntry = null;
        this.isFirst = true;
        this.lhaEntryIndex = new Dictionary<string, LzHeader>(StringComparer.OrdinalIgnoreCase);
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
        var dirs = new HashSet<string>();
        var currentPathComponents = new List<string>(this.rootPathComponents).ToArray();
        var lhaEntries = (await lhaArchive.Entries()).ToList();

        var entries = new List<Entry>();

        foreach (var lhaEntry in lhaEntries)
        {
            var entryPath = GetEntryName(lhaEntry.Name).Replace("\\", "/");

            var entryFullPathComponents = GetPathComponents(entryPath);
            var entryRelativePathComponents = this.rootPathComponents.SequenceEqual(entryFullPathComponents)
                ? new[] { entryFullPathComponents[^1] }
                : entryFullPathComponents.Skip(currentPathComponents.Length).ToArray();

            var isDir = (lhaEntry.UnixMode & Constants.UNIX_FILE_DIRECTORY) == Constants.UNIX_FILE_DIRECTORY;
            if (isDir)
            {
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
                        Date = lhaEntry.UnixLastModifiedStamp,
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

            lhaEntryIndex.Add(entryPath, lhaEntry);

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

            var entryRelativePath = string.Join("/", entryRelativePathComponents);

            var fileEntry = new Entry
            {
                Name = entryRelativePath,
                FormattedName = entryRelativePath,
                RawPath = entryPath,
                FullPathComponents = entryFullPathComponents,
                RelativePathComponents = entryRelativePathComponents,
                Date = lhaEntry.UnixLastModifiedStamp,
                Size = lhaEntry.OriginalSize,
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
    private string GetEntryComment(string name)
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

    public string[] GetPathComponents(string path)
    {
        return path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
    }

    public bool UsesPattern => this.pathComponentMatcher?.UsesPattern ?? false;

    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public UaeMetadata UaeMetadata { get; set; }
}