using System.Runtime.Caching;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Hst.Core.Extensions;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Imager.Core.UaeMetadatas;
using Models.FileSystems;

public class DirectoryEntryWriter : IEntryWriter
{
    private readonly string path;
    private readonly byte[] buffer;
    private readonly IList<string> logs;
    private readonly MemoryCache cache;
    private readonly DateTimeOffset cacheExpiration;

    public DirectoryEntryWriter(string path)
    {
        this.path = path;
        this.buffer = new byte[4096];
        this.logs = new List<string>();
        this.cache = new MemoryCache($"{nameof(DirectoryEntryWriter)}_CACHE");
        this.cacheExpiration = DateTimeOffset.Now.AddMinutes(10);
    }

    public string MediaPath => this.path;
    public string FileSystemPath => string.Empty;
    public UaeMetadata UaeMetadata { get; set; }
    
    public void Dispose()
    {
    }

    private async Task<string> GetEntryPath(Entry entry, string[] entryPathComponents) =>
        UaeMetadata == UaeMetadata.None
            ? CreateNormalEntryPath(entry, entryPathComponents)
            : await CreateUaeEntryPath(entry, entryPathComponents);

    private string CreateNormalEntryPath(Entry entry, string[] entryPathComponents)
    {
        return Path.Combine(entryPathComponents.Select(UaeMetadataHelper.CreateNormalFilename).ToArray());
    }
    
    private async Task<string> CreateUaeEntryPath(Entry entry, string[] entryPathComponents)
    {
        if (entryPathComponents.Length == 0)
        {
            return string.Empty;
        }

        var cacheKey = string.Join("|", entryPathComponents);
        
        var uaeCacheEntry = cache.Get(cacheKey) as UaeCacheEntry;

        if (uaeCacheEntry != null)
        {
            return uaeCacheEntry.UaeEntryPath;
        }

        var uaeEntryPath = string.Empty;

        var uaeEntryPathComponents = new List<string>();
        
        foreach (var entryPathComponent in entryPathComponents)
        {
            cacheKey = string.Join("|", uaeEntryPathComponents.Concat(new []{entryPathComponent}));
        
            uaeCacheEntry = cache.Get(cacheKey) as UaeCacheEntry;
            
            // reuse, if already created
            if (uaeCacheEntry != null)
            {
                uaeEntryPathComponents.Add(uaeCacheEntry.EntryPathComponent);

                uaeEntryPath = string.IsNullOrWhiteSpace(uaeEntryPath)
                    ? entryPathComponent
                    : Path.Combine(uaeEntryPath, entryPathComponent);
                
                continue;
            }

            // if not use uae metadata, then add to cache and continue
            if (!UaeMetadataHelper.RequiresUaeMetadataFileName(UaeMetadata, entryPathComponent))
            {
                uaeEntryPathComponents.Add(entryPathComponent);
                
                cacheKey = string.Join("|", uaeEntryPathComponents);

                uaeEntryPath = string.IsNullOrWhiteSpace(uaeEntryPath)
                    ? entryPathComponent
                    : Path.Combine(uaeEntryPath, entryPathComponent);

                uaeCacheEntry = new UaeCacheEntry
                {
                    EntryPathComponent = entryPathComponent,
                    UaeEntryPath = uaeEntryPath
                };

                cache.Add(cacheKey, uaeCacheEntry, cacheExpiration);
                
                continue;
            }
            
            // create new entry
            var fileName = UaeMetadataHelper.CreateUaeMetadataFileName(UaeMetadata, uaeEntryPath, entryPathComponent);

            await UaeMetadataHelper.WriteUaeMetadata(UaeMetadata, Path.Combine(path, uaeEntryPath), entryPathComponent, 
                fileName, 0, DateTime.Now, string.Empty);

            uaeEntryPathComponents.Add(entryPathComponent);
                
            cacheKey = string.Join("|", uaeEntryPathComponents);
            
            uaeEntryPath = string.IsNullOrWhiteSpace(uaeEntryPath)
                ? fileName
                : Path.Combine(uaeEntryPath, fileName);

            uaeCacheEntry = new UaeCacheEntry
            {
                EntryPathComponent = entryPathComponent,
                UaeEntryPath = uaeEntryPath
            };
            
            cache.Add(cacheKey, uaeCacheEntry, cacheExpiration);
        }
        
        return uaeEntryPath;
    }

    private async Task<string> ReadNormalNameFromUaeFsDb(string path, string amigaName)
    {
        var uaeFsDbPath = Path.Combine(path, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);

        if (!File.Exists(uaeFsDbPath))
        {
            return null;
        }

        await using var stream = new FileStream(uaeFsDbPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read);

        var nodes = await UaeFsDbReader.ReadFromStream(stream);

        return nodes.FirstOrDefault(node => node.AmigaName.Equals(amigaName))?.NormalName;
    }

