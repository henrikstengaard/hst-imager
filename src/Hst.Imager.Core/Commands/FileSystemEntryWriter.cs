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
/// <param name="media">Media mounted.</param>
/// <param name="partitionTableType">Partition table type mounted.</param>
/// <param name="partitionNumber">Partition number mounted.</param>
/// <param name="fileSystem">File system mounted to write entries.</param>
/// <param name="rootPathComponents">Root path components.</param>
/// <param name="recursive">Recursive creating directories and files.</param>
/// <param name="createDirectory">Create directory for root path components, if it doesn't exist.</param>
/// <param name="forceOverwrite">Force overwriting any existing files.</param>
public class FileSystemEntryWriter(Media media, PartitionTableType partitionTableType, int partitionNumber,
    IFileSystem fileSystem, string[] rootPathComponents, bool recursive, bool createDirectory, bool forceOverwrite) : IEntryWriter
{
    private readonly byte[] buffer = new byte[4096];
    private readonly IMediaPath mediaPath = PathComponents.MediaPath.GenericMediaPath;
    private bool disposed;
    private readonly HashSet<string> dirPathsCreated = new();

    /// <summary>
    /// Directory path components of root path components that exist.
    /// </summary>
    private string[] dirPathComponents = [];

    private bool lastPathComponentExist = true;
    private Models.FileSystems.EntryType lastPathComponentEntryType = Models.FileSystems.EntryType.Dir;
    private bool isInitialized = false;

    public string MediaPath => media.Path;
    public PartitionTableType PartitionTableType => partitionTableType;
    public int PartitionNumber => partitionNumber;
    public string FileSystemPath => string.Empty;
    public UaeMetadata UaeMetadata { get; set; }
    
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
            media.Stream.Flush();
            media.Dispose();
        }

        disposed = true;
    }

    public void Dispose() => Dispose(true);

    public Task<Result> Initialize()
    {
        var exisingPathComponents = new List<string>(10);
            
        lastPathComponentExist = true;

        for (var i = 0; i < rootPathComponents.Length; i++)
        {
            var dirPath = mediaPath.Join(exisingPathComponents.Concat([rootPathComponents[i]]).ToArray());

            var fileSystemInfo = fileSystem.GetFileSystemInfo(dirPath);
            
            var nextDirPath = string.Join("/", rootPathComponents.Take(i + 1));

            if (!fileSystemInfo.Exists && fileSystemInfo is DiscFileInfo && i < rootPathComponents.Length - 1)
            {
                return Task.FromResult(new Result(new PathNotFoundError(
                    $"Path '{nextDirPath}' is a file and not a directory", nextDirPath)));
            }
            
            if (!fileSystemInfo.Exists)
            {
                if (!createDirectory)
                {
                    if (i != rootPathComponents.Length - 1)
                    {
                        return Task.FromResult(new Result(new PathNotFoundError(
                            $"Path not found '{nextDirPath}'", nextDirPath)));
                    }
                
                    lastPathComponentExist = false;

                    break;
                }
                
                fileSystem.CreateDirectory(dirPath);
            }
            else
            {
                if (i == rootPathComponents.Length - 1)
                {
                    lastPathComponentEntryType = fileSystem.DirectoryExists(fileSystemInfo.FullName)
                        ? Models.FileSystems.EntryType.Dir : Models.FileSystems.EntryType.File;

                    if (!fileSystem.DirectoryExists(fileSystemInfo.FullName))
                    {
                        break;
                    }
                }
            }
            
            exisingPathComponents.Add(rootPathComponents[i]);
        }

        if (recursive && !lastPathComponentExist)
        {
            var path = string.Join("/", rootPathComponents);
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
            lastPathComponentEntryType, rootPathComponents, lastPathComponentExist, isSingleFileEntry);

        if (fullPathComponents.Length == 0)
        {
            return Task.FromResult(new Result());
        }
        
        var requiredPathComponentsToExist = isSingleFileEntry ? dirPathComponents : rootPathComponents;

        var path = mediaPath.Join(requiredPathComponentsToExist);
        if (!fileSystem.Exists(path))
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

            if (fileSystem.FileExists(path))
            {
                return new Result(new Error($"Create directory path '{path}' failed. Path already exists as a file!"));
            }
            
            fileSystem.CreateDirectory(path);

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
            lastPathComponentEntryType, rootPathComponents, lastPathComponentExist, isSingleFileEntry);

        var fullPath = mediaPath.Join(fullPathComponents);

        if (!forceOverwrite && fileSystem.FileExists(fullPath))
        {
            return new Result(new FileExistsError($"File already exists '{fullPath}'"));
        }

        await using var entryStream = fileSystem.OpenFile(fullPath, FileMode.OpenOrCreate);
        int bytesRead;
        do
        {
            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            await entryStream.WriteAsync(buffer, 0, bytesRead);
        } while (bytesRead != 0);
        
        return new Result();
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
        return new FileSystemEntryIterator(media, partitionTableType, partitionNumber, fileSystem, rootPathComponents,
            recursive);
    }

    private bool IsSameMediaAndPartition(IEntryIterator entryIterator) =>
        entryIterator.Media != null && media.Equals(entryIterator.Media) &&
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
        
        var lastPathComponent = rootPathComponents.Length > 0 ? rootPathComponents[^1] : string.Empty;

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
        if (!recursive && entryIterator.IsSingleFileEntryNext)
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
        return hasSameAndMoreDirPathComponents && recursive;
    }

    public bool SupportsUaeMetadata => false;
}