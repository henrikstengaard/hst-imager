using DiscUtils.Ntfs;
using Hst.Core;
using Hst.Imager.Core.Helpers;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Ext;
using DiscUtils.Fat;
using PathComponents;
using UaeMetadatas;
using Models;
using Entry = Models.FileSystems.Entry;

/// <summary>
/// File system entry iterator.
/// </summary>
public class FileSystemEntryIterator : IEntryIterator
{
    private readonly IMediaPath mediaPath = Core.PathComponents.MediaPath.GenericMediaPath;
    private PathComponentMatcher pathComponentMatcher;
    private readonly Stack<Entry> nextEntries = new();
    private bool isFirst = true;
    private Entry currentEntry;
    private bool initialized;
    private bool disposed;
    private readonly HashSet<string> dirPathsIteratedIndex = [];
    private readonly Media media;
    private readonly PartitionTableType partitionTableType;
    private readonly int partitionNumber;
    private readonly IFileSystem fileSystem;
    private readonly string[] rootPathComponents;
    private readonly bool recursive;

    /// <summary>
    /// File system entry iterator.
    /// </summary>
    /// <param name="media">Media used by iterator.</param>
    /// <param name="partitionTableType">Partition table type used by iterator.</param>
    /// <param name="partitionNumber">Partition number used by iterator.</param>
    /// <param name="fileSystem">File system to iterate through.</param>
    /// <param name="rootPathComponents">Root path components to root of iterator.</param>
    /// <param name="recursive">Iterate recursively.</param>
    public FileSystemEntryIterator(Media media,
        PartitionTableType partitionTableType,
        int partitionNumber,
        IFileSystem fileSystem,
        string[] rootPathComponents,
        bool recursive)
    {
        this.media = media;
        this.partitionTableType = partitionTableType;
        this.partitionNumber = partitionNumber;
        this.fileSystem = fileSystem;
        this.rootPathComponents = rootPathComponents;
        this.recursive = recursive;
        AttributesMode = GetAttributesMode(fileSystem);
    }

    private static AttributesMode GetAttributesMode(IFileSystem fileSystem)
    {
        return fileSystem switch
        {
            ExtFileSystem => AttributesMode.Unix,
            FatFileSystem or NtfsFileSystem => AttributesMode.Windows,
            _ => AttributesMode.Auto
        };
    }

    public PartitionTableType PartitionTableType => partitionTableType;
    public int PartitionNumber => partitionNumber;

    /// <summary>
    /// Root path components of iterator.
    /// </summary>
    public string[] PathComponents => rootPathComponents;

    /// <summary>
    /// Dir path components from root path components that exist and is set during initialization.
    /// </summary>
    public string[] DirPathComponents { get; private set; } = [];

    public AttributesMode AttributesMode { get; }

    public Media Media => media;

    public Entry Current => currentEntry;

    public bool HasMoreEntries => nextEntries.Count > 0;

    public bool IsSingleFileEntryNext { get; private set; }
    
    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            if (fileSystem is IDisposable disposable)
            {
                disposable.Dispose();
            }