    public async Task CreateDirectory(Entry entry, string[] entryPathComponents, bool skipAttributes)
    {
        if (entryPathComponents.Length == 0)
        {
            return;
        }

        var entryPath = await GetEntryPath(entry, entryPathComponents.Take(entryPathComponents.Length - 1).ToArray());

        var fullPath = Path.Combine(path, entryPath);
        if (!string.IsNullOrEmpty(fullPath) && !Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        if (!int.TryParse(GetProperty(entry.Properties, Constants.EntryPropertyNames.ProtectionBits), out var protectionBits))
        {
            protectionBits = 0;
        }
            
        var comment = GetProperty(entry.Properties, Constants.EntryPropertyNames.Comment);

        var entryName = entryPathComponents[^1];
        var useUaeMetadataProperties = UaeMetadata != UaeMetadata.None
            ? UaeMetadataHelper.RequiresUaeMetadataProperties(protectionBits, comment)
            : false;
        var useUaeMetadataName = UaeMetadata != UaeMetadata.None
            ? UaeMetadataHelper.RequiresUaeMetadataFileName(UaeMetadata, entryName)
            : false;
        var writeUaeMetadata = useUaeMetadataProperties || useUaeMetadataName;

        var name = UaeMetadata != UaeMetadata.None
            ? await ReadNormalNameFromUaeFsDb(fullPath, entryName)
            : null;

        if (string.IsNullOrEmpty(name))
        {
            name = useUaeMetadataName
                ? UaeMetadataHelper.CreateUaeMetadataFileName(UaeMetadata, fullPath, entryName)
                : UaeMetadataHelper.CreateNormalFilename(entryName);
        }
        else
        {
            writeUaeMetadata = false;
        }

        var uaeEntryPath = Path.Combine(entryPath, name);
        
        if (writeUaeMetadata)
        {
            switch (UaeMetadata)
            {
                case UaeMetadata.UaeFsDb:
                    await UaeMetadataHelper.WriteUaeFsDb(fullPath, entryName, name, protectionBits, comment);
                    break;
                case UaeMetadata.UaeMetafile:
                    await UaeMetadataHelper.WriteUaeMetafile(fullPath, name, protectionBits, entry.Date ?? DateTime.Now, comment);
                    break;
            }
        }
        
        fullPath = Path.Combine(path, uaeEntryPath);
        if (!string.IsNullOrEmpty(fullPath) && !Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        if (UaeMetadata == UaeMetadata.None)
        {
            return;
        }

        var uaeCacheEntry = new UaeCacheEntry
        {
            EntryPathComponent = entryName,
            UaeEntryPath = uaeEntryPath
        };

        var cacheKey = string.Join("|", entryPathComponents);

        cache.Add(cacheKey, uaeCacheEntry, cacheExpiration);
    }

    public async Task WriteEntry(Entry entry, string[] entryPathComponents, Stream stream, bool skipAttributes)
    {
        var dirPath = await GetEntryPath(entry, entryPathComponents.Take(entryPathComponents.Length - 1).ToArray());

        var fullPath = Path.Combine(path, dirPath);
        if (!string.IsNullOrEmpty(fullPath) && !Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        if (!int.TryParse(GetProperty(entry.Properties, Core.Constants.EntryPropertyNames.ProtectionBits), out var protectionBits))
        {
            protectionBits = 0;
        }
            
        var comment = GetProperty(entry.Properties, Core.Constants.EntryPropertyNames.Comment);

        var entryName = entryPathComponents[^1];
        var requiresUaeMetadataProperties = UaeMetadata != UaeMetadata.None
            ? UaeMetadataHelper.RequiresUaeMetadataProperties(protectionBits, comment)
            : false;
        var requiresUaeMetadataFileName = UaeMetadata != UaeMetadata.None
            ? UaeMetadataHelper.RequiresUaeMetadataFileName(UaeMetadata, entryName)
            : false;
        var writeUaeMetadata = requiresUaeMetadataProperties || requiresUaeMetadataFileName;
        
        var fileName = requiresUaeMetadataFileName
            ? UaeMetadataHelper.CreateUaeMetadataFileName(UaeMetadata, fullPath, entryName)
            : UaeMetadataHelper.CreateNormalFilename(entryName);

        var filePath = Path.Combine(fullPath, fileName);

        await using var fileStream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite);

        int bytesRead;
        do
        {
            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            await fileStream.WriteAsync(buffer, 0, bytesRead);
        } while
            (bytesRead !=
             0); // continue until bytes read is 0. reads from zip streams can return bytes between 0 to buffer length. 

        fileStream.Close();
        await fileStream.DisposeAsync();

        if (entry.Date.HasValue)
        {
            File.SetCreationTime(filePath, entry.Date.Value);
            File.SetLastWriteTime(filePath, entry.Date.Value);
            File.SetLastAccessTime(filePath, entry.Date.Value);
        }
        
        if (!writeUaeMetadata)
        {
            return;
        }

        switch (UaeMetadata)
        {
            case UaeMetadata.UaeFsDb:
                await UaeMetadataHelper.WriteUaeFsDb(fullPath, entryName, fileName, protectionBits, comment);
                break;
            case UaeMetadata.UaeMetafile:
                await UaeMetadataHelper.WriteUaeMetafile(fullPath, fileName, protectionBits, entry.Date ?? DateTime.Now, comment);
                break;
        }
    }

    private static string GetProperty(IDictionary<string, string> properties, string name) => 
        properties.TryGetValue(name, out var value) ? value : null;

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
        return this.logs.Count == 0
            ? new List<string>()
            : new[]
            {
                string.Empty, "Following files were renamed to due invalid characters or conflicts with Windows OS reserved filenames:"
            }.Concat(logs);
    }

    public IEntryIterator CreateEntryIterator(string rootPath, bool recursive) => null;
}
