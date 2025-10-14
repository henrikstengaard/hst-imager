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

public class FileSystemEntryWriter(Media media, IFileSystem fileSystem, string[] rootPathComponents,
    bool createDirectory) : IEntryWriter
{
    private readonly byte[] buffer = new byte[4096];
    private readonly IMediaPath mediaPath = PathComponents.MediaPath.GenericMediaPath;
    private bool disposed;
    private readonly HashSet<string> dirPathsCreated = new();

    /// <summary>
    /// root path components after initialized
    /// </summary>
    private string[] dirPathComponents = [];

    private bool lastPathComponentExist = true;
    private bool isInitialized = false;

    public string MediaPath => media.Path;
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

            var dirs = fileSystem.GetDirectories("").ToList();
            
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
            
            exisingPathComponents.Add(rootPathComponents[i]);
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
            rootPathComponents, lastPathComponentExist, isSingleFileEntry);

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

        CreateFileSystemDirectory(fullPathComponents);

        return Task.FromResult(new Result());
    }

    private void CreateFileSystemDirectory(string[] pathComponents)
    {
        for (var i = 1; i <= pathComponents.Length; i++)
        {
            var dirPath = mediaPath.Join(pathComponents.Take(i).ToArray()).ToLower();

            if (dirPathsCreated.Contains(dirPath))
            {
                continue;
            }

            fileSystem.CreateDirectory(mediaPath.Join(pathComponents.Take(i).ToArray()));

            dirPathsCreated.Add(dirPath);
        }
    }

    public async Task<Result> CreateFile(Entry entry, string[] entryPathComponents, Stream stream, bool skipAttributes,
        bool isSingleFileEntry)
    {
        if (!isInitialized)
        {
            return new Result(new Error("FileSystemEntryWriter is not initialized."));
        }

        var fullPathComponents = PathComponentHelper.GetFullPathComponents(entry.Type, entryPathComponents,
            rootPathComponents, lastPathComponentExist, isSingleFileEntry);

        var fullPath = mediaPath.Join(fullPathComponents);

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
    
    public IEntryIterator CreateEntryIterator(string rootPath, bool recursive) => null;
}