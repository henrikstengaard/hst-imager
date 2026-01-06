using DiscUtils.Ntfs;
using Hst.Core;

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
/// <param name="media">Media used by iterator.</param>
/// <param name="partitionTableType">Partition table type used by iterator.</param>
/// <param name="partitionNumber">Partition number used by iterator.</param>
/// <param name="fileSystem">File system to iterate through.</param>
/// <param name="rootPathComponents">Root path components to root of iterator.</param>
/// <param name="recursive">Iterate recursively.</param>
public class FileSystemEntryIterator(
    Media media,
    PartitionTableType partitionTableType,
    int partitionNumber,
    IFileSystem fileSystem,
    string[] rootPathComponents,
    bool recursive) : IEntryIterator
{
    private readonly IMediaPath mediaPath = MediaPath.GenericMediaPath;
    private PathComponentMatcher pathComponentMatcher;
    private readonly Stack<Entry> nextEntries = new();
    private bool isFirst = true;
    private Entry currentEntry;
    private bool initialized;
    private bool disposed;
    private readonly HashSet<string> dirPathsIteratedIndex = [];

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
                skipEntry = !EntryIteratorFunctions.IsRelativePathComponentsValid(pathComponentMatcher,
                    currentEntry.RelativePathComponents, recursive);
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
            var attributes = string.Empty;
            DateTime? lastWriteTime = null;
            try
            {
                lastWriteTime = fileSystem.GetLastWriteTime(dirPath);
                attributes = Format(dirPath);
            }
            catch (Exception)
            {
                // ignored
            }

            var properties = new Dictionary<string, string>();

            var dirAttributes = string.Empty;

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, DirPathComponents,
                recursive, dirPath, dirPath, true, lastWriteTime ?? DateTime.Now, 0,
                attributes, properties, dirAttributes).ToList();

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
            var attributes = Format(fileInfo.Attributes);

            var properties = new Dictionary<string, string>();

            var dirAttributes = string.Empty;

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, DirPathComponents,
                recursive, filePath, filePath, false, lastWriteTime, size,
                attributes, properties, dirAttributes).ToList();

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

    private string Format(string path)
    {
        switch (fileSystem)
        {
            case ExtFileSystem extFileSystem:
                var unixFileInfo = extFileSystem.GetUnixFileInfo(path);
                return Format(unixFileInfo);
            case FatFileSystem fatFileSystem:
                var fileAttributes = fatFileSystem.GetAttributes(path);
                return Format(fileAttributes);
            case NtfsFileSystem ntfsFileSystem:
                var ntfsFileAttributes = ntfsFileSystem.GetAttributes(path);
                return Format(ntfsFileAttributes);
            default:
                return string.Empty;
        }
    }

    private static string Format(UnixFileSystemInfo unixFileSystemInfo)
    {
        var unixAttributes = "rwxrwxrwx";

        if (unixFileSystemInfo?.Permissions == null)
        {
            return new string('-', unixAttributes.Length + 1);
        }

        var orderedPermissions = new[]
        {
            UnixFilePermissions.OwnerRead,
            UnixFilePermissions.OwnerWrite,
            UnixFilePermissions.OwnerExecute,
            UnixFilePermissions.GroupRead,
            UnixFilePermissions.GroupWrite,
            UnixFilePermissions.GroupExecute,
            UnixFilePermissions.OthersRead,
            UnixFilePermissions.OthersWrite,
            UnixFilePermissions.OthersExecute
        };

        return string.Concat(GetTypeAttribute(unixFileSystemInfo), FormatAttributes(unixAttributes,
            orderedPermissions.Select(x => unixFileSystemInfo.Permissions.HasFlag(x)).ToArray()));
    }

    private static char GetTypeAttribute(UnixFileSystemInfo unixFileSystemInfo)
    {
        switch (unixFileSystemInfo.FileType)
        {
            case UnixFileType.Link:
                return 'l';
            case UnixFileType.Directory:
                return 'd';
            default:
                return '-';
        }
    }

    private static string Format(FileAttributes? fileAttributes)
    {
        const string fatAttributes = "ARHS";

        if (fileAttributes == null)
        {
            return new string('-', fatAttributes.Length);
        }

        var orderedAttributes = new[]
        {
            FileAttributes.Archive,
            FileAttributes.ReadOnly,
            FileAttributes.Hidden,
            FileAttributes.System
        };

        return FormatAttributes(fatAttributes,
            orderedAttributes.Select(x => fileAttributes.Value.HasFlag(x)).ToArray());
    }

    private static string FormatAttributes(string attributes, bool[] presentAttributes)
    {
        var attributesArray = attributes.ToCharArray();
        for (var i = 0; i < presentAttributes.Length; i++)
        {
            if (presentAttributes[i])
            {
                continue;
            }

            attributesArray[i] = '-';
        }

        return new string(attributesArray);
    }

    public Task<Stream> OpenEntry(Entry entry)
    {
        return entry.Size == 0
            ? Task.FromResult(new MemoryStream() as Stream)
            : Task.FromResult(fileSystem.OpenFile(entry.RawPath, FileMode.Open) as Stream);
    }

    public string[] GetPathComponents(string path)
    {
        return path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
    }

    public bool UsesPattern => pathComponentMatcher.UsesPattern;

    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public bool SupportsUaeMetadata => false;

    public UaeMetadata UaeMetadata { get; set; }
}