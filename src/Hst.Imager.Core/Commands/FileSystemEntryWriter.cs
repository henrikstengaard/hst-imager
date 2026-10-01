using Hst.Core;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils;
using PathComponents;
using UaeMetadatas;
using Models;
using Entry = Models.FileSystems.Entry;

/// <summary>
/// File system entry writer.
/// </summary>
public class FileSystemEntryWriter : IEntryWriter
{
    private readonly byte[] buffer = new byte[4096];
    private readonly IMediaPath mediaPath = Core.PathComponents.MediaPath.GenericMediaPath;
    private bool disposed;
    private readonly HashSet<string> dirPathsCreated = new();

    /// <summary>
    /// Directory path components of root path components that exist.
    /// </summary>
    private string[] dirPathComponents = [];

    private bool lastPathComponentExist = true;
    private Models.FileSystems.EntryType lastPathComponentEntryType = Models.FileSystems.EntryType.Dir;
    private bool isInitialized = false;
    private readonly Media _media;
    private readonly PartitionTableType _partitionTableType;
    private readonly int _partitionNumber;
    private readonly IFileSystem _fileSystem;
    private readonly string[] _rootPathComponents;
    private readonly bool _recursive;
    private readonly bool _createDirectory;
    private readonly bool _forceOverwrite;

    /// <summary>
    /// File system entry writer.
    /// </summary>
    /// <param name="media">Media mounted.</param>
    /// <param name="partitionTableType">Partition table type mounted.</param>
    /// <param name="partitionNumber">Partition number mounted.</param>
    /// <param name="fileSystem">File system mounted to write entries.</param>
    /// <param name="rootPathComponents">Root path components.</param>
    /// <param name="recursive">Recursive creating directories and files.</param>
    /// <param name="createDirectory">Create directory for root path components, if it doesn't exist.</param>
    /// <param name="forceOverwrite">Force overwriting any existing files.</param>
    public FileSystemEntryWriter(Media media, PartitionTableType partitionTableType, int partitionNumber,
        IFileSystem fileSystem, string[] rootPathComponents, bool recursive, bool createDirectory, bool forceOverwrite)
    {
        _media = media;
        _partitionTableType = partitionTableType;
        _partitionNumber = partitionNumber;
        _fileSystem = fileSystem;
        _rootPathComponents = rootPathComponents;
        _recursive = recursive;
        _createDirectory = createDirectory;
        _forceOverwrite = forceOverwrite;
    }

    public Media Media => _media;
    public string MediaPath => _media.Path;
    public PartitionTableType PartitionTableType => _partitionTableType;
    public int PartitionNumber => _partitionNumber;
    public string FileSystemPath => string.Empty;
    public UaeMetadata UaeMetadata { get; set; }
    public string[] PathComponents => _rootPathComponents;
    public string[] DirPathComponents => dirPathComponents;

    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            if (_fileSystem is IDisposable disposable)
            {
                disposable.Dispose();
            }
            
