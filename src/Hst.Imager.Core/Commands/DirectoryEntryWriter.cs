using System.Runtime.Caching;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Hst.Core;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UaeMetadatas;
using Models.FileSystems;

public class DirectoryEntryWriter(string path) : IEntryWriter
{
    private readonly byte[] buffer = new byte[4096];
    private readonly IList<string> logs = new List<string>();
    private readonly MemoryCache cache = new($"{nameof(DirectoryEntryWriter)}_CACHE");
    private readonly DateTimeOffset cacheExpiration = DateTimeOffset.Now.AddMinutes(10);

    private string[] rootPathComponents = [];
    /// <summary>
    /// dir path components after initialized
    /// is only used when creating single file
    /// </summary>
    private string[] dirPathComponents = [];
    private string initializedDirPath = string.Empty;
    private bool lastPathComponentExist = true;
    private bool isInitialized = false;

    public string MediaPath => path;
    public string FileSystemPath => string.Empty;
    public UaeMetadata UaeMetadata { get; set; }
    
    public void Dispose()
    {
    }

    private async Task<string> GetEntryPath(Entry entry, string[] entryPathComponents) =>
        UaeMetadata == UaeMetadata.None
            ? CreateNormalEntryPath(entry, entryPathComponents)
            : await CreateUaeEntryPath(entry, entryPathComponents);

