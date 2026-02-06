using System.Collections.Generic;
using System.Linq;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Hst.Amiga.DataTypes.UaeMetafiles;
using Hst.Amiga.FileSystems;
using Hst.Core.Extensions;
using Hst.Imager.Core.Caching;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Helpers;

namespace Hst.Imager.Core.UaeMetadatas;

using System;
using System.IO;
using System.Threading.Tasks;

public class UaeMetadataHelper(IAppCache appCache)
{
    private static readonly bool IsWindowsOperatingSystem = OperatingSystem.IsWindows();

    private static string GetNodesListCacheKey(string path) => 
        string.Concat("NODES-LIST:", path);

    private static string GetNodesIndexedByAmigaNameCacheKey(string path) => 
        string.Concat("NODES-INDEXED-AMIGA-NAME:", path);

    private static string GetNodesIndexedByNormalNameCacheKey(string path) => 
        string.Concat("NODES-INDEXED-NORMAL-NAME:", path);
    
    private static string GetUaeMetadataAmigaNameEntryCacheKey(string path) => 
        string.Concat("AMIGA-NAME-ENTRY:", path);

    private static string GetUaeMetadataNormalNameEntryCacheKey(string path) => 
        string.Concat("NORMAL-NAME-ENTRY:", path);
    
    private static string GetUaeEntryPathCacheKey(string path) => 
        string.Concat("UAE-ENTRY-PATH:", path);

    private static string GetNormalEntryPathCacheKey(string path) => 
        string.Concat("NORMAL-ENTRY-PATH:", path);
    
    public async Task<string> CreateUaeMetadataEntry(UaeMetadata uaeMetadata, string[] entryPathComponents) =>
        uaeMetadata == UaeMetadata.None
            ? CreateNormalEntryPath(entryPathComponents)
            : await CreateUaeEntryPath(uaeMetadata, entryPathComponents);

    private async Task<string> CreateUaeEntryPath(UaeMetadata uaeMetadata, string[] entryPathComponents)
    {
        if (entryPathComponents.Length == 0)
        {
            return string.Empty;
        }

        // create uae entry path from entry path components, which may contain amiga filenames
        var entryPath = Path.Combine(entryPathComponents);
            
        var cacheKey = GetUaeEntryPathCacheKey(entryPath);

        var cacheEntry = appCache.Get(cacheKey) as UaeCacheEntry;

        if (cacheEntry != null)
        {
            return cacheEntry.NormalEntryPath;
        }

        entryPath = string.Empty;
        var normalEntryPath = string.Empty;
        
        var firstPathComponent = entryPathComponents[0];
        
        if (PathHelper.IsRootPath(firstPathComponent))
        {
            var isWindowsRoot = PathHelper.IsWindowsRootPath(firstPathComponent);
            entryPath = isWindowsRoot ? firstPathComponent : "/";
            normalEntryPath = entryPath;
            entryPathComponents = entryPathComponents.Skip(1).ToArray();
        }

        foreach (var pathComponent in entryPathComponents)
        {
            var cacheEntryPath = Path.Combine(entryPath, pathComponent);
            
            cacheKey = GetUaeEntryPathCacheKey(cacheEntryPath);
                
            cacheEntry = appCache.Get(cacheKey) as UaeCacheEntry;

            if (cacheEntry != null)
            {
                entryPath = cacheEntry.UaeEntryPath;
                normalEntryPath = cacheEntry.NormalEntryPath;
                
                continue;
            }

            if (!RequiresUaeMetadataFileName(uaeMetadata, pathComponent))
            {
                entryPath = Path.Combine(entryPath, pathComponent);
                normalEntryPath = Path.Combine(normalEntryPath, pathComponent);
                
                cacheKey = GetUaeEntryPathCacheKey(entryPath);
                
                var uaeCacheEntry = new UaeCacheEntry
                {
                    EntryPathComponent = pathComponent,
                    UaeEntryPath = entryPath,
                    NormalEntryPath = normalEntryPath
                };
    
                appCache.Add(cacheKey, uaeCacheEntry);
                
                continue;
            }
            
            // create normal filename for path component in entry path
            var normalFileName = await GetFileName(uaeMetadata, normalEntryPath, pathComponent);

            entryPath = Path.Combine(entryPath, pathComponent);
            normalEntryPath = Path.Combine(normalEntryPath, normalFileName);
            
            cacheKey = GetUaeEntryPathCacheKey(entryPath);
            
            cacheEntry = new UaeCacheEntry
            {
                EntryPathComponent = pathComponent,
                UaeEntryPath = entryPath,
                NormalEntryPath = normalEntryPath
            };
            
            appCache.Add(cacheKey, cacheEntry);
        }

        return normalEntryPath;
    }