            _media.Stream?.Flush();
            _media.Dispose();
        }

        disposed = true;
    }

    public void Dispose() => Dispose(true);

    public Task<Result> Initialize()
    {
        var exisingPathComponents = new List<string>(10);
            
        lastPathComponentExist = true;

        for (var i = 0; i < _rootPathComponents.Length; i++)
        {
            var dirPath = mediaPath.Join(exisingPathComponents.Concat([_rootPathComponents[i]]).ToArray());

            var fileSystemInfo = _fileSystem.GetFileSystemInfo(dirPath);
            
            var nextDirPath = string.Join("/", _rootPathComponents.Take(i + 1));

            if (!fileSystemInfo.Exists && fileSystemInfo is DiscFileInfo && i < _rootPathComponents.Length - 1)
            {
                return Task.FromResult(new Result(new PathNotFoundError(
                    $"Path '{nextDirPath}' is a file and not a directory", nextDirPath)));
            }
            
            if (!fileSystemInfo.Exists)
            {
                if (!_createDirectory)
                {
                    if (i != _rootPathComponents.Length - 1)
                    {
                        return Task.FromResult(new Result(new PathNotFoundError(
                            $"Path not found '{nextDirPath}'", nextDirPath)));
                    }
                
                    lastPathComponentExist = false;

                    break;
                }
                
                _fileSystem.CreateDirectory(dirPath);
            }
            else
            {
                if (i == _rootPathComponents.Length - 1)
                {
                    lastPathComponentEntryType = _fileSystem.DirectoryExists(fileSystemInfo.FullName)
                        ? Models.FileSystems.EntryType.Dir : Models.FileSystems.EntryType.File;

                    if (!_fileSystem.DirectoryExists(fileSystemInfo.FullName))
                    {
                        break;
                    }
                }
            }
            
            exisingPathComponents.Add(_rootPathComponents[i]);
        }

        if (_recursive && !lastPathComponentExist)
        {
            var path = string.Join("/", _rootPathComponents);
            return Task.FromResult(new Result(new PathNotFoundError($"Path '{path}' not found. Directory must exist when using recursive!", path)));
        }

        dirPathComponents = exisingPathComponents.ToArray();

        isInitialized = true;
        
        return Task.FromResult(new Result());
    }

    public Task<Result> CreateDirectory(Entry entry, string[] entryPathComponents, bool skipAttributes,
        bool isSingleFileEntry)
    {
        if (!isInitialized)
        {
            return Task.FromResult(new Result(new Error("FileSystemEntryWriter is not initialized.")));
        }
        
        var fullPathComponents = PathComponentHelper.GetFullPathComponents(entry.Type, entryPathComponents,
            lastPathComponentEntryType, _rootPathComponents, lastPathComponentExist, isSingleFileEntry);

        if (fullPathComponents.Length == 0)
        {
            return Task.FromResult(new Result());
        }
        
        var requiredPathComponentsToExist = isSingleFileEntry ? dirPathComponents : _rootPathComponents;

        var path = mediaPath.Join(requiredPathComponentsToExist);
        if (!_fileSystem.Exists(path))
        {
            return Task.FromResult(new Result(new PathNotFoundError($"Path not found '{path}'", path)));
        }

        return Task.FromResult(CreateFileSystemDirectory(fullPathComponents));
    }

    private Result CreateFileSystemDirectory(string[] pathComponents)
    {
        for (var i = 1; i <= pathComponents.Length; i++)
        {
            var dirPath = mediaPath.Join(pathComponents.Take(i).ToArray()).ToLower();

            if (dirPathsCreated.Contains(dirPath))
            {
                continue;
            }

            var path = mediaPath.Join(pathComponents.Take(i).ToArray());

            if (_fileSystem.FileExists(path))
            {
                return new Result(new Error($"Create directory path '{path}' failed. Path already exists as a file!"));
            }
            
            _fileSystem.CreateDirectory(path);

            dirPathsCreated.Add(dirPath);
        }
        
        return new Result();
    }

    public async Task<Result> CreateFile(Entry entry, string[] entryPathComponents, Stream stream, bool skipAttributes,
        bool isSingleFileEntry)
    {
        if (!isInitialized)
        {
            return new Result(new Error("FileSystemEntryWriter is not initialized."));
        }

        var fullPathComponents = PathComponentHelper.GetFullPathComponents(entry.Type, entryPathComponents,
            lastPathComponentEntryType, _rootPathComponents, lastPathComponentExist, isSingleFileEntry);

        var fullPath = mediaPath.Join(fullPathComponents);

        if (!_forceOverwrite && _fileSystem.FileExists(fullPath))
        {
            return new Result(new FileExistsError($"File already exists '{fullPath}'"));
        }

        await using var entryStream = _fileSystem.OpenFile(fullPath, FileMode.OpenOrCreate);
        int bytesRead;
        do
        {
            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            await entryStream.WriteAsync(buffer, 0, bytesRead);
        } while (bytesRead != 0);
        
        return new Result();
    }

    public Task<Result> MoveEntry(Entry entry, string[] srcEntryPathComponents, bool singleFile)
    {
        if (!isInitialized)
        {
            return Task.FromResult(new Result(new Error("FileSystemEntryWriter is not initialized.")));
        }

        var destFullPathComponents = PathComponentHelper.GetFullPathComponents(entry.Type, srcEntryPathComponents,
            lastPathComponentEntryType, _rootPathComponents, lastPathComponentExist, singleFile);

        var srcEntryPath = mediaPath.Join(entry.FullPathComponents);
        var destEntryPath = mediaPath.Join(destFullPathComponents);

        if (string.IsNullOrEmpty(destEntryPath))
        {
            return Task.FromResult(new Result(new Error("Destination path is null or empty.")));
        }

        if (!_fileSystem.Exists(srcEntryPath))
        {
            return Task.FromResult(new Result(new PathNotFoundError($"Source path '{srcEntryPath}' not found", srcEntryPath)));
        }
        
        var destEntryExists = _fileSystem.Exists(destEntryPath);
        if (!_forceOverwrite && destEntryExists)
        {
            return Task.FromResult(new Result(new PathExistsError($"Destination path '{destEntryPath}' already exists")));
        }
        
        if (entry.Type == Models.FileSystems.EntryType.Dir)
        {
            _fileSystem.MoveDirectory(srcEntryPath, destEntryPath);
        }
        else
        {
            if (destEntryExists && _forceOverwrite)
            {
                _fileSystem.DeleteFile(destEntryPath);
            }

            _fileSystem.MoveFile(srcEntryPath, destEntryPath);
        }
        
        return Task.FromResult(new Result());
    }

    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetDebugLogs()
    {
        return new List<string>();
    }

    public IEnumerable<string> GetLogs()
    {
        return new List<string>();
    }

    public IEntryIterator CreateEntryIterator(string[] rootPathComponents, bool recursive)
    {
        return new FileSystemEntryIterator(_media, _partitionTableType, _partitionNumber, _fileSystem, rootPathComponents,
            recursive);
    }

    private bool IsSameMediaAndPartition(IEntryIterator entryIterator) =>
        entryIterator.Media != null && _media.Equals(entryIterator.Media) &&
        entryIterator.PartitionTableType == PartitionTableType &&
        entryIterator.PartitionNumber == PartitionNumber;

    public bool ArePathComponentsSelfCopy(IEntryIterator entryIterator)
    {
        // return false, if not an file system entry iterator or not same media.
        // self copy/extract is only possible, when copying/extracting from and to same media and partition.
        if (entryIterator is not FileSystemEntryIterator ||
            !IsSameMediaAndPartition(entryIterator))
        {
            return false;
        }
        
        var sameDirPathComponents = entryIterator.DirPathComponents.Length == dirPathComponents.Length &&
                                    entryIterator.DirPathComponents.SequenceEqual(dirPathComponents);

        // return false, if it's not same dir path components or if it's not a single file copy
        if (!sameDirPathComponents || !entryIterator.IsSingleFileEntryNext)
        {
            return false;
        }
        
        var lastPathComponent = _rootPathComponents.Length > 0 ? _rootPathComponents[^1] : string.Empty;

        // return true, if last writer path component is empty or if last writer path component exist and
        // is same as last iterator path component
        return string.IsNullOrEmpty(lastPathComponent) ||
               lastPathComponentExist && entryIterator.PathComponents[^1].Equals(lastPathComponent);
    }

    public bool ArePathComponentsCyclic(IEntryIterator entryIterator)
    {
        // return false, if not same media and partition.
        // cyclic copy/extract is only possible, when copying/extracting from and to same media and partition.
        if (!IsSameMediaAndPartition(entryIterator))
        {
            return false;
        }

        // return false, if not recursive
        if (!_recursive && entryIterator.IsSingleFileEntryNext)
        {
            return false;
        }

        // array of path components that in length is the same between iterator and writer
        var sameDirPathComponents = entryIterator.DirPathComponents.Length > 0 && dirPathComponents.Length > 1
            ? dirPathComponents.Take(entryIterator.DirPathComponents.Length).ToArray()
            : [];

        // true, if writer has same and more path components than iterator
        var hasSameAndMoreDirPathComponents = dirPathComponents.Length > entryIterator.DirPathComponents.Length &&
                                              (entryIterator.DirPathComponents.Length == 0 || entryIterator.DirPathComponents.SequenceEqual(sameDirPathComponents));
        
        // return true, if writer has same or more path components and it's recursive
        return hasSameAndMoreDirPathComponents && _recursive;
    }

    public bool SupportsUaeMetadata => false;
}