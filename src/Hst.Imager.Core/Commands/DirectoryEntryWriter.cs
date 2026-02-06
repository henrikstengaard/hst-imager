using Hst.Core;
using Hst.Imager.Core.Caching;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UaeMetadatas;
using Models.FileSystems;

public class DirectoryEntryWriter : IEntryWriter
{
    private readonly byte[] buffer = new byte[4096];
    private readonly IList<string> logs = new List<string>();

    private readonly string rootPath;
    private readonly bool recursive;
    private readonly bool createDirectory;
    private readonly bool forceOverwrite;

    private readonly string[] rootPathComponents;
    /// <summary>
    /// dir path components after initialized
    /// is only used when creating single file
    /// </summary>
    private string[] dirPathComponents = [];
    private string initializedDirPath = string.Empty;
    private bool lastPathComponentExist = true;
    private EntryType lastPathComponentEntryType = EntryType.Dir;
    private bool isInitialized;
    private readonly IAppCache appCache;
    private readonly UaeMetadataHelper uaeMetadataHelper;

    /// <summary>
    /// Directory entry writer.
    /// </summary>
    /// <param name="rootPath">Root path components.</param>
    /// <param name="recursive">Recursive creating directories and files.</param>
    /// <param name="createDirectory">Create directory for root path components, if it doesn't exist.</param>
    /// <param name="forceOverwrite">Force overwriting any existing files.</param>
    /// <param name="appCache">Application cache.</param>
    public DirectoryEntryWriter(string rootPath, bool recursive, bool createDirectory, bool forceOverwrite,
        IAppCache appCache)
    {
        this.rootPath = rootPath;
        this.recursive = recursive;
        this.createDirectory = createDirectory;
        this.forceOverwrite = forceOverwrite;
        this.appCache = appCache;
        uaeMetadataHelper = new UaeMetadataHelper(appCache);
        rootPathComponents = GetPathComponents(rootPath);
    }

    public Media Media => null;
    public string MediaPath => rootPath;
    public string FileSystemPath => string.Empty;
    public UaeMetadata UaeMetadata { get; set; }

    public void Dispose()
    {
        appCache.Dispose();
    }

    public async Task<Result> Initialize()
    {
        var exisingPathComponents = new List<string>(10);

        lastPathComponentExist = true;

        var currentPath = string.Empty;
        var firstPathComponent = rootPathComponents.Length > 0 ? rootPathComponents[0] : string.Empty;
        
        if (PathHelper.IsRootPath(firstPathComponent))
        {
            var isWindowsRoot = PathHelper.IsWindowsRootPath(firstPathComponent);
            currentPath = isWindowsRoot ? firstPathComponent : "/";
            
        }

        for (var i = 0; i < rootPathComponents.Length; i++)
        {
            var pathComponent = rootPathComponents[i];
            var entryPath = Path.Combine(currentPath, pathComponent);

            var dirExists = Directory.Exists(entryPath);
            var fileExists = File.Exists(entryPath);

            var entryPathComponents = exisingPathComponents.Concat([pathComponent]).ToArray();
            var uaeMetadataEntry = await uaeMetadataHelper.GetUaeMetadataEntry(UaeMetadata, entryPathComponents);
            
            if (uaeMetadataEntry is { UaeMetadataExists: true })
            {
                pathComponent = uaeMetadataEntry.NormalPathComponents[^1];
                entryPath = Path.Combine(currentPath, pathComponent);
                dirExists = Directory.Exists(entryPath);
                fileExists = File.Exists(entryPath);
            }

            var exists = dirExists || fileExists;
            
            if (fileExists && i < rootPathComponents.Length - 1)
            {
                return new Result(new PathNotFoundError($"Path '{entryPath}' is a file and not a directory", entryPath));
            }

            if (!exists)
            {
                if (!createDirectory)
                {
                    if (i != rootPathComponents.Length - 1)
                    {
                        return new Result(new PathNotFoundError($"Path not found '{entryPath}'", entryPath));
                    }
                
                    lastPathComponentExist = false;
                
                    break;
                }
            
                var requiresUaeMetadata = UaeMetadata != UaeMetadata.None &&
                                          UaeMetadataHelper.RequiresUaeMetadataFileName(UaeMetadata, pathComponent);

                if (requiresUaeMetadata)
                {
                    entryPath = await uaeMetadataHelper.CreateUaeMetadataEntry(UaeMetadata, entryPathComponents);
                    pathComponent = Path.GetFileName(entryPath);
                }
                
                Directory.CreateDirectory(entryPath);
            }
            else
            {
                if (i == rootPathComponents.Length - 1)
                {
                    lastPathComponentEntryType = dirExists ? EntryType.Dir : EntryType.File;
            
                    if (!dirExists)
                    {
                        break;
                    }
                }
            }
            
            exisingPathComponents.Add(pathComponent);
            currentPath = Path.Combine(currentPath, pathComponent);
        }

        if (recursive && !lastPathComponentExist)
        {
            return new Result(new PathNotFoundError($"Path '{rootPath}' not found. Directory must exist when using recursive!", rootPath));
        }

        dirPathComponents = exisingPathComponents.ToArray();
        initializedDirPath = Path.Combine(dirPathComponents);

        isInitialized = true;

        return new Result();
    }

    public async Task<Result> CreateDirectory(Entry entry, string[] entryPathComponents, bool skipAttributes,
        bool isSingleFileEntry)
    {
        if (!isInitialized)
        {
            return new Result(new Error("DirectoryEntryWriter is not initialized."));
        }

        var fullPathComponents = PathComponentHelper.GetFullPathComponents(entry.Type, entryPathComponents,
            lastPathComponentEntryType, rootPathComponents, lastPathComponentExist, isSingleFileEntry);

        var fullPath = await uaeMetadataHelper.CreateUaeMetadataEntry(UaeMetadata, fullPathComponents);

        if (!lastPathComponentExist)
        {
            return new Result(new PathNotFoundError($"Path not found '{rootPath}'", rootPath));
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

        var name = await uaeMetadataHelper.GetFileName(UaeMetadata, dirPath, entryName);

        if (writeUaeMetadata)
        {
            await UaeMetadataHelper.WriteUaeMetadata(UaeMetadata, string.IsNullOrEmpty(dirPath) ? "." : dirPath,
                entryName, name, protectionBits, entry.Date ?? DateTime.Now, comment);
        }

        fullPath = Path.Combine(dirPath, name);

        if (File.Exists(fullPath))
        {
            return new Result(new Error($"Create directory path '{fullPath}' failed. Path already exists as a file!"));
        }
        
        if (!string.IsNullOrEmpty(fullPath) && !Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

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
            lastPathComponentEntryType, rootPathComponents, lastPathComponentExist, isSingleFileEntry);
        
        var fullPath = await uaeMetadataHelper.CreateUaeMetadataEntry(UaeMetadata, fullPathComponents);

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
        
        var fileName = await uaeMetadataHelper.GetFileName(UaeMetadata, dirPath, entryName);

        var filePath = string.IsNullOrEmpty(dirPath)
            ? fileName
            : Path.Combine(dirPath, fileName);

        if (!forceOverwrite && File.Exists(filePath))
        {
            return new Result(new FileExistsError($"File already exists '{filePath}'"));
        }
        
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
        return logs.Count == 0
            ? new List<string>()
            : new[]
            {
                string.Empty, "Following files were renamed to due invalid characters or conflicts with Windows OS reserved filenames:"
            }.Concat(logs);
    }

    public IEntryIterator CreateEntryIterator(string[] rootPathComponents, bool recursive)
    {
        return new DirectoryEntryIterator(string.Join(Path.PathSeparator, rootPathComponents), recursive,
            UaeMetadata, new MemoryAppCache());
    }

    public bool ArePathComponentsSelfCopy(IEntryIterator entryIterator)
    {
        // return false, if iterator is not a directory entry iterator
        // self copy/extract is only possible, when copying/extracting from and to same
        if (entryIterator is not DirectoryEntryIterator)
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
        // return false, if iterator is not a directory entry iterator
        // cyclic copy/extract is only possible, when copying/extracting from and to same
        if (entryIterator is not DirectoryEntryIterator)
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

    public bool SupportsUaeMetadata => true;

    private static string[] GetPathComponents(string path)
    {
        return (path.StartsWith("/") ? new []{"/"} : Array.Empty<string>())
            .Concat(path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
    }
}