    /// <summary>
    /// Get name for directory or file. If name already has uae metadata applied,
    /// then that is read or created, if it doesn't exist.
    /// </summary>
    /// <param name="uaeMetadata"></param>
    /// <param name="dirPath">Path to directory to get filename in.</param>
    /// <param name="amigaName"></param>
    /// <returns></returns>
    public async Task<string> GetFileName(UaeMetadata uaeMetadata, string dirPath, string amigaName)
    {
        if (uaeMetadata == UaeMetadata.None)
        {
            return CreateNormalFilename(amigaName);
        }

        var uaeMetadataDir = string.IsNullOrEmpty(dirPath) ? Directory.GetCurrentDirectory() : dirPath;

        var uaeMetadataNodes = await GetUaeMetadataNodesIndexedByAmigaName(uaeMetadata,
            uaeMetadataDir);

        var uaeMetadataNode = uaeMetadataNodes.GetValueOrDefault(amigaName);

        var normalName = uaeMetadataNode == null
            ? CreateUaeMetadataFileName(uaeMetadata, uaeMetadataDir, amigaName)
            : uaeMetadataNode.NormalName;

        var requiresUaeMetadata = !amigaName.Equals(normalName);

        if (requiresUaeMetadata && uaeMetadataNode == null)
        {
            await WriteUaeMetadata(uaeMetadata, uaeMetadataDir, amigaName, normalName);
        }

        return normalName;
    }
    
    public string CreateNormalEntryPath(string[] pathComponents)
    {
        var isRootPath = PathHelper.IsRootPath(pathComponents[0]);
    
        var normalisedPathComponents = isRootPath
            ? new[]{ pathComponents[0] }.Concat(pathComponents.Skip(1).Select(UaeMetadataHelper.CreateNormalFilename))
            : pathComponents.Select(UaeMetadataHelper.CreateNormalFilename);
    
        return Path.Combine(normalisedPathComponents.ToArray());
    }


