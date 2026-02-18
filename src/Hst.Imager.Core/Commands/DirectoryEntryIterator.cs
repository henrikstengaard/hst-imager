using Hst.Core;
using Hst.Imager.Core.Caching;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PathComponents;
using UaeMetadatas;
using Entry = Models.FileSystems.Entry;

/// <summary>
/// Local directory entry iterator. An iterator that iterates over entries in a local directory.
/// It preserves the path as-is and will use UAE metadata to resolve normalized filenames when available for each path component.
/// </summary>
public class DirectoryEntryIterator : IEntryIterator
{
    public PartitionTableType PartitionTableType => PartitionTableType.None;
    public int PartitionNumber => 0;

    private readonly Stack<Entry> nextEntries;
    private readonly string rootPath;
    private readonly string[] rootPathComponents;
    private PathComponentMatcher pathComponentMatcher;
    private readonly bool recursive;
    private Entry currentEntry;
    private bool isFirst;
    private bool initialized;
    private readonly IAppCache appCache;
    private readonly UaeMetadataHelper uaeMetadataHelper;

    public DirectoryEntryIterator(string path, bool recursive, UaeMetadata uaeMetadata, IAppCache appCache)
    {
        this.nextEntries = new Stack<Entry>();
        rootPath = PathHelper.GetFullPath(path);
        this.recursive = recursive;
        this.isFirst = true;
        this.UaeMetadata = uaeMetadata;
        this.appCache = appCache;
        uaeMetadataHelper = new UaeMetadataHelper(appCache);
        rootPathComponents = PathHelper.Split(rootPath);
    }

    public async Task<Result> Initialize()
    {
        if (rootPathComponents.Length == 0)
        {
            return new Result(new PathNotFoundError($"Path not found '{rootPath}'", rootPath));
        }

        var pathComponents = rootPathComponents;
        var firstPathComponent = pathComponents[0];
        var dirPath = string.Empty;
        var dirComponents = new List<string>();
        var validDirComponents = new List<string>();

        var isWindowsRoot = PathHelper.IsWindowsRootPath(firstPathComponent);
        if (PathHelper.IsRootPath(firstPathComponent))
        {
            dirPath = isWindowsRoot ? firstPathComponent : "/";
            if (isWindowsRoot)
            {
                dirComponents.Add(firstPathComponent);
                validDirComponents.Add(firstPathComponent);
                pathComponents = pathComponents.Skip(1).ToArray();
            }
        }

        var usePattern = false;

        for (var i = 0; i < pathComponents.Length; i++)
        {
            var pathComponent = pathComponents[i];
            dirComponents.Add(pathComponent);

            var entryPath = Path.Combine(dirPath, pathComponent);
            var dirExists = Directory.Exists(entryPath);
            var fileExists = File.Exists(entryPath);

            if (dirExists)
            {
                validDirComponents.Add(pathComponent);
                dirPath = entryPath;
                continue;
            }
            
            if (fileExists && i < pathComponents.Length - 1)
            {
                return new Result(new PathNotFoundError($"Path '{entryPath}' is a file and not a directory", entryPath));
            }

            // get uae metadata entry for dir components
            var uaeMetadataEntry = await uaeMetadataHelper.GetUaeMetadataEntry(UaeMetadata, dirComponents.ToArray());
        
            if (uaeMetadataEntry is { UaeMetadataExists: true })
            {
                var uaePathComponent = uaeMetadataEntry.NormalPathComponents[^1];
                entryPath = Path.Combine(dirPath, uaePathComponent);

                if (Directory.Exists(entryPath))
                {
                    validDirComponents.Add(uaePathComponent);
                    dirPath = Path.Combine(dirPath, uaePathComponent);
                    continue;
                }
            }
            
            // use pattern, if last path component is not a directory
            if (validDirComponents.Count == rootPathComponents.Length - 1)
            {
                var uaeMetadataFileExist = false;
                if (uaeMetadataEntry is { UaeMetadataExists: true })
                {
                    var uaePathComponent = uaeMetadataEntry.NormalPathComponents[^1];
                    entryPath = Path.Combine(validDirComponents.Concat([uaePathComponent]).ToArray());
                    uaeMetadataFileExist = File.Exists(entryPath);
                }

                usePattern = true;
                IsSingleFileEntryNext = (fileExists || uaeMetadataFileExist) &&
                    pathComponents.Length > 0;
                if (IsSingleFileEntryNext || PathComponentHelper.HasWildcard(pathComponent))
                {
                    break;
                }
            }

            return new Result(new PathNotFoundError(
                $"Path not found '{dirPath}'", dirPath));
        }

        DirPathComponents = rootPathComponents.Take(validDirComponents.Count).ToArray();
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
    public string[] DirPathComponents { get; private set; }

    public Media Media => null;

    public Entry Current => currentEntry;

    public bool HasMoreEntries => nextEntries.Count > 0;
    public bool IsSingleFileEntryNext { get; private set; }

    public async Task<bool> Next()
    {
        ThrowIfNotInitialized();

        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            await EnqueueDirectory(DirPathComponents);
        }

        if (this.nextEntries.Count <= 0)
        {
            return false;
        }

        bool skipEntry;
        do
        {
            currentEntry = this.nextEntries.Pop();

            if (currentEntry.Type == Models.FileSystems.EntryType.File)
            {
                return true;
            }

            if (recursive)
            {
                var (dirEntriesEnqueued, fileEntriesEnqueued) = await EnqueueDirectory(currentEntry.FullPathComponents);
                skipEntry = pathComponentMatcher.UsesPattern && dirEntriesEnqueued + fileEntriesEnqueued == 0 ||
                            !pathComponentMatcher.IsMatch(currentEntry.FullPathComponents);
            }
            else
            {
                skipEntry = currentEntry.FullPathComponents.Length < pathComponentMatcher.PathComponents.Length ||
                            !pathComponentMatcher.IsMatch(currentEntry.FullPathComponents);
            }
            
            if (skipEntry)
            {
                currentEntry = null;
            }
        } while (nextEntries.Count > 0 && skipEntry);

        return currentEntry != null;
    }

