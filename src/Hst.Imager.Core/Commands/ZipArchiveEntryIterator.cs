using Hst.Core;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Compressions.Zip;
using Helpers;
using PathComponents;
using UaeMetadatas;
using Entry = Models.FileSystems.Entry;

public class ZipArchiveEntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly IMediaPath mediaPath;
    private readonly string rootPath;
    private readonly string[] rootPathComponents;
    private PathComponentMatcher pathComponentMatcher;
    private readonly ZipArchive zipArchive;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private readonly IDictionary<string, ZipArchiveEntry> zipEntryIndex;
    private IList<ZipArchiveEntry> zipEntries = new List<ZipArchiveEntry>();
    private bool initialized;
    private readonly HashSet<string> dirHasEntries = [];

    public PartitionTableType PartitionTableType => PartitionTableType.None;
    public int PartitionNumber => 0;

    public ZipArchiveEntryIterator(Stream stream, string rootPath, ZipArchive zipArchive, bool recursive)
    {
        this.stream = stream;
        mediaPath = MediaPath.ZipArchivePath;
        this.rootPath = rootPath;
        this.zipArchive = zipArchive;
        this.recursive = recursive;
        nextEntries = new Stack<Entry>();
        currentEntry = null;
        isFirst = true;
        zipEntryIndex = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        rootPathComponents = GetPathComponents(rootPath);
    }

    public void Dispose()
    {
        zipArchive.Dispose();
        stream.Dispose();
    }

    private void ThrowIfNotInitialized()
    {
        if (initialized)
        {
            return;
        }

        throw new InvalidOperationException("File system entry iterator not initialized");
    }
    
    public Task<Result> Initialize()
    {
        zipEntries = zipArchive.Entries.OrderBy(x => x.FullName).ToList();

        if (rootPathComponents.Length == 0)
        {
            DirPathComponents = [];
            pathComponentMatcher = new PathComponentMatcher(rootPathComponents, recursive: recursive);
            initialized = true;
            return Task.FromResult(new Result());
        }
        
        var validDirComponents = Array.Empty<string>();
        var usePattern = false;
        var entriesExist = false;
        foreach (var zipEntry in zipEntries)
        {
            var entryPath = GetEntryName(zipEntry.FullName);
            
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
            return Task.FromResult(new Result(new PathNotFoundError($"Path not found '{rootPath}'", rootPath)));
        }        

        DirPathComponents = validDirComponents.ToArray();
        pathComponentMatcher = new PathComponentMatcher(usePattern ? rootPathComponents.ToArray() : [], recursive: recursive);
        initialized = true;

        return Task.FromResult(new Result());
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

    public async Task<bool> Next()
    {
        ThrowIfNotInitialized();
        
        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            await EnqueueEntries();
        }

        if (nextEntries.Count <= 0)
        {
            return false;
        }

        bool skipEntry;
        do
        {
            currentEntry = nextEntries.Pop();
            
            if (currentEntry.Type == Models.FileSystems.EntryType.File)
            {
                return true;
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

        return true;
    }

    public Task<Stream> OpenEntry(Entry entry)
    {
        return !zipEntryIndex.TryGetValue(entry.RawPath, out var zipEntry)
            ? throw new IOException($"Entry '{entry.RawPath}' not found")
            : Task.FromResult(zipEntry.Open());
    }

    public string[] GetPathComponents(string path) => mediaPath.Split(path);

    private async IAsyncEnumerable<CentralDirectoryFileHeader> ReadCentralDirectoryFileHeaders()
    {
        stream.Seek(0, SeekOrigin.Begin);
        var zipArchiveReader = new ZipArchiveReader(stream);

        while (await zipArchiveReader.Read() is { } zipHeader)
        {
            if (zipHeader is CentralDirectoryFileHeader centralDirectoryFileHeader)
            {
                yield return centralDirectoryFileHeader;
            }
        }
    }

    private async Task EnqueueEntries()
    {
        var centralDirectoryFileHeaderIndex = new Dictionary<string, CentralDirectoryFileHeader>();
        await foreach (var centralDirectoryFileHeader in ReadCentralDirectoryFileHeaders())
        {
            centralDirectoryFileHeaderIndex[centralDirectoryFileHeader.FileName] = centralDirectoryFileHeader;
        }

        var uniqueEntries = new Dictionary<string, Entry>();

        for (var i = zipEntries.Count - 1; i >= 0; i--)
        {
            var zipEntry = zipEntries[i];

            var entryPath = GetEntryName(zipEntry.FullName);

            zipEntryIndex.Add(entryPath, zipEntry);

            var isDir = zipEntry.FullName.EndsWith('/');

            if (isDir && UsesPattern)
            {
                continue;
            }

            var centralDirectoryFileHeader = centralDirectoryFileHeaderIndex.GetValueOrDefault(zipEntry.FullName);

            var attributes = GetAttributes(centralDirectoryFileHeader);
            var properties = GetProperties(centralDirectoryFileHeader);

            var dirAttributes = EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(0));

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, DirPathComponents,
                recursive, entryPath, entryPath, isDir, zipEntry.LastWriteTime.LocalDateTime, zipEntry.Length,
                attributes, properties, dirAttributes).ToList();

            foreach (var entry in entries)
            {
                switch (entry.Type)
                {
                    case Models.FileSystems.EntryType.Dir when rootPath.Equals(entry.RawPath):
                        continue;
                    case Models.FileSystems.EntryType.File:
                    {
                        var dirPath =
                            mediaPath.Join(entry.FullPathComponents.Take(entry.FullPathComponents.Length - 1).ToArray());
                        dirHasEntries.Add(dirPath);
                        break;
                    }
                }

                uniqueEntries[entry.Name] = entry;
            }
        }

        foreach (var entry in uniqueEntries.Values.OrderByDescending(x => x.Name))
        {
            nextEntries.Push(entry);
        }
    }

    private static string GetAttributes(CentralDirectoryFileHeader centralDirectoryFileHeader)
    {
        if (centralDirectoryFileHeader == null)
        {
            return string.Empty;
        }

        var hostOs = (HostOsFlags)centralDirectoryFileHeader.HostOs;
        switch(hostOs)
        {
            case HostOsFlags.MsDos:
                return FileAttributesFormatter.FormatMsDosAttributes((int)centralDirectoryFileHeader.ExternalFileAttributes);
            case HostOsFlags.Amiga:
                var protectionBitsValue = (int)((centralDirectoryFileHeader.ExternalFileAttributes >> 16) & 0xff);
                return EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(protectionBitsValue ^ 0xf));
            default:
                return string.Empty;
        }
    }

    private static Dictionary<string, string> GetProperties(CentralDirectoryFileHeader centralDirectoryFileHeader)
    {
        var properties = new Dictionary<string, string>();

        if (centralDirectoryFileHeader == null)
        {
            return properties;
        }

        if (!string.IsNullOrEmpty(centralDirectoryFileHeader.FileComment))
        {
            properties[Constants.EntryPropertyNames.Comment] = centralDirectoryFileHeader.FileComment;
        }

        var hostOs = (HostOsFlags)centralDirectoryFileHeader.HostOs;
        if (hostOs == HostOsFlags.Amiga)
        {
            var protectionBitsValue = (int)((centralDirectoryFileHeader.ExternalFileAttributes >> 16) & 0xff);
            properties[Constants.EntryPropertyNames.ProtectionBits] = (protectionBitsValue ^ 0xf).ToString();
        }

        return properties;
    }

    private static string GetEntryName(string name)
    {
        return name.EndsWith('/') ? name[..^1] : name;
    }
    
    public bool UsesPattern => pathComponentMatcher.UsesPattern;
    
    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public bool SupportsUaeMetadata => true;

    public UaeMetadata UaeMetadata { get; set; }
}