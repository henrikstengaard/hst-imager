namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Compression.Lha;
using Entry = Models.FileSystems.Entry;

public class LhaArchiveEntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly string rootPath;
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
        this.lhaArchive = lhaArchive;
        this.recursive = recursive;

        this.nextEntries = new Stack<Entry>();
        this.currentEntry = null;
        this.isFirst = true;
        this.lhaEntryIndex = new Dictionary<string, LzHeader>(StringComparer.OrdinalIgnoreCase);
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
            await EnqueueEntries(rootPath);
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

    private async Task EnqueueEntries(string currentPath)
    {
        var dirs = new HashSet<string>();

        var currentPathComponents = GetPathComponents(currentPath);
        var lhaEntries = (await lhaArchive.Entries()).ToList();

        var entries = new List<Entry>();
        
        foreach (var lhaEntry in lhaEntries)
        {
            var entryPath = GetEntryName(lhaEntry.Name).Replace("\\", "/");

            if (!string.IsNullOrEmpty(currentPath))
            {
                if (entryPath
                        .IndexOf(string.Concat(currentPath, "/").Replace("\\", "/"),
                            StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
            }

            var entryPathComponents = GetPathComponents(entryPath);
            var entryRelativePathComponents = entryPathComponents.Skip(currentPathComponents.Length).ToArray();

            var isDir = (lhaEntry.UnixMode & Constants.UNIX_FILE_DIRECTORY) == Constants.UNIX_FILE_DIRECTORY;
            if (isDir)
            {
                continue;
            }

            if (entryPathComponents.Length > currentPathComponents.Length + 1)
            {
                for (var componentIndex = currentPathComponents.Length;
                     componentIndex < (recursive ? entryPathComponents.Length : Math.Min(currentPathComponents.Length + 2, entryPathComponents.Length));
                     componentIndex++)
                {
                    var dirRelativePathComponents = entryPathComponents.Skip(currentPathComponents.Length)
                        .Take(componentIndex - currentPathComponents.Length).ToArray();
                    var dirRelativePath = string.Join("/", dirRelativePathComponents);
                    if (string.IsNullOrEmpty(dirRelativePath) || dirs.Contains(dirRelativePath))
                    {
                        continue;
                    }

                    dirs.Add(dirRelativePath);
                    entries.Add(new Entry
                    {
                        Name = dirRelativePath,
                        FormattedName = dirRelativePath,
                        RawPath = dirRelativePath,
                        FullPathComponents = entryPathComponents,
                        RelativePathComponents = dirRelativePathComponents,
                        Date = lhaEntry.UnixLastModifiedStamp,
                        Size = 0,
                        Type = Models.FileSystems.EntryType.Dir,
                        Attributes =
                            EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(0)),
                        Properties = new Dictionary<string, string>()
                    });
                }

                if (!recursive)
                {
                    continue;
                }
            }

            lhaEntryIndex.Add(entryPath, lhaEntry);

            var properties = new Dictionary<string, string>();
            var comment = GetEntryComment(lhaEntry.Name);
            if (!string.IsNullOrEmpty(comment))
            {
                properties.Add("Comment", comment);
            }
            
            var entryRelativePath = string.Join("/", entryRelativePathComponents);
            
            entries.Add(new Entry
            {
                Name = entryRelativePath,
                FormattedName = entryRelativePath,
                RawPath = entryPath,
                FullPathComponents = entryPathComponents,
                RelativePathComponents = entryRelativePathComponents,
                Date = lhaEntry.UnixLastModifiedStamp,
                Size = lhaEntry.OriginalSize,
                Type = Models.FileSystems.EntryType.File,
                Attributes =
                    EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(lhaEntry.Attribute)),
                Properties = properties
            });
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

    public bool UsesFileNameMatcher => false;
}