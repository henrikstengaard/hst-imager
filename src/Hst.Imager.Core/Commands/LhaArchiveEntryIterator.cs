using Hst.Core;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Compression.Lha;
using PathComponents;
using UaeMetadatas;
using Entry = Models.FileSystems.Entry;

public class LhaArchiveEntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly IMediaPath mediaPath;
    private readonly string rootPath;
    private readonly string[] rootPathComponents;
    private PathComponentMatcher pathComponentMatcher;
    private readonly LhaArchive lhaArchive;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private readonly IDictionary<string, LzHeader> lhaEntryIndex;
    private IList<LzHeader> lhaEntries = new List<LzHeader>();
    private bool initialized;
    private readonly HashSet<string> dirHasEntries = [];

    public PartitionTableType PartitionTableType => PartitionTableType.None;
    public int PartitionNumber => 0;

    public LhaArchiveEntryIterator(Stream stream, string rootPath, LhaArchive lhaArchive, bool recursive)
    {
        this.stream = stream;
        mediaPath = MediaPath.LhaArchivePath;
        this.rootPath = rootPath;
        this.lhaArchive = lhaArchive;
        this.recursive = recursive;
        nextEntries = new Stack<Entry>();
        currentEntry = null;
        isFirst = true;
        lhaEntryIndex = new Dictionary<string, LzHeader>(StringComparer.OrdinalIgnoreCase);
        rootPathComponents = GetPathComponents(rootPath);
    }

    public void Dispose()
    {
        lhaArchive.Dispose();
        stream.Dispose();
    }

    public async Task<Result> Initialize()
    {
        lhaEntries = (await lhaArchive.Entries()).ToList();

        if (rootPathComponents.Length == 0)
        {
            DirPathComponents = [];
            pathComponentMatcher = new PathComponentMatcher(rootPathComponents, recursive: recursive);
            initialized = true;
            return new Result();
        }
        
        var validDirComponents = Array.Empty<string>();
        var usePattern = false;
        var entriesExist = false;
        foreach (var lhaEntry in lhaEntries)
        {
            var entryPath = GetEntryName(lhaEntry.Name);

            var isDir = entryPath.EndsWith(mediaPath.PathSeparator);
            
            var entryPathComponents = mediaPath.Split(entryPath);

            var pathComponentMatch = PathComponentHelper.MatchPathComponents(rootPathComponents, entryPathComponents);

            // path components do not match, continue
            if (!pathComponentMatch.Success)
            {
                continue;
            }

            // update valid dir components, if entry is a directory
            if (isDir)
            {
                if (pathComponentMatch.MatchingPathComponents.Length > validDirComponents.Length)
                {
                    validDirComponents = pathComponentMatch.MatchingPathComponents;
                }
                continue;
            }
            
            // file entries exist
            entriesExist = true;

            // update valid dir components from file entry match
            if (pathComponentMatch.MatchingPathComponents.Length > validDirComponents.Length)
            {
                validDirComponents = entryPathComponents.Length == pathComponentMatch.MatchingPathComponents.Length 
                    ? pathComponentMatch.MatchingPathComponents.Take(pathComponentMatch.MatchingPathComponents.Length - 1).ToArray()
                    : pathComponentMatch.MatchingPathComponents;
            }
            
            // use pattern, if entry path components length equals root path components length
            if (entryPathComponents.Length == PathComponents.Length)
            {
                usePattern = true;
            }
        }
        
        if (!entriesExist)
        {
            return new Result(new PathNotFoundError($"Path not found '{rootPath}'", rootPath));
        } 

        DirPathComponents = validDirComponents.ToArray();
        pathComponentMatcher = new PathComponentMatcher(usePattern ? rootPathComponents.ToArray() : [], recursive: recursive);
        initialized = true;

        return new Result();
    }

    private void ThrowIfNotInitialized()
    {
        if (initialized)
        {
            return;
        }

        throw new InvalidOperationException("File system entry iterator not initialized");
    }

    /// <summary>
    /// Root path components of iterator.
    /// </summary>
    public string[] PathComponents => rootPathComponents;

    /// <summary>
    /// Dir path components from root path components that exist and is set during initialization.
    /// </summary>
    public string[] DirPathComponents { get; private set; } = [];

    public Media Media => null;
    public string RootPath => rootPath;

    public Entry Current => currentEntry;

    public bool HasMoreEntries => nextEntries.Count > 0;
    public bool IsSingleFileEntryNext => 1 == nextEntries.Count && 
                                         nextEntries.All(x => x.Type == Models.FileSystems.EntryType.File);

    public Task<bool> Next()
    {
        ThrowIfNotInitialized();
        
        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            EnqueueEntries();
        }

        if (nextEntries.Count <= 0)
        {
            return Task.FromResult(false);
        }

        bool skipEntry;
        do
        {
            currentEntry = nextEntries.Pop();
            
            if (currentEntry.Type == Models.FileSystems.EntryType.File)
            {
                return Task.FromResult(true);
            }

            if (recursive)
            {
                var dirPath = mediaPath.Join(currentEntry.FullPathComponents);
                skipEntry = pathComponentMatcher.UsesPattern && !dirHasEntries.Contains(dirPath);
            }
            else
            {
                skipEntry = currentEntry.FullPathComponents.Length < pathComponentMatcher.PathComponents.Length ||
                            !pathComponentMatcher.IsMatch(currentEntry.FullPathComponents);
            }
        } while (nextEntries.Count > 0 && skipEntry);

        return Task.FromResult(true);
    }

    public Task<Stream> OpenEntry(Entry entry)
    {
        if (!lhaEntryIndex.ContainsKey(entry.RawPath))
        {
            throw new IOException($"Entry '{entry.RawPath}' not found");
        }

        var output = new MemoryStream();
        lhaArchive.Extract(lhaEntryIndex[entry.RawPath], output);
        output.Position = 0;
        return Task.FromResult<Stream>(output);
    }

    private void EnqueueEntries()
    {
        var uniqueEntries = new Dictionary<string, Entry>();

        foreach (var lhaEntry in lhaEntries)
        {
            var entryPath = GetEntryName(lhaEntry.Name);

            lhaEntryIndex.Add(entryPath, lhaEntry);

            var isDir = entryPath.EndsWith('\\');

            var protectionBits = ProtectionBitsConverter.ToProtectionBits(lhaEntry.Attribute);
            var properties = new Dictionary<string, string>
            {
                { Core.Constants.EntryPropertyNames.ProtectionBits, ((int)protectionBits ^ 0xf).ToString() }
            };
            var comment = GetEntryComment(lhaEntry.Name);
            if (!string.IsNullOrEmpty(comment))
            {
                properties.Add(Core.Constants.EntryPropertyNames.Comment, comment);
            }

            var dirAttributes = EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(0));

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, DirPathComponents,
            recursive, entryPath, entryPath, isDir, lhaEntry.UnixLastModifiedStamp, lhaEntry.OriginalSize,
            EntryFormatter.FormatProtectionBits(protectionBits), properties, dirAttributes).ToList();

            foreach (var entry in entries)
            {
                if (entry.Type == Models.FileSystems.EntryType.Dir && rootPath.Equals(entry.RawPath))
                {
                    continue;
                }
                
                if (entry.Type == Models.FileSystems.EntryType.File)
                {
                    var dirPath =
                        mediaPath.Join(entry.FullPathComponents.Take(entry.FullPathComponents.Length - 1).ToArray());
                    dirHasEntries.Add(dirPath);
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

    public bool UsesPattern => pathComponentMatcher.UsesPattern;

    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public bool SupportsUaeMetadata => false;

    public UaeMetadata UaeMetadata { get; set; }
}