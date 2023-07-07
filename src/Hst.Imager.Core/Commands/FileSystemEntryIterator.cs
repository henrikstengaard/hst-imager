namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Ext;
using DiscUtils.Fat;
using Models;
using Entry = Models.FileSystems.Entry;

public class FileSystemEntryIterator : IEntryIterator
{
    private readonly Media media;
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
        this.rootPath = rootPath.EndsWith("\\") ? rootPath : string.Concat(rootPath, "\\");
        this.rootPathComponents = GetPathComponents(this.rootPath);
        this.pathComponentMatcher = null;
        this.recursive = recursive;
        this.fileSystem = fileSystem;
        this.nextEntries = new Stack<Entry>();
        this.currentEntry = null;
        this.isFirst = true;
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

        if (this.pathComponentMatcher.UsesPattern)
        {
            do
            {
                if (this.nextEntries.Count <= 0)
                {
                    return Task.FromResult(false);
                }

                currentEntry = this.nextEntries.Pop();
                if (this.recursive && currentEntry.Type == Models.FileSystems.EntryType.Dir)
                {
                    EnqueueDirectory(currentEntry.FullPathComponents);
                }
            } while (currentEntry.Type == Models.FileSystems.EntryType.Dir);
        }
        else
        {
            currentEntry = this.nextEntries.Pop();
            if (this.recursive && currentEntry.Type == Models.FileSystems.EntryType.Dir)
            {
                EnqueueDirectory(currentEntry.FullPathComponents);
            }
        }

        return Task.FromResult(true);
    }

    private void ResolveRootPath()
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

    private void EnqueueDirectory(string[] currentPathComponents)
    {
        var currentPath = string.Join("\\", currentPathComponents);

        try
        {
            foreach (var dirPath in fileSystem.GetDirectories(currentPath).OrderByDescending(x => x).ToList())
            {
                var fullPathComponents = GetPathComponents(dirPath);
                var relativePathComponents = fullPathComponents.Skip(this.rootPathComponents.Length).ToArray();
                var relativePath = string.Join(Path.DirectorySeparatorChar, relativePathComponents);

                DateTime? lastWriteTime = null;
                try
                {
                    lastWriteTime = fileSystem.GetLastWriteTime(dirPath);
                }
                catch (Exception)
                {
                    // ignored
                }

                var dirEntry = new Entry
                {
                    Name = relativePath,
                    FormattedName = relativePath,
                    RawPath = dirPath,
                    FullPathComponents = fullPathComponents,
                    RelativePathComponents = relativePathComponents,
                    Attributes = Format(dirPath),
                    Date = lastWriteTime,
                    Size = 0,
                    Type = Models.FileSystems.EntryType.Dir,
                    Properties = new Dictionary<string, string>()
                };

                if (recursive || this.pathComponentMatcher.IsMatch(dirEntry.FullPathComponents))
                {
                    this.nextEntries.Push(dirEntry);
                }
            }
        }
        catch (Exception)
        {
            // ignored
        }

        try
        {
            foreach (var filePath in fileSystem.GetFiles(currentPath).OrderByDescending(x => x).ToList())
            {
                var fullPathComponents = GetPathComponents(filePath);
                var relativePathComponents = fullPathComponents.Skip(this.rootPathComponents.Length).ToArray();
                var relativePath = string.Join(Path.DirectorySeparatorChar, relativePathComponents);

                DateTime? lastWriteTime = null;
                long size = 0;
                try
                {
                    lastWriteTime = fileSystem.GetLastWriteTime(filePath);
                    size = fileSystem.GetFileLength(filePath);
                }
                catch (Exception)
                {
                    // ignored
                }

                var fileEntry = new Entry
                {
                    Name = relativePath,
                    FormattedName = relativePath,
                    RawPath = filePath,
                    FullPathComponents = fullPathComponents,
                    RelativePathComponents = relativePathComponents,
                    Attributes = Format(filePath),
                    Date = lastWriteTime,
                    Size = size,
                    Type = Models.FileSystems.EntryType.File,
                    Properties = new Dictionary<string, string>()
                };

                if (this.pathComponentMatcher.IsMatch(fileEntry.FullPathComponents))
                {
                    this.nextEntries.Push(fileEntry);
                }
            }
        }
        catch (Exception)
        {
            // ignored
        }
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
}