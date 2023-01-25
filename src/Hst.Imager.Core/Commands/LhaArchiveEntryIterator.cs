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
        var lhaEntries = (await lhaArchive.Entries()).ToList();

        for (var i = lhaEntries.Count - 1; i >= 0; i--)
        {
            var lhaEntry = lhaEntries[i];

            var entryName = GetEntryName(lhaEntry.Name);
            var entryPath = entryName;

            if (!string.IsNullOrEmpty(currentPath))
            {
                if (lhaEntry.Name.Replace("\\", "/")
                        .IndexOf(currentPath.Replace("\\", "/"), StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                entryName = entryName.Substring(currentPath.Length + 1);
            }

            if (!recursive && (entryName.IndexOf("/", StringComparison.Ordinal) >= 0 ||
                entryName.IndexOf("\\", StringComparison.Ordinal) >= 0))
            {
                continue;
            }

            lhaEntryIndex.Add(entryPath, lhaEntry);

            nextEntries.Push(new Entry
            {
                Name = entryName,
                FormattedName = entryName,
                RawPath = entryPath,
                PathComponents = entryPath.Split('\\', '/', StringSplitOptions.RemoveEmptyEntries),
                Date = lhaEntry.UnixLastModifiedStamp,
                Size = lhaEntry.OriginalSize,
                Type = (lhaEntry.UnixMode & Constants.UNIX_FILE_DIRECTORY) == Constants.UNIX_FILE_DIRECTORY
                    ? Models.FileSystems.EntryType.Dir
                    : Models.FileSystems.EntryType.File,
                Attributes =
                    EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(lhaEntry.Attribute)),
                Properties = new Dictionary<string, string>
                {
                    { "Comment", GetEntryComment(lhaEntry.Name) }
                }
            });
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
        return path.Split('\\', '/', StringSplitOptions.RemoveEmptyEntries);
    }
}