            media.Dispose();
        }

        disposed = true;
    }

    public void Dispose() => Dispose(true);

    public IMediaPath MediaPath => mediaPath;

    public Task<Result> Initialize()
    {
        if (rootPathComponents.Length == 0)
        {
            DirPathComponents = [];
            pathComponentMatcher = new PathComponentMatcher(rootPathComponents, recursive: recursive);
            initialized = true;
            return Task.FromResult(new Result());
        }

        var dirComponents = new List<string>();
        var usePattern = false;

        var validDirComponents = new List<string>();

        foreach (var pathComponent in rootPathComponents)
        {
            dirComponents.Add(pathComponent);

            var dirPath = mediaPath.Join(dirComponents.ToArray());

            if (fileSystem.DirectoryExists(dirPath))
            {
                validDirComponents.Add(pathComponent);
                continue;
            }

            // use pattern, if last path component is not a directory
            if (validDirComponents.Count == PathComponents.Length - 1)
            {
                usePattern = true;
                IsSingleFileEntryNext = fileSystem.FileExists(dirPath) && PathComponents.Length > 0;
                if (IsSingleFileEntryNext || PathComponentHelper.HasWildcard(pathComponent))
                {
                    break;
                }
            }

            return Task.FromResult(new Result(new PathNotFoundError($"Path not found '{dirPath}'", dirPath)));
        }

        DirPathComponents = validDirComponents.ToArray();
        pathComponentMatcher = new PathComponentMatcher(usePattern ? dirComponents.ToArray() : [], recursive: recursive);

        initialized = true;
        return Task.FromResult(new Result());
    }
    
    private void ThrowIfNotInitialized()
    {
        if (initialized)
        {
            return;
        }

        throw new InvalidOperationException("File system entry iterator not initialized");
    }
    
    public Task<bool> Next()
    {
        ThrowIfNotInitialized();
        
        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            EnqueueDirectory(DirPathComponents);
        }

        if (nextEntries.Count <= 0)
        {
            return Task.FromResult(false);
        }

        bool skipEntry;
        do
        {
            skipEntry = false;
            currentEntry = nextEntries.Pop();

            if (currentEntry.Type == Models.FileSystems.EntryType.File)
            {
                return Task.FromResult(true);
            }

            if (recursive)
            {
                var entriesEnqueued = EnqueueDirectory(currentEntry.FullPathComponents);
                skipEntry = pathComponentMatcher.UsesPattern && entriesEnqueued == 0;
            }
            else
            {
                skipEntry = currentEntry.FullPathComponents.Length < pathComponentMatcher.PathComponents.Length ||
                            !pathComponentMatcher.IsMatch(currentEntry.FullPathComponents);
            }
        } while (nextEntries.Count > 0 && skipEntry);

        return Task.FromResult(true);
    }

    private int EnqueueDirectory(string[] pathComponents)
    {
        var path = mediaPath.Join(pathComponents);

        if (dirPathsIteratedIndex.Contains(path))
        {
            return 0;
        }

        dirPathsIteratedIndex.Add(path);

        if (!fileSystem.Exists(path))
        {
            return 0;
        }
        
        var uniqueEntries = new Dictionary<string, Entry>();

        foreach (var dirPath in fileSystem.GetDirectories(path, "*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(x => x).ToList())
        {
            DateTime? lastWriteTime = null;
            try
            {
                lastWriteTime = fileSystem.GetLastWriteTime(dirPath);
            }
            catch (Exception)
            {
                // ignored
            }

            var properties = new Dictionary<string, string>(GetAttributeProperties(dirPath));

            var attributes = string.Empty;

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, DirPathComponents,
                recursive, dirPath, dirPath, true, lastWriteTime ?? DateTime.Now, 0,
                attributes, properties, attributes).ToList();

            foreach (var entry in entries)
            {
                var entryPath = mediaPath.Join(entry.FullPathComponents);

                if (path.Equals(entry.Name) ||
                    (entry.Type == Models.FileSystems.EntryType.Dir && dirPathsIteratedIndex.Contains(entryPath)) ||
                    (entry.Type == Models.FileSystems.EntryType.Dir && uniqueEntries.ContainsKey(entry.RawPath)))
                {
                    continue;
                }

                uniqueEntries[entry.Name] = entry;
            }
        }

        foreach (var filePath in fileSystem.GetFiles(path, "*", SearchOption.TopDirectoryOnly).OrderByDescending(x => x).ToList())
        {
            DiscFileInfo fileInfo = null;

            try
            {
                fileInfo = fileSystem.GetFileInfo(filePath);

                // file is opened to verify the file can be accessed
                using var fileStream = fileSystem.OpenFile(filePath, FileMode.Open);
            }
            catch (Exception)
            {
                // ignored
                continue;
            }

            // skip file, if it's not possible to get file info or open the file
            if (fileInfo == null)
            {
                continue;
            }

            var lastWriteTime = fileInfo.LastWriteTime;
            var size = fileInfo.Length;

            var properties = new Dictionary<string, string>(GetAttributeProperties(filePath));

            var attributes = string.Empty;

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, DirPathComponents,
                recursive, filePath, filePath, false, lastWriteTime, size,
                attributes, properties, attributes).ToList();

            foreach (var entry in entries)
            {
                var entryPath = mediaPath.Join(entry.FullPathComponents);

                if (path.Equals(entry.Name) ||
                    (entry.Type == Models.FileSystems.EntryType.Dir && dirPathsIteratedIndex.Contains(entryPath)) ||
                    (entry.Type == Models.FileSystems.EntryType.Dir && uniqueEntries.ContainsKey(entry.RawPath)))
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

        return uniqueEntries.Values.Count;
    }

    private IEnumerable<KeyValuePair<string, string>> GetAttributeProperties(string path)
    {
        var attributeProperties = new List<KeyValuePair<string, string>>();
        
        try
        {
            switch (fileSystem)
            {
                case ExtFileSystem extFileSystem:
                    var unixFileInfo = extFileSystem.GetUnixFileInfo(path);
                    attributeProperties.Add(new KeyValuePair<string, string>(Core.Constants.EntryPropertyNames.UnixFileMode,
                        unixFileInfo?.Permissions.ToString()));
                    break;
                case FatFileSystem fatFileSystem:
                    var fileAttributes = fatFileSystem.GetAttributes(path);
                    attributeProperties.Add(new KeyValuePair<string, string>(Core.Constants.EntryPropertyNames.WindowsAttributes,
                        fileAttributes.ToString()));
                    break;
                case NtfsFileSystem ntfsFileSystem:
                    var ntfsFileAttributes = ntfsFileSystem.GetAttributes(path);
                    attributeProperties.Add(new KeyValuePair<string, string>(Core.Constants.EntryPropertyNames.WindowsAttributes,
                        ntfsFileAttributes.ToString()));
                    break;
            }
        }
        catch (Exception)
        {
            // ignored
        }

        return attributeProperties;
    }

    public Task<Stream> OpenEntry(Entry entry)
    {
        return entry.Size == 0
            ? Task.FromResult(new MemoryStream() as Stream)
            : Task.FromResult(fileSystem.OpenFile(entry.RawPath, FileMode.Open) as Stream);
    }

    public Task<Result> DeleteEntry(string[] fullPathComponents)
    {
        var entryPath = mediaPath.Join(fullPathComponents);

        if (fileSystem.FileExists(entryPath))
        {
            fileSystem.DeleteFile(entryPath);
            return Task.FromResult(new Result());
        }

        fileSystem.DeleteDirectory(entryPath);
        return Task.FromResult(new Result());
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