    private static string CreateNormalEntryPath(Entry entry, string[] entryPathComponents)
    {
        // has windows drive letter first, if entry has more than 1 entry path component (a directory is present)
        // and first entry path component is a valid Windows drive letter (like c:).
        // this is to allow Windows drive containing semicolon in name
        var hasWindowsDriveLetterFirst = entryPathComponents.Length > 1 &&
                                         OperatingSystem.IsWindows() &&
                                         Regexs.WindowsDriveRegex.IsMatch(entryPathComponents[0]);
        
        var entryPathComponentsNormalised = hasWindowsDriveLetterFirst
            ? new List<string>{entryPathComponents[0]}.Concat(entryPathComponents.Skip(1).Select(UaeMetadataHelper.CreateNormalFilename))
            : entryPathComponents.Select(UaeMetadataHelper.CreateNormalFilename);
        
        return Path.Combine(entryPathComponentsNormalised.ToArray());
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
            cacheKey = string.Join("|", uaeEntryPathComponents.Concat([entryPathComponent]));
        
            uaeCacheEntry = cache.Get(cacheKey) as UaeCacheEntry;
            
            // reuse, if already created
            if (uaeCacheEntry != null)
            {
                uaeEntryPathComponents.Add(entryPathComponent);

                uaeEntryPath = uaeCacheEntry.UaeEntryPath;
                
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
            var writeUaeMetadata = UaeMetadata != UaeMetadata.None;
            var fileName = await GetFileName(uaeEntryPath, entryPathComponent, 
                writeUaeMetadata);

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

    public Task<Result> Initialize()
    {
        rootPathComponents = path.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);
        
        var exisingPathComponents = new List<string>(10);

        lastPathComponentExist = true;

        for (var i = 0; i < rootPathComponents.Length; i++)
        {
            var dirPath = Path.Combine(rootPathComponents.Take(i + 1).ToArray());
            
            var dirExists = Directory.Exists(dirPath);
            var fileExists = File.Exists(dirPath);
            var exists = dirExists || fileExists;
            
            if (fileExists && i < rootPathComponents.Length - 1)
            {
                return Task.FromResult(new Result(new PathNotFoundError($"Path '{dirPath}' is a file and not a directory", dirPath)));
            }

            if (!exists)
            {
                if (i != rootPathComponents.Length - 1)
                {
                    return Task.FromResult(new Result(new PathNotFoundError($"Path not found '{dirPath}'", dirPath)));
                }
                
                lastPathComponentExist = false;
                
                break;
            }

            exisingPathComponents.Add(rootPathComponents[i]);
        }

        dirPathComponents = exisingPathComponents.ToArray();
        initializedDirPath = Path.Combine(dirPathComponents);

        isInitialized = true;

        return Task.FromResult(new Result());
    }

    /// <summary>
    /// Get name for directory or file. If name already has uae metadata applied,
    /// then that is read or created, if it doesn't exist.
    /// </summary>
    /// <param name="dirPath">Path to directory to get filename in.</param>
    /// <param name="amigaName"></param>
    /// <param name="useUaeMetadata"></param>
    /// <returns></returns>
    private async Task<string> GetFileName(string dirPath, string amigaName, bool useUaeMetadata)
    {
        if (!useUaeMetadata)
        {
            return UaeMetadataHelper.CreateNormalFilename(amigaName);
        }

        // return amiga name, if dir path is empty and amiga name is a valid Windows drive letter (c:)
        // this is to allow Windows drive containing semicolon in name
        if (string.IsNullOrEmpty(dirPath) &&
            OperatingSystem.IsWindows() &&
            Regexs.WindowsDriveRegex.IsMatch(amigaName))
        {
            return amigaName;
        }
        
        var uaeMetadataDir = string.IsNullOrEmpty(dirPath) ? Directory.GetCurrentDirectory() : dirPath;

        var uaeMetadataNode = (await UaeMetadataHelper.ReadUaeMetadataNodes(UaeMetadata, uaeMetadataDir, amigaName))
            .FirstOrDefault(x => x.AmigaName.Equals(amigaName, StringComparison.OrdinalIgnoreCase));

        var normalName = uaeMetadataNode == null
            ? UaeMetadataHelper.CreateUaeMetadataFileName(UaeMetadata, uaeMetadataDir, amigaName)
            : uaeMetadataNode.NormalName;

        var requiresUaeMetadata = !amigaName.Equals(normalName);

        if (requiresUaeMetadata && uaeMetadataNode == null)
        {
            await UaeMetadataHelper.WriteUaeMetadata(UaeMetadata, uaeMetadataDir, amigaName, normalName);
        }

        return normalName;
    }

    public async Task<Result> CreateDirectory(Entry entry, string[] entryPathComponents, bool skipAttributes,
        bool isSingleFileEntry)
    {
        if (!isInitialized)
        {
            return new Result(new Error("DirectoryEntryWriter is not initialized."));
        }

        // return, if it is a single file entry or if there are no entry path components.
        // for single file entries, the create file method will create the directory.
        if (isSingleFileEntry || entryPathComponents.Length == 0)
        {
            return new Result();
        }

        var fullPathComponents = PathComponentHelper.GetFullPathComponents(entry.Type, entryPathComponents,
            rootPathComponents, lastPathComponentExist, isSingleFileEntry);

        var fullPath = await GetEntryPath(entry, fullPathComponents);

        if (!lastPathComponentExist)
        {
            return new Result(new PathNotFoundError($"Path not found '{path}'", path));
        }

        int? protectionBits = null;
        if (int.TryParse(GetProperty(entry.Properties, Constants.EntryPropertyNames.ProtectionBits),
                out var parsedProtectionBits))
        {
            protectionBits = parsedProtectionBits;
        }
            
        var comment = GetProperty(entry.Properties, Constants.EntryPropertyNames.Comment);

        var entryName = entryPathComponents[^1];
        var useUaeMetadataProperties = UaeMetadata != UaeMetadata.None &&
                                       UaeMetadataHelper.RequiresUaeMetadataProperties(protectionBits, comment);
        var useUaeMetadataName = UaeMetadata != UaeMetadata.None &&
                                 UaeMetadataHelper.RequiresUaeMetadataFileName(UaeMetadata, entryName);
        var writeUaeMetadata = useUaeMetadataProperties || useUaeMetadataName;

        var dirPath = Path.GetDirectoryName(fullPath) ?? string.Empty;

        var name = await GetFileName(dirPath, entryName, writeUaeMetadata);

        if (writeUaeMetadata)
        {
            await UaeMetadataHelper.WriteUaeMetadata(UaeMetadata, string.IsNullOrEmpty(dirPath) ? "." : dirPath,
                entryName, name, protectionBits, entry.Date ?? DateTime.Now, comment);
        }

        fullPath = Path.Combine(dirPath, name);

        if (!string.IsNullOrEmpty(fullPath) && !Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        if (UaeMetadata == UaeMetadata.None)
        {
            return new Result();
        }

        var uaeCacheEntry = new UaeCacheEntry
        {
            EntryPathComponent = entryName,
            UaeEntryPath = fullPath
        };

        var cacheKey = string.Join("|", fullPathComponents);

        cache.Add(cacheKey, uaeCacheEntry, cacheExpiration);

        return new Result();
    }

    public async Task<Result> CreateFile(Entry entry, string[] entryPathComponents, Stream stream, bool skipAttributes,
        bool isSingleFileEntry)
    {
        if (!isInitialized)
        {
            return new Result(new Error("DirectoryEntryWriter is not initialized."));
        }
        
        var fullPathComponents = PathComponentHelper.GetFullPathComponents(entry.Type, entryPathComponents,
            rootPathComponents, lastPathComponentExist, isSingleFileEntry);
        
        var fullPath = await GetEntryPath(entry, fullPathComponents);

        if (!int.TryParse(GetProperty(entry.Properties, Constants.EntryPropertyNames.ProtectionBits), out var protectionBits))
        {
            protectionBits = 0;
        }
            
        var comment = GetProperty(entry.Properties, Constants.EntryPropertyNames.Comment);

        var entryName = fullPathComponents[^1];
        var requiresUaeMetadataProperties = UaeMetadata != UaeMetadata.None &&
                                            UaeMetadataHelper.RequiresUaeMetadataProperties(protectionBits, comment);
        var requiresUaeMetadataFileName = UaeMetadata != UaeMetadata.None && 
                                          UaeMetadataHelper.RequiresUaeMetadataFileName(UaeMetadata, entryName);
        var writeUaeMetadata = requiresUaeMetadataProperties || requiresUaeMetadataFileName;

        var dirPath = Path.GetDirectoryName(fullPath);

        // if the entry is a single file entry, then create the directory if it does not exist.
        if (isSingleFileEntry &&
            !string.IsNullOrEmpty(dirPath) &&
            !Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }
        
        var fileName = await GetFileName(dirPath, entryName, writeUaeMetadata);

        var filePath = string.IsNullOrEmpty(dirPath)
            ? fileName
            : Path.Combine(dirPath, fileName);

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
            return new Result();
        }

        switch (UaeMetadata)
        {
            case UaeMetadata.UaeFsDb:
                await UaeMetadataHelper.WriteUaeFsDb(dirPath, entryName, fileName, protectionBits, comment);
                break;
            case UaeMetadata.UaeMetafile:
                await UaeMetadataHelper.WriteUaeMetafile(dirPath, fileName, protectionBits, entry.Date ?? DateTime.Now, comment);
                break;
        }

        return new Result();
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
