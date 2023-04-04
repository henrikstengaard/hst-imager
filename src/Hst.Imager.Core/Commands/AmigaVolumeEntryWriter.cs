namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Models;
using Entry = Models.FileSystems.Entry;
using FileMode = Amiga.FileSystems.FileMode;

public class AmigaVolumeEntryWriter : IEntryWriter
{
    private readonly byte[] buffer;
    private readonly Media media;
    private readonly string[] pathComponents;
    private readonly IFileSystemVolume fileSystemVolume;
    private string[] currentPathComponents;
    private bool disposed;
    private int writeOperations;

    public AmigaVolumeEntryWriter(Media media, string[] pathComponents, IFileSystemVolume fileSystemVolume)
    {
        this.buffer = new byte[4096];
        this.media = media;
        this.pathComponents = pathComponents;
        this.fileSystemVolume = fileSystemVolume;
        this.currentPathComponents = Array.Empty<string>();
        this.writeOperations = 0;
    }
    
    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            fileSystemVolume.Flush().GetAwaiter().GetResult();
            media.Stream.Flush();
            media.Dispose();
        }

        disposed = true;
    }

    public void Dispose() => Dispose(true);

    public async Task CreateDirectory(Entry entry, string[] entryPathComponents)
    {
        await fileSystemVolume.ChangeDirectory("/");

        var fullPathComponents = pathComponents.Concat(entryPathComponents).ToArray();

        for (var i = 0; i < fullPathComponents.Length; i++)
        {
            var part = fullPathComponents[i];
            
            IEnumerable<Hst.Amiga.FileSystems.Entry> entries = (await fileSystemVolume.ListEntries()).ToList();

            var dirEntry = entries.FirstOrDefault(x =>
                x.Name.Equals(part, StringComparison.OrdinalIgnoreCase) && x.Type == EntryType.Dir);
            
            if (dirEntry == null)
            {
                await fileSystemVolume.CreateDirectory(part);
            }

            if (i == fullPathComponents.Length - 1)
            {
                await fileSystemVolume.SetProtectionBits(part, GetProtectionBits(entry.Attributes));
        
                if (entry.Date.HasValue)
                {
                    await fileSystemVolume.SetDate(part, entry.Date.Value);
                }
        
                if (entry.Properties.ContainsKey("Comment") && !string.IsNullOrWhiteSpace(entry.Properties["Comment"]))
                {
                    await fileSystemVolume.SetComment(part, entry.Properties["Comment"]);
                }
            }

            await fileSystemVolume.ChangeDirectory(part);
            writeOperations++;
        }

        currentPathComponents = fullPathComponents;
        
        await Flush();
    }

    public async Task WriteEntry(Entry entry, string[] entryPathComponents, Stream stream)
    {
        var fullPathComponents = pathComponents.Concat(entryPathComponents).ToArray();
        var fileName = fullPathComponents[^1];

        var directoryChanged = currentPathComponents.Length != fullPathComponents.Length - 1;
        if (!directoryChanged)
        {
            for (var i = fullPathComponents.Length - 2; i >= 0; i--)
            {
                if (currentPathComponents[i] == fullPathComponents[i])
                {
                    continue;
                }
                directoryChanged = true;
                break;
            }
        }

        if (directoryChanged)
        {
            await fileSystemVolume.ChangeDirectory("/");

            for (var i = 0; i < fullPathComponents.Length - 1; i++)
            {
                var entries = (await fileSystemVolume.ListEntries()).ToList();

                if (!entries.Any(x => x.Name.Equals(fullPathComponents[i], StringComparison.OrdinalIgnoreCase)))
                {
                    await fileSystemVolume.CreateDirectory(fullPathComponents[i]);
                }

                await fileSystemVolume.ChangeDirectory(fullPathComponents[i]);
                
            }

            currentPathComponents = fullPathComponents.Take(fullPathComponents.Length - 1).ToArray();
        }

        await fileSystemVolume.CreateFile(fileName, true, true);

        await using (var entryStream = await fileSystemVolume.OpenFile(fileName, FileMode.Append, true))
        {
            int bytesRead;
            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                await entryStream.WriteAsync(buffer, 0, bytesRead);
            } while (bytesRead != 0); // continue until bytes read is 0. reads from zip streams can return bytes between 0 to buffer length. 
        }

        await fileSystemVolume.SetProtectionBits(fileName, GetProtectionBits(entry.Attributes));

        if (entry.Properties.ContainsKey("Comment") && !string.IsNullOrWhiteSpace(entry.Properties["Comment"]))
        {
            await fileSystemVolume.SetComment(fileName, entry.Properties["Comment"]);
        }
        
        if (entry.Date.HasValue)
        {
            await fileSystemVolume.SetDate(fileName, entry.Date.Value);
        }

        writeOperations++;
        await Flush();
    }

    public IEnumerable<string> GetDebugLogs()
    {
        return fileSystemVolume.GetStatus().ToList();
    }

    public IEnumerable<string> GetLogs()
    {
        return new List<string>();
    }

    private async Task Flush()
    {
        if (this.writeOperations <= 100)
        {
            return;
        }

        this.writeOperations = 0;
        await this.fileSystemVolume.Flush();
    }

    private ProtectionBits GetProtectionBits(string attributes)
    {
        if (string.IsNullOrWhiteSpace(attributes))
        {
            return ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable | ProtectionBits.Delete;
        }
        
        var protectionBits = ProtectionBits.None;

        foreach (var attribute in attributes)
        {
            switch (attribute)
            {
                case 'H':
                    protectionBits |= ProtectionBits.HeldResident;
                    break;
                case 'S':
                    protectionBits |= ProtectionBits.Script;
                    break;
                case 'P':
                    protectionBits |= ProtectionBits.Pure;
                    break;
                case 'A':
                    protectionBits |= ProtectionBits.Archive;
                    break;
                case 'R':
                    protectionBits |= ProtectionBits.Read;
                    break;
                case 'W':
                    protectionBits |= ProtectionBits.Write;
                    break;
                case 'E':
                    protectionBits |= ProtectionBits.Executable;
                    break;
                case 'D':
                    protectionBits |= ProtectionBits.Delete;
                    break;
            }
        }

        return protectionBits;
    }
}