    public Task<Stream> OpenEntry(Entry entry)
    {
        return Task.FromResult<Stream>(File.OpenRead(entry.RawPath));
    }

    public string[] GetPathComponents(string path) => PathHelper.Split(path);

    public bool UsesPattern => pathComponentMatcher.UsesPattern;

    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public bool SupportsUaeMetadata => true;

    private async Task<(int, int)> EnqueueDirectory(string[] pathComponents)
    {
        // get uae metadata node for path components
        var uaeMetadataNode = await uaeMetadataHelper.GetUaeMetadataEntry(UaeMetadata, pathComponents);
        
        // if uae metadata node exists, use its path components
        if (uaeMetadataNode != null)
        {
            pathComponents = uaeMetadataNode.NormalPathComponents;
        }
        
        var currentPath = Path.Combine(pathComponents);
        var currentDir = new DirectoryInfo(currentPath);

        // return 0 directories and 0 files enqueued, if directory does not exist
        // this happens when running multiple tests creating and deleting directories quickly
        if (!currentDir.Exists)
        {
            return (0, 0);
        }

        var dirEntriesEnqueued = 0;
        var fileEntriesEnqueued = 0;
        
        foreach (var dirInfo in currentDir.GetDirectories().OrderByDescending(x => x.Name).ToList())
        {
            var fullPathComponents = GetPathComponents(dirInfo.FullName);

            var date = dirInfo.LastWriteTime;
            var properties = new Dictionary<string, string>();

            var relativePathComponents = fullPathComponents.Skip(DirPathComponents.Length).ToArray();

            if (UaeMetadata != UaeMetadata.None)
            {
                var uaeMetadataEntry = await uaeMetadataHelper.GetUaeMetadataEntry(UaeMetadata, fullPathComponents);

                if (uaeMetadataEntry != null)
                {
                    relativePathComponents = uaeMetadataEntry.UaePathComponents.Skip(DirPathComponents.Length).ToArray();
                    date = uaeMetadataEntry.Date ?? DateTime.Now;
                    if (uaeMetadataEntry.Comment != null)
                    {
                        properties[Core.Constants.EntryPropertyNames.Comment] = uaeMetadataEntry.Comment;
                    }

                    if (uaeMetadataEntry.ProtectionBits.HasValue)
                    {
                        properties[Core.Constants.EntryPropertyNames.ProtectionBits] = uaeMetadataEntry.ProtectionBits.ToString();
                    }
                }
            }

            var relativePath = string.Join(Path.DirectorySeparatorChar, relativePathComponents);

            this.nextEntries.Push(new Entry
            {
                Name = relativePath,
                FormattedName = relativePath,
                RawPath = dirInfo.FullName,
                FullPathComponents = fullPathComponents,
                RelativePathComponents = relativePathComponents,
                Date = date,
                Size = 0,
                Type = Models.FileSystems.EntryType.Dir,
                Properties = properties
            });
            
            dirEntriesEnqueued++;
        }

        var fileInfos = currentDir.GetFiles().AsEnumerable();

        if (UaeMetadata != UaeMetadata.None)
        {
            fileInfos = RemoveUaeMetadataFiles(fileInfos);
        }

        foreach (var fileInfo in fileInfos.OrderByDescending(x => x.Name).ToList())
        {
            var fullPathComponents = GetPathComponents(fileInfo.FullName);

            var date = fileInfo.LastWriteTime;
            var properties = new Dictionary<string, string>();

            var relativePathComponents = fullPathComponents.Skip(DirPathComponents.Length).ToArray();

            if (UaeMetadata != UaeMetadata.None)
            {
                var uaeMetadataEntry = await uaeMetadataHelper.GetUaeMetadataEntry(UaeMetadata, fullPathComponents);

                if (uaeMetadataEntry != null)
                {
                    relativePathComponents = uaeMetadataEntry.UaePathComponents.Skip(DirPathComponents.Length).ToArray();

                    if (uaeMetadataEntry.Date.HasValue)
                    {
                        date = uaeMetadataEntry.Date.Value;
                    }
                    
                    if (uaeMetadataEntry.Comment != null)
                    {
                        properties[Core.Constants.EntryPropertyNames.Comment] = uaeMetadataEntry.Comment;
                    }

                    if (uaeMetadataEntry.ProtectionBits.HasValue)
                    {
                        properties[Core.Constants.EntryPropertyNames.ProtectionBits] = uaeMetadataEntry.ProtectionBits.ToString();
                    }

                    fullPathComponents = DirPathComponents.Concat(relativePathComponents).ToArray();
                }
            }

            if (!pathComponentMatcher.IsMatch(fullPathComponents))
            {
                continue;
            }

            var relativePath = string.Join(Path.DirectorySeparatorChar, relativePathComponents);

            this.nextEntries.Push(new Entry
            {
                Name = relativePath,
                FormattedName = relativePath,
                RawPath = fileInfo.FullName,
                FullPathComponents = fullPathComponents,
                RelativePathComponents = relativePathComponents,
                Date = date,
                Size = fileInfo.Length,
                Type = Models.FileSystems.EntryType.File,
                Properties = properties
            });

            fileEntriesEnqueued++;
        }

        return (dirEntriesEnqueued, fileEntriesEnqueued);
    }

    public void Dispose()
    {
        appCache.Dispose();
    }

    public UaeMetadata UaeMetadata { get; set; }

    private IEnumerable<FileInfo> RemoveUaeMetadataFiles(IEnumerable<FileInfo> fileInfos) =>
        fileInfos.Where(file =>
            !file.Name.Equals(Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName, StringComparison.OrdinalIgnoreCase) &&
            !file.Extension.Equals(Amiga.DataTypes.UaeMetafiles.Constants.UaeMetafileExtension, StringComparison.OrdinalIgnoreCase));
}