    /// <summary>
    /// Get UAE metadata entry for path components.
    /// THIS IS A REFACTORING OF GetUaeMetadataEntry WITH FULL PATH COMPONENTS INSTEAD OF DIRPATH
    /// </summary>
    /// <param name="uaeMetadata">UAE metadata type.</param>
    /// <param name="pathComponents">Path components.</param>
    /// <returns></returns>
    public async Task<UaeMetadataEntry> GetUaeMetadataEntry(UaeMetadata uaeMetadata, string[] pathComponents)
    {
        if (uaeMetadata == UaeMetadata.None || pathComponents.Length == 0)
        {
            return null;
        }

        UaeMetadataEntry cacheEntry = null;
        var currentPathComponents = new List<string>();
        var uaePathComponents = new List<string>();
        var dirPath = string.Empty;
        var firstPathComponent = pathComponents[0];
        
        if (PathHelper.IsRootPath(firstPathComponent))
        {
            var isWindowsRoot = PathHelper.IsWindowsRootPath(firstPathComponent);
            dirPath = isWindowsRoot ? string.Concat(firstPathComponent, Path.DirectorySeparatorChar) : "/";
            if (isWindowsRoot)
            {
                currentPathComponents.Add(firstPathComponent);
                uaePathComponents.Add(firstPathComponent);
                pathComponents = pathComponents.Skip(1).ToArray();
            }
        }
        
        for (var i = 0; i < pathComponents.Length; i++)
        {
            var pathComponent = pathComponents[i];
            
            var entryPath = Path.Combine(dirPath, pathComponent);
            
            // get from cache by amiga name
            var cacheKey = GetUaeMetadataAmigaNameEntryCacheKey(entryPath);

            cacheEntry = appCache.Get(cacheKey) as UaeMetadataEntry;

            // get from cache by normal name, if not found by amiga name
            if (cacheEntry == null)
            {
                cacheKey = GetUaeMetadataNormalNameEntryCacheKey(entryPath);
                
                cacheEntry = appCache.Get(cacheKey) as UaeMetadataEntry;
            }
            
            // if path is cached, reuse and continue
            var currentPath = PathHelper.GetFullPath(dirPath);

            if (cacheEntry != null)
            {
                uaePathComponents = cacheEntry.UaePathComponents.ToList();
                currentPathComponents = cacheEntry.NormalPathComponents.ToList();
                dirPath = Path.Combine(cacheEntry.DirPath, currentPathComponents[^1]);
                
                continue;
            }

            // path is not cached

            // default names
            var uaePathComponentAmigaName = pathComponent;
            var pathComponentNormalName = pathComponent;

            //var uaeMetadataExists = false;
            UaeMetadataNode uaeMetadataNode = null;
            var uaeMetadataNodesIndexedByNormalName =
                await GetUaeMetadataNodesIndexedByNormalName(uaeMetadata, currentPath);
            
            var uaeMetadataNodeIndexedByNormalName = uaeMetadataNodesIndexedByNormalName.GetValueOrDefault(pathComponent);

            if (uaeMetadataNodeIndexedByNormalName != null)
            {
                uaeMetadataNode = uaeMetadataNodeIndexedByNormalName;
                uaePathComponentAmigaName = uaeMetadataNodeIndexedByNormalName.AmigaName;
            }
            
            var uaeMetadataNodesIndexedByAmigaName =
                await GetUaeMetadataNodesIndexedByAmigaName(uaeMetadata, currentPath);

            var uaeMetadataNodeIndexedByAmigaName = uaeMetadataNodesIndexedByAmigaName.GetValueOrDefault(pathComponent);

            if (uaeMetadataNodeIndexedByAmigaName != null)
            {
                uaeMetadataNode = uaeMetadataNodeIndexedByAmigaName;
                pathComponentNormalName = uaeMetadataNodeIndexedByAmigaName.NormalName;
            }
            
            uaePathComponents.Add(uaePathComponentAmigaName);
            currentPathComponents.Add(pathComponentNormalName);

            // use updated path component to check if it exists as directory or file
            entryPath = Path.Combine(dirPath, pathComponentNormalName);
            
            if (!Directory.Exists(entryPath) && !File.Exists(entryPath))
            {
                return null;
            }
            
            cacheEntry = new UaeMetadataEntry
            {
                DirPath = dirPath,
                UaeMetadataExists = uaeMetadataNode != null,
                UaePathComponents = uaePathComponents.ToArray(),
                NormalPathComponents = currentPathComponents.ToArray(),
                Date = uaeMetadataNode?.Date,
                ProtectionBits = uaeMetadataNode?.ProtectionBits,
                Comment = uaeMetadataNode?.Comment
            };

            var amigaNamePath = Path.Combine(dirPath, uaePathComponentAmigaName);
            var normalNamePath = Path.Combine(dirPath, pathComponentNormalName);
            
            cacheKey = GetUaeMetadataAmigaNameEntryCacheKey(amigaNamePath);
            appCache.Add(cacheKey, cacheEntry);
            
            cacheKey = GetUaeMetadataNormalNameEntryCacheKey(normalNamePath);
            appCache.Add(cacheKey, cacheEntry);
            
            dirPath = Path.Combine(currentPath, pathComponentNormalName);
        }

        return cacheEntry;
    }

    /// <summary>
    /// Get UAE metadata nodes indexed by Amiga name.
    /// </summary>
    /// <param name="uaeMetadata">UAE metadata type.</param>
    /// <param name="path">Directory path.</param>
    /// <returns>UAE metadata nodes indexed by Amiga name.</returns>
    public async Task<Dictionary<string, UaeMetadataNode>> GetUaeMetadataNodesIndexedByAmigaName(
        UaeMetadata uaeMetadata, string path)
    {
        var cacheKey = GetNodesIndexedByAmigaNameCacheKey(path);

        Dictionary<string, UaeMetadataNode> nodesIndexedByAmigaName;
        if (appCache.Contains(cacheKey))
        {
            nodesIndexedByAmigaName = appCache.Get(cacheKey) as Dictionary<string, UaeMetadataNode>;
            if (nodesIndexedByAmigaName != null)
            {
                return nodesIndexedByAmigaName;
            }
        }
        
        var nodes = await ReadUaeMetadataNodes(uaeMetadata, path);

        nodesIndexedByAmigaName = nodes.ToDictionary(node => node.AmigaName,
            StringComparer.OrdinalIgnoreCase);

        appCache.Add(cacheKey, nodesIndexedByAmigaName);
        
        return nodesIndexedByAmigaName;
    }

