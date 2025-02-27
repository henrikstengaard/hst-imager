namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils;
using Hst.Imager.Core.PathComponents;
using Hst.Imager.Core.UaeMetadatas;
using Models;
using Entry = Models.FileSystems.Entry;

public class FileSystemEntryWriter : IEntryWriter
{
    private readonly byte[] buffer;
    private readonly Media media;
    private readonly IMediaPath mediaPath;
    private readonly string[] pathComponents;
    private readonly IFileSystem fileSystem;
    private string currentDirPath;
    private bool disposed;
    private readonly HashSet<string> dirPathsCreated;

    public FileSystemEntryWriter(Media media, IFileSystem fileSystem, string[] pathComponents)
    {
        this.buffer = new byte[4096];
        this.media = media;
        this.mediaPath = PathComponents.MediaPath.GenericMediaPath;
        this.pathComponents = pathComponents;
        this.fileSystem = fileSystem;
        this.currentDirPath = string.Empty;

        dirPathsCreated = new HashSet<string>();
    }

    public string MediaPath => this.media.Path;
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
    
    public Task CreateDirectory(Entry entry, string[] entryPathComponents, bool skipAttributes)
    {
        var fullPathComponents = pathComponents.Concat(entryPathComponents).ToArray();

        CreateFileSystemDirectory(fullPathComponents);

        var dirPath = mediaPath.Join(fullPathComponents);
        currentDirPath = dirPath;

        return Task.CompletedTask;
    }

    private void CreateFileSystemDirectory(string[] pathComponents)
    {
        for (var i = 1; i <= pathComponents.Length; i++)
        {
            var dirPathComponents = pathComponents.Take(i);
            var dirPath = mediaPath.Join(dirPathComponents.ToArray()).ToLower();

            if (dirPathsCreated.Contains(dirPath))
            {
                continue;
            }

            fileSystem.CreateDirectory(mediaPath.Join(pathComponents.Take(i).ToArray()));

            dirPathsCreated.Add(dirPath);
        }
    }

    public async Task WriteEntry(Entry entry, string[] entryPathComponents, Stream stream, bool skipAttributes)
    {
        var fullPathComponents = pathComponents.Concat(entryPathComponents).ToArray();

        var dirPathComponents = fullPathComponents.Length <= 1 
            ? Array.Empty<string>()
            : fullPathComponents.Take(fullPathComponents.Length - 1).ToArray();
        var dirPath = mediaPath.Join(dirPathComponents);

        if (dirPathComponents.Length > 0 && !currentDirPath.Equals(dirPath))
        {
            CreateFileSystemDirectory(dirPathComponents);

            currentDirPath = dirPath;
        }

        var fullPath = mediaPath.Join(fullPathComponents);

        await using var entryStream = fileSystem.OpenFile(fullPath, FileMode.OpenOrCreate);
        int bytesRead;
        do
        {
            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            await entryStream.WriteAsync(buffer, 0, bytesRead);
        } while (bytesRead != 0);
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