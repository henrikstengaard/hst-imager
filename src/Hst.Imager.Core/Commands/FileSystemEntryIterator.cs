namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Ext;
using DiscUtils.Fat;
using Hst.Imager.Core.PathComponents;
using Hst.Imager.Core.UaeMetadatas;
using Models;
using Entry = Models.FileSystems.Entry;

public class FileSystemEntryIterator : IEntryIterator
{
    private readonly Media media;
    private readonly IMediaPath mediaPath;
    private readonly string rootPath;
    private string[] rootPathComponents;
    private PathComponentMatcher pathComponentMatcher;
    private readonly IFileSystem fileSystem;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private bool disposed;

    public FileSystemEntryIterator(Media media, IFileSystem fileSystem, string rootPath, bool recursive)
    {
        this.media = media;
        this.mediaPath = CreateMediaPath(fileSystem);
        this.rootPath = rootPath;
        this.recursive = recursive;
        this.fileSystem = fileSystem;
        this.nextEntries = new Stack<Entry>();
        this.currentEntry = null;
        this.isFirst = true;

        var pathComponents = GetPathComponents(rootPath);
        this.pathComponentMatcher = new PathComponentMatcher(pathComponents, recursive);
        this.rootPathComponents = this.pathComponentMatcher.PathComponents;
    }

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

    public string RootPath => rootPath;

    public Entry Current => currentEntry;

    public Task<bool> Next()
    {
        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            ResolveRootPath();
            EnqueueDirectory(this.rootPathComponents);
        }

        if (this.nextEntries.Count <= 0)
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

    private void ResolveRootPath()
    {
        var pathComponents = GetPathComponents(rootPath);

        if (pathComponents.Length == 0)
        {
            this.rootPathComponents = pathComponents;
            this.pathComponentMatcher = new PathComponentMatcher(pathComponents, recursive);
            return;
        }

        var dirComponents = new List<string>();
        var usePattern = false;
        foreach (var pathComponent in pathComponents)
        {
            dirComponents.Add(pathComponent);

            var dirPath = mediaPath.Join(dirComponents.ToArray());

            try
            {
                fileSystem.GetLastAccessTime(dirPath);
            }
            catch (Exception)
            {
                throw new IOException($"Path not found '{dirPath}'");
            }

            if (dirComponents.Count == pathComponents.Length - 1)
            {
                usePattern = true;
                break;
            }
        }

        rootPathComponents = dirComponents.ToArray();
        pathComponentMatcher = new PathComponentMatcher(usePattern ? pathComponents : Array.Empty<string>(), recursive);
    }

    private int EnqueueDirectory(string[] pathComponents)
    {
        var uniqueEntries = new Dictionary<string, Entry>();

        var path = mediaPath.Join(pathComponents);

        foreach (var dirPath in fileSystem.GetDirectories(path, "*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(x => x).ToList())
        {
            var fullPathComponents = mediaPath.Split(dirPath);

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

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, rootPathComponents,
                recursive, dirPath, dirPath, true, lastWriteTime ?? DateTime.Now, 0,
                attributes, properties, dirAttributes).ToList();

            foreach (var entry in entries)
            {
                if (rootPath.Equals(entry.RawPath) ||
                    path.Equals(entry.Name) ||
                    (entry.Type == Models.FileSystems.EntryType.Dir && uniqueEntries.ContainsKey(entry.Name)))
                {
                    continue;
                }

                uniqueEntries[entry.Name] = entry;
            }

        }

        foreach (var filePath in fileSystem.GetFiles(path, "*", SearchOption.TopDirectoryOnly).OrderByDescending(x => x).ToList())
        {
            var fullPathComponents = mediaPath.Split(filePath);

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

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, rootPathComponents,
                recursive, filePath, filePath, false, lastWriteTime, size,
                attributes, properties, dirAttributes).ToList();

            foreach (var entry in entries)
            {
                if (rootPath.Equals(entry.RawPath) ||
                    path.Equals(entry.Name) ||
                    (entry.Type == Models.FileSystems.EntryType.Dir && uniqueEntries.ContainsKey(entry.Name)))
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
            default:
                return string.Empty;
        }
    }

    private static IMediaPath CreateMediaPath(IFileSystem fileSystem)
    {
        switch (fileSystem)
        {
            case ExtFileSystem extFileSystem:
                return new ForwardSlashMediaPath();
            case FatFileSystem fatFileSystem:
            default:
                return new BackslashMediaPath();
        }
    }

    private OperatingSystemEnum GetOperatingSystem()
    {
        return fileSystem switch
        {
            ExtFileSystem => OperatingSystemEnum.Linux,
            FatFileSystem => OperatingSystemEnum.Windows,
            _ => OperatingSystemEnum.Other
        };
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
        var fatAttributes = "ARHS";

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

    public bool UsesPattern => this.pathComponentMatcher?.UsesPattern ?? false;

    public Task Flush()
    {
        return Task.CompletedTask;
    }
    private bool SkipEntry(string[] pathComponents)
    {
        if (!UsesPattern)
        {
            return false;
        }

        return !pathComponentMatcher.IsMatch(pathComponents);
    }

    public UaeMetadata UaeMetadata { get; set; }
}