    /// <summary>
    /// Get UAE metadata nodes indexed by normal name.
    /// </summary>
    /// <param name="uaeMetadata">UAE metadata type.</param>
    /// <param name="path">Directory path.</param>
    /// <returns>UAE metadata nodes indexed by normal name.</returns>
    public async Task<Dictionary<string, UaeMetadataNode>> GetUaeMetadataNodesIndexedByNormalName(
        UaeMetadata uaeMetadata, string path)
    {
        var cacheKey = GetNodesIndexedByNormalNameCacheKey(path);

        Dictionary<string, UaeMetadataNode> nodesIndexedByNormalName;
        if (appCache.Contains(cacheKey))
        {
            nodesIndexedByNormalName = appCache.Get(cacheKey) as Dictionary<string, UaeMetadataNode>;
            if (nodesIndexedByNormalName != null)
            {
                return nodesIndexedByNormalName;
            }
        }
        
        var nodes = await ReadUaeMetadataNodes(uaeMetadata, path);

        nodesIndexedByNormalName = nodes.ToDictionary(node => node.NormalName,
            StringComparer.OrdinalIgnoreCase);

        appCache.Add(cacheKey, nodesIndexedByNormalName);
        
        return nodesIndexedByNormalName;
    }
    
    /// <summary>
    /// Read UAE metadata nodes from directory based on UAE metadata type.
    /// </summary>
    /// <param name="uaeMetadata">UAE metadata type.</param>
    /// <param name="path">Directory path.</param>
    /// <returns>UAE metadata nodes.</returns>
    public async Task<List<UaeMetadataNode>> ReadUaeMetadataNodes(UaeMetadata uaeMetadata, string path)
    {
        if (!Directory.Exists(path))
        {
            return new List<UaeMetadataNode>();
        }
        
        var cacheKey = GetNodesListCacheKey(path);

        List<UaeMetadataNode> nodes;
        if (appCache.Contains(cacheKey))
        {
            nodes = appCache.Get(cacheKey) as List<UaeMetadataNode>;
            if (nodes != null)
            {
                return nodes;
            }
        }
        
        switch (uaeMetadata)
        {
            case UaeMetadata.UaeFsDb:
                nodes = await ReadUaeFsDbFile(path);
                break;
            case UaeMetadata.UaeMetafile:
                nodes = ReadUaeMetafiles(path);
                break;
            case UaeMetadata.None:
            default:
                nodes = new List<UaeMetadataNode>();
                break;
        }
        
        appCache.Add(cacheKey, nodes);

        return nodes;
    }

    /// <summary>
    /// Read UAE metadata nodes from directory UAEFSDB file or alternative data streams.
    /// </summary>
    /// <param name="path">Directory path.</param>
    /// <returns>UAE metadata nodes.</returns>
    private async Task<List<UaeMetadataNode>> ReadUaeFsDbFile(string path)
    {
        var uaeFsDbFilePath = Path.Combine(path, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);

        if (File.Exists(uaeFsDbFilePath))
        {
            return await ReadUaeFsDbFileVersion1(path);
        }

        return await ReadUaeFsDbFileVersion2(path);
    }
    
    private static async Task<List<UaeMetadataNode>> ReadUaeFsDbFileVersion1(string path)
    {
        var uaeFsDbFilePath = Path.Combine(path, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);

        if (!File.Exists(uaeFsDbFilePath))
        {
            return new List<UaeMetadataNode>();
        }

        var uaeFsDbNodes = await UaeFsDbReader.ReadFromFile(uaeFsDbFilePath);

        var uaeMetadataNodes = new List<UaeMetadataNode>();

        foreach (var uaeFsDbNode in uaeFsDbNodes)
        {
            var uaeMetadataNode = UaeMetadataNode.FromUaeFsDbNode(uaeFsDbNode);

            var filePath = Path.Combine(path, uaeFsDbNode.NormalName);

            uaeMetadataNode.Date = File.Exists(filePath)
                ? new FileInfo(filePath).LastWriteTime
                : DateTime.Now;

            uaeMetadataNodes.Add(uaeMetadataNode);
        }

        return uaeMetadataNodes;
    }

