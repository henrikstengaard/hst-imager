namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils;
using Models;
using Entry = Models.FileSystems.Entry;

public class FileSystemEntryWriter : IEntryWriter
{
    private readonly byte[] buffer;
    private readonly Media media;
    private readonly string[] pathComponents;
    private readonly IFileSystem fileSystem;
    private string[] currentPathComponents;
    private bool disposed;

    public FileSystemEntryWriter(Media media, IFileSystem fileSystem, string[] pathComponents)
    {
        this.buffer = new byte[4096];
        this.media = media;
        this.pathComponents = pathComponents;
        this.fileSystem = fileSystem;
        this.currentPathComponents = Array.Empty<string>();
    }

    public string MediaPath => this.media.Path;
    public string FileSystemPath => string.Empty;
    
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

        for (var i = 1; i <= fullPathComponents.Length; i++)
        {
            fileSystem.CreateDirectory(string.Join("\\", fullPathComponents.Take(i)));
        }

        currentPathComponents = fullPathComponents;

        return Task.CompletedTask;
    }

    public async Task WriteEntry(Entry entry, string[] entryPathComponents, Stream stream, bool skipAttributes)
    {
        var fullPathComponents = pathComponents.Concat(entryPathComponents).ToArray();
        
        var directoryChanged = currentPathComponents.Length != fullPathComponents.Length - 1;
        if (directoryChanged && fullPathComponents.Length > 1)
        {
            for (var i = 1; i < fullPathComponents.Length; i++)
            {
                fileSystem.CreateDirectory(string.Join("\\", fullPathComponents.Take(i)));
            }
            
            currentPathComponents = fullPathComponents.Take(fullPathComponents.Length - 1).ToArray();
        }

        var fullPath = string.Join("\\", fullPathComponents);

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