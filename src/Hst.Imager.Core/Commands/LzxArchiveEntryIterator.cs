using Hst.Core;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Compression.Lzx;
using PathComponents;
using UaeMetadatas;
using Entry = Models.FileSystems.Entry;

public class LzxArchiveEntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly IMediaPath mediaPath;
    private readonly string rootPath;
    private readonly string[] rootPathComponents;
    private PathComponentMatcher pathComponentMatcher;
    private readonly LzxArchive lzxArchive;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private bool disposed;
    private readonly IDictionary<string, byte[]> lzxEntryIndex;
    private readonly IList<LzxEntry> lzxEntries = new List<LzxEntry>();
    private bool initialized;
    private readonly HashSet<string> dirHasEntries = [];

    public PartitionTableType PartitionTableType => PartitionTableType.None;
    public int PartitionNumber => 0;

    public LzxArchiveEntryIterator(Stream stream, string rootPath, LzxArchive lzxArchive, bool recursive)
    {
        this.stream = stream;
        mediaPath = MediaPath.LzxArchivePath;
        this.rootPath = rootPath;
        this.lzxArchive = lzxArchive;
        this.recursive = recursive;
        nextEntries = new Stack<Entry>();
        currentEntry = null;
        isFirst = true;
        lzxEntryIndex = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        rootPathComponents = GetPathComponents(rootPath);
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

    public async Task<Result> Initialize()
    {
        var hasWildcard = rootPathComponents.Length > 0 && PathComponentHelper.HasWildcard(rootPathComponents[^1]);
        var validDirComponents = Array.Empty<string>();
        var entriesExist = false;

        while (await lzxArchive.Next() is { } lzxEntry)
        {
            lzxEntries.Add(lzxEntry);
            
            var entryPath = lzxEntry.Name;
            
            // extract lzx entry
            using (var memoryStream = new MemoryStream())
            {
                await lzxArchive.Extract(memoryStream);
                lzxEntryIndex.Add(entryPath, memoryStream.ToArray());
            }
            
            var entryPathComponents = mediaPath.Split(entryPath);

            var pathComponentMatch = PathComponentHelper.MatchPathComponents(rootPathComponents, entryPathComponents);

            if (!pathComponentMatch.Success)
            {
                continue;
            }
            
            entriesExist = true;
                
            if (pathComponentMatch.MatchingPathComponents.Length > validDirComponents.Length)
            {
                validDirComponents = pathComponentMatch.MatchingPathComponents;
            }
        }
        
        if (!entriesExist)
        {
            return new Result(new PathNotFoundError($"Path not found '{rootPath}'", rootPath));
        }        

        DirPathComponents = rootPathComponents.Length > 0 ? validDirComponents.ToArray() : [];
        pathComponentMatcher = new PathComponentMatcher(hasWildcard && rootPathComponents.Length > 0
            ? rootPathComponents.ToArray() : [], recursive: recursive);
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
                skipEntry = !EntryIteratorFunctions.IsRelativePathComponentsValid(pathComponentMatcher,
                    currentEntry.RelativePathComponents, recursive);
            }
        } while (nextEntries.Count > 0 && skipEntry);

        return Task.FromResult(true);
    }
    
    public Task<Stream> OpenEntry(Entry entry)
    {
        return !lzxEntryIndex.TryGetValue(entry.RawPath, out var zipEntry)
            ? throw new IOException($"Entry '{entry.RawPath}' not found")
            : Task.FromResult<Stream>(new MemoryStream(zipEntry));
    }
    
    private void EnqueueEntries()
    {
        var uniqueEntries = new Dictionary<string, Entry>();

        foreach (var lzxEntry in lzxEntries)
        {
            var entryPath = lzxEntry.Name;
            
            var isDir = entryPath.EndsWith("//");

            var protectionBits = GetProtectionBits(lzxEntry.Attributes);
            var properties = new Dictionary<string, string>
            {
                { Constants.EntryPropertyNames.ProtectionBits, ((int)protectionBits ^ 0xf).ToString() }
            };

            if (!string.IsNullOrEmpty(lzxEntry.Comment))
            {
                properties.Add(Constants.EntryPropertyNames.Comment, lzxEntry.Comment);
            }

            var dirAttributes = EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(0));

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, DirPathComponents,
                recursive, entryPath, lzxEntry.Name, isDir, lzxEntry.Date, lzxEntry.UnpackedSize,
                EntryFormatter.FormatProtectionBits(protectionBits), properties, dirAttributes).ToList();

            foreach (var entry in entries)
            {
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

    public bool UsesPattern => pathComponentMatcher.UsesPattern;

    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public bool SupportsUaeMetadata => true;

    public UaeMetadata UaeMetadata { get; set; }
}