    private async Task<List<UaeMetadataNode>> ReadUaeFsDbFileVersion2(string path)
    {
        var uaeMetadataNodes = new List<UaeMetadataNode>();

        var dirInfo = new DirectoryInfo(path);

        if (!dirInfo.Exists)
        {
            return uaeMetadataNodes;
        }
        
        var uaeFsDbAlternativeStreamPaths = dirInfo.GetDirectories().Select(x => string.Concat(x.FullName, ":", Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName))
            .Concat(dirInfo.GetFiles().Select(x => string.Concat(x.FullName, ":", Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName)))
            .ToList();

        foreach (var uaeFsDbAlternativeStreamPath in uaeFsDbAlternativeStreamPaths)
        {
            if (!File.Exists(uaeFsDbAlternativeStreamPath))
            {
                continue;
            }

            var uaeFsDbNodes = await UaeFsDbReader.ReadFromFile(uaeFsDbAlternativeStreamPath);

            foreach (var uaeFsDbNode in uaeFsDbNodes)
            {
                var uaeMetadataNode = UaeMetadataNode.FromUaeFsDbNode(uaeFsDbNode);
                uaeMetadataNodes.Add(uaeMetadataNode);
            }
        }

        return uaeMetadataNodes;
    }

    private static List<UaeMetadataNode> ReadUaeMetafiles(string path)
    {
        var dirInfo = new DirectoryInfo(path);

        var uaeMetadataNodes = new List<UaeMetadataNode>();

        foreach (var file in dirInfo.GetFiles($"*{Amiga.DataTypes.UaeMetafiles.Constants.UaeMetafileExtension}", SearchOption.TopDirectoryOnly))
        {
            var uaeMetafile = UaeMetafileReader.Read(File.ReadAllBytes(file.FullName));

            var name = file.Name.Substring(0, file.Name.Length - Amiga.DataTypes.UaeMetafiles.Constants.UaeMetafileExtension.Length);
            var amigaName = UaeMetafileHelper.DecodeFilename(name);

            var uaeMetadataNode = UaeMetadataNode.FromUaeMetafile(uaeMetafile, amigaName, name);

            uaeMetadataNodes.Add(uaeMetadataNode);
        }

        return uaeMetadataNodes;
    }

    
    public static bool RequiresUaeMetadataProperties(int? protectionBits, string comment) =>
        protectionBits.HasValue && protectionBits != 0 || !string.IsNullOrEmpty(comment);

    private static bool HasInvalidFilenameChars(string fileName)
    {
        if (fileName.Equals(".") || fileName.Equals(".."))
        {
            return true;
        }

        return Regexs.InvalidFilenameCharsRegex.IsMatch(fileName) ||
               IsWindowsOperatingSystem && HasWindowsReservedNames(fileName);
    }

