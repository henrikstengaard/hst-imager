namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils.Fat;
using Models;
using Entry = Models.FileSystems.Entry;

public class FatEntryWriter : IEntryWriter
{
    private readonly byte[] buffer;
    private readonly Media media;
    private readonly string[] pathComponents;
    private readonly FatFileSystem fatFileSystem;
    private bool disposed;

    public FatEntryWriter(Media media, FatFileSystem fatFileSystem, string[] pathComponents)
    {
        this.buffer = new byte[4096];
        this.media = media;
        this.pathComponents = pathComponents;
        this.fatFileSystem = fatFileSystem;
    }

    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            fatFileSystem.Dispose();
            media.Stream.Flush();
            media.Dispose();
        }

        disposed = true;
    }

    public void Dispose() => Dispose(true);
    
    public Task CreateDirectory(Entry entry, string[] entryPathComponents)
    {
        var fullPathComponents = pathComponents.Concat(entryPathComponents).ToArray();

        for (var i = 1; i <= fullPathComponents.Length; i++)
        {
            fatFileSystem.CreateDirectory(string.Join("\\", fullPathComponents.Take(i)));
        }

        return Task.CompletedTask;
    }

    public async Task WriteEntry(Entry entry, string[] entryPathComponents, Stream stream)
    {
        var fullPathComponents = pathComponents.Concat(entryPathComponents).ToArray();
        var fullPath = string.Join("\\", fullPathComponents);

        await using var entryStream = fatFileSystem.OpenFile(fullPath, FileMode.OpenOrCreate);
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
}