namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Entry = Models.FileSystems.Entry;
using FileMode = Amiga.FileSystems.FileMode;

public class AmigaVolumeEntryWriter : IEntryWriter
{
    private readonly byte[] buffer;
    private readonly Stream stream;
    private readonly string path;
    private readonly IFileSystemVolume fileSystemVolume;
    private string currentPath;
    private bool disposed;

    public AmigaVolumeEntryWriter(Stream stream, string path, IFileSystemVolume fileSystemVolume)
    {
        this.buffer = new byte[4096];
        this.stream = stream;
        this.path = path;
        this.fileSystemVolume = fileSystemVolume;
        this.currentPath = null;
    }
    
    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            stream.Dispose();
        }

        disposed = true;
    }

    public void Dispose() => Dispose(true);
    
    public async Task CreateDirectory(Entry entry)
    {
        var dirPath = Path.GetDirectoryName(entry.Path) ?? "/";
        var dirName = Path.GetFileName(entry.Path);
        IEnumerable<Hst.Amiga.FileSystems.Entry> entries;

        if (currentPath != dirPath)
        {
            await fileSystemVolume.ChangeDirectory("/");
            var parts = dirPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
    
            foreach (var part in parts)
            {
                entries = (await fileSystemVolume.ListEntries()).ToList();

                if (!entries.Any(x => x.Name.Equals(part, StringComparison.OrdinalIgnoreCase)))
                {
                    await fileSystemVolume.CreateDirectory(part);
                }

                await fileSystemVolume.ChangeDirectory(part);
            }

            currentPath = dirPath;
        }
        
        entries = (await fileSystemVolume.ListEntries()).ToList();
        if (entries.Any(x => x.Name.Equals(dirName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        
        await fileSystemVolume.CreateDirectory(dirName);
        
        await fileSystemVolume.SetProtectionBits(dirName, GetProtectionBits(entry.Attributes));
        
        if (entry.Date.HasValue)
        {
            await fileSystemVolume.SetDate(dirName, entry.Date.Value);
        }
        
        if (entry.Properties.ContainsKey("Comment") && !string.IsNullOrWhiteSpace(entry.Properties["Comment"]))
        {
            await fileSystemVolume.SetComment(dirName, entry.Properties["Comment"]);
        }
    }

    public async Task WriteEntry(Entry entry, Stream stream)
    {
        var entryPath = Path.Combine(path, entry.Path).Replace("\\", "/");
        var dirPath = Path.GetDirectoryName(entryPath) ?? string.Empty;
        var fileName = Path.GetFileName(entryPath);
        
        if (currentPath != dirPath)
        {
            await fileSystemVolume.ChangeDirectory("/");

            if (!string.IsNullOrEmpty(dirPath))
            {
                var parts = dirPath.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);
    
                for (var i = 0; i < parts.Length; i++)
                {
                    var entries = (await fileSystemVolume.ListEntries()).ToList();

                    if (!entries.Any(x => x.Name.Equals(parts[i], StringComparison.OrdinalIgnoreCase)))
                    {
                        await fileSystemVolume.CreateDirectory(parts[i]);
                    }

                    await fileSystemVolume.ChangeDirectory(parts[i]);
                }
            }
            
            currentPath = dirPath;
        }

        await fileSystemVolume.CreateFile(fileName);
        await using var entryStream = await fileSystemVolume.OpenFile(fileName, FileMode.Append);
        
        int bytesRead;
        do
        {
            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            await entryStream.WriteAsync(buffer, 0, bytesRead);
        } while (bytesRead == buffer.Length);

        await fileSystemVolume.SetProtectionBits(fileName, GetProtectionBits(entry.Attributes));
        
        if (entry.Date.HasValue)
        {
            Console.WriteLine($"set date '{entry.Date.Value}'");
            await fileSystemVolume.SetDate(fileName, entry.Date.Value);
        }

        if (entry.Properties.ContainsKey("Comment") && !string.IsNullOrWhiteSpace(entry.Properties["Comment"]))
        {
            await fileSystemVolume.SetComment(fileName, entry.Properties["Comment"]);
        }
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