    private static bool HasWindowsReservedNames(string fileName)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName) ?? string.Empty;

        return Regexs.WindowsReservedNamesRegex.IsMatch(fileName) ||
               Regexs.WindowsReservedNamesRegex.IsMatch(fileNameWithoutExtension);
    }

    public static bool RequiresUaeMetadataFileName(UaeMetadata uaeMetadata, string fileName)
    {
        if (IsWindowsOperatingSystem && HasWindowsReservedNames(fileName))
        {
            return true;
        }

        return uaeMetadata switch
        {
            UaeMetadata.UaeFsDb => UaeFsDbNodeHelper.HasSpecialFilenameChars(fileName),
            UaeMetadata.UaeMetafile => UaeMetafileHelper.HasSpecialFilenameChars(fileName),
            _ => HasInvalidFilenameChars(fileName)
        };
    }

    public static string CreateNormalFilename(string fileName)
    {
        if (fileName.Equals(".") || fileName.Equals(".."))
        {
            return Regexs.InvalidFilenameCharsRegex.Replace(fileName, "_");
        }

        if (IsWindowsOperatingSystem && HasWindowsReservedNames(fileName))
        {
            return $"_{fileName}";
        }

        return Regexs.InvalidFilenameCharsRegex.Replace(fileName, "_");
    }

    public static string CreateUaeMetadataFileName(UaeMetadata uaeMetadata, string dirPath, string fileName)
    {
        if (!RequiresUaeMetadataFileName(uaeMetadata, fileName))
        {
            return fileName;
        }
        
        return uaeMetadata switch
        {
            UaeMetadata.UaeFsDb => UaeFsDbNodeHelper.CreateUniqueNormalName(dirPath,
                UaeFsDbNodeHelper.MakeSafeFilename(fileName)),
            UaeMetadata.UaeMetafile => HasWindowsReservedNames(fileName)
                ? UaeMetafileHelper.EncodeFilename(fileName)
                : UaeMetafileHelper.EncodeFilenameSpecialChars(fileName),
            _ => CreateNormalFilename(fileName)
        };
    }

    public static bool IsChanged(string normalName1, int? protectionBits1, DateTime? date1, string comment1,
        string normalName2, int? protectionBits2, DateTime? date2, string comment2)
    {
        if (protectionBits1.HasValue && protectionBits2.HasValue &&
            protectionBits1 != protectionBits2)
        {
            return true;
        }
        
        if (date1.HasValue && date2.HasValue &&
            date1 != date2)
        {
            return true;
        }

        return !(comment1 ?? string.Empty).Equals(comment2 ?? string.Empty);
    }
    
    public static async Task WriteUaeMetadata(UaeMetadata uaeMetadata, string dirPath, string amigaName, 
        string normalName, int? protectionBits = null, DateTime? date = null, string comment = null)
    {
        if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }

        switch (uaeMetadata)
        {
            case UaeMetadata.UaeFsDb:
                await WriteUaeFsDb(dirPath, amigaName, normalName, protectionBits, comment);
                break;
            case UaeMetadata.UaeMetafile:
                await WriteUaeMetafile(dirPath, normalName, protectionBits, date, comment);
                break;
        }
    }

    public static async Task WriteUaeFsDb(string dirPath, string amigaName, string normalName, int? protectionBits, string comment)
    {
        var uaeFsDbPath = Path.Combine(dirPath, "_UAEFSDB.___");

        await using var stream = new FileStream(uaeFsDbPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

        var hasNode = false;
        UaeFsDbNode node = null;

        while (stream.Length >= Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbNodeVersion1Size && stream.Position < stream.Length)
        {
            var position = stream.Position;
            var nodeBytes = await stream.ReadBytes(Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbNodeVersion1Size);

            node = UaeFsDbReader.ReadFromBytes(nodeBytes);

            if (!node.AmigaName.Equals(amigaName))
            {
                continue;
            }

            // seek back to position to overwrite node
            hasNode = true;
            stream.Seek(position, SeekOrigin.Begin);
            break;
        }

        var nodeUpdated = false;

        // node not found, create new one
        if (!hasNode)
        {
            node = new UaeFsDbNode
            {
                Version = UaeFsDbNode.NodeVersion.Version1,
                Mode = protectionBits.HasValue ? (uint)protectionBits : 0U,
                Comment = string.IsNullOrWhiteSpace(comment) ? string.Empty : comment,
                NormalName = normalName,
                AmigaName = amigaName
            };
            
            nodeUpdated = true;
        }
        
        if (protectionBits.HasValue && node.Mode != (uint)protectionBits)
        {
            node.Mode = (uint)protectionBits;
            nodeUpdated = true;
        }

        if (!(node.Comment ?? String.Empty).Equals(comment ?? string.Empty))
        {
            node.Comment = comment;
            nodeUpdated = true;
        }

        if (!nodeUpdated)
        {
            return;
        }
        
        var newNodeBytes = UaeFsDbWriter.Build(node);

        if (!hasNode)
        {
            stream.Seek(0, SeekOrigin.End);
        }

        await stream.WriteAsync(newNodeBytes, 0, newNodeBytes.Length);
    }

    public static async Task WriteUaeMetafile(string dirPath, string normalName, int? protectionBits, DateTime? date, string comment)
    {
        var uaeMetafile = new UaeMetafile
        {
            ProtectionBits = EntryFormatter.FormatProtectionBits(
                (ProtectionBits)((protectionBits ?? 0) ^ 0xf)).ToLower(), // mask away "RWED" protection bits
            Date = date ?? DateTime.Now,
            Comment = comment ?? string.Empty
        };

        var uaeMetafileBytes = UaeMetafileWriter.Build(uaeMetafile);

        var uaeMetafilePath = Path.Combine(dirPath, string.Concat(normalName,
            Amiga.DataTypes.UaeMetafiles.Constants.UaeMetafileExtension));

        await File.WriteAllBytesAsync(uaeMetafilePath, uaeMetafileBytes);
    }
}