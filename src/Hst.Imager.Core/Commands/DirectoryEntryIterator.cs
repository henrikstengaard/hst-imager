using Hst.Core;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using Amiga.DataTypes.UaeFsDbs;
using Amiga.DataTypes.UaeMetafiles;
using PathComponents;
using UaeMetadatas;
using Entry = Models.FileSystems.Entry;

public class DirectoryEntryIterator : IEntryIterator
{
    public PartitionTableType PartitionTableType => PartitionTableType.None;
    public int PartitionNumber => 0;

    private readonly Stack<Entry> nextEntries;
    private readonly string rootPath;
    private readonly string dirPath;
    private string[] rootPathComponents;
    private PathComponentMatcher pathComponentMatcher;
    private readonly bool recursive;
    private Entry currentEntry;
    private bool isFirst;
    private readonly MemoryCache cache;
    private readonly DateTimeOffset cacheExpiration;

    public DirectoryEntryIterator(string path, bool recursive)
    {
        this.nextEntries = new Stack<Entry>();
        rootPath = PathHelper.GetFullPath(path);
        this.recursive = recursive;
        this.isFirst = true;
        this.cache = new MemoryCache($"{nameof(DirectoryEntryIterator)}_CACHE");
        this.cacheExpiration = DateTimeOffset.Now.AddMinutes(10);
        dirPath = Directory.Exists(rootPath) ? rootPath : Path.GetDirectoryName(rootPath);
    }

    public Task<Result> Initialize()
    {
        IsSingleFileEntryNext = File.Exists(this.rootPath);
        
        if (!Directory.Exists(dirPath))
        {
            return Task.FromResult(new Result(new PathNotFoundError("Path not found '{dirPath}'", dirPath)));
        }
        
        var pathComponents = GetPathComponents(rootPath);
        var usePattern = !Directory.Exists(rootPath);

        rootPathComponents = pathComponents;
        DirPathComponents = !Directory.Exists(rootPath) && pathComponents.Length > 0
            ? pathComponents.Take(pathComponents.Length - 1).ToArray()
            : pathComponents;
            
        pathComponentMatcher = new PathComponentMatcher(usePattern ? pathComponents : [], 
            isFile: IsSingleFileEntryNext, recursive: recursive);
        
        return Task.FromResult(new Result());
    }

    public string[] PathComponents => rootPathComponents;

    public string[] DirPathComponents { get; private set; }

    public Media Media => null;
    public string RootPath => dirPath;

    public Entry Current => currentEntry;

    public bool HasMoreEntries => nextEntries.Count > 0;
    public bool IsSingleFileEntryNext { get; private set; }

    public async Task<bool> Next()
    {
        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            await EnqueueDirectory(DirPathComponents);
        }

        if (this.nextEntries.Count <= 0)
        {
            return false;
        }

        bool skipEntry;
        do
        {
            currentEntry = this.nextEntries.Pop();

            if (currentEntry.Type == Models.FileSystems.EntryType.File)
            {
                return true;
            }

            skipEntry = SkipEntry(currentEntry.FullPathComponents);

            if (recursive)
            {
                await EnqueueDirectory(currentEntry.FullPathComponents);
            }
        } while (nextEntries.Count > 0 && skipEntry);

        return true;
    }

    public Task<Stream> OpenEntry(Entry entry)
    {
        return Task.FromResult<Stream>(File.OpenRead(entry.RawPath));
    }

    public string[] GetPathComponents(string path)
    {
        return (path.StartsWith("/") ? new []{"/"} : Array.Empty<string>())
            .Concat(path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
    }

    public bool UsesPattern => pathComponentMatcher.UsesPattern;

    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public bool SupportsUaeMetadata => true;

    private async Task<UaeMetadataEntry> GetUaeMetadataEntry(string path, string[] pathComponents)
    {
        if (pathComponents.Length == 0)
        {
            return null;
        }

        var cacheKey = string.Join("|", pathComponents);

        var cacheEntry = cache.Get(cacheKey) as UaeMetadataEntry;

        if (cacheEntry != null)
        {
            return cacheEntry;
        }

        var currentPathComponents = new List<string>();
        var uaePathComponents = new List<string>();

        foreach (var pathComponent in pathComponents)
        {
            cacheKey = string.Join("|", currentPathComponents.Concat(new[] { pathComponent }));

            cacheEntry = cache.Get(cacheKey) as UaeMetadataEntry;

            // if path is cached, reuse and continue
            if (cacheEntry != null)
            {
                uaePathComponents.Add(cacheEntry.UaePathComponents[^1]);

                currentPathComponents.Add(pathComponent);

                continue;
            }

            // path is not cached
            var currentPath = Path.Combine(new[] { path }.Concat(currentPathComponents).ToArray());

            var uaeMetadataNodes = await ReadUaeMetadataNodes(currentPath);

            var uaeMetadataNode = uaeMetadataNodes.FirstOrDefault(x => x.NormalName.Equals(pathComponent, StringComparison.OrdinalIgnoreCase));

            uaePathComponents.Add(uaeMetadataNode != null ? uaeMetadataNode.AmigaName : pathComponent);

            currentPathComponents.Add(pathComponent);

            cacheEntry = new UaeMetadataEntry
            {
                UaePathComponents = uaePathComponents.ToArray(),
                PathComponents = pathComponents.ToArray(),
                Date = uaeMetadataNode?.Date,
                ProtectionBits = uaeMetadataNode?.ProtectionBits,
                Comment = uaeMetadataNode?.Comment
            };

            cache.Add(cacheKey, cacheEntry, cacheExpiration);
        }

        return cacheEntry;
    }

    private async Task EnqueueDirectory(string[] pathComponents)
    {
        var currentPath = Path.Combine(pathComponents);
        var currentDir = new DirectoryInfo(currentPath);

        foreach (var dirInfo in currentDir.GetDirectories().OrderByDescending(x => x.Name).ToList())
        {
            var fullPathComponents = GetPathComponents(dirInfo.FullName);

            var date = dirInfo.LastWriteTime;
            var properties = new Dictionary<string, string>();

            var relativePathComponents = fullPathComponents.Skip(DirPathComponents.Length).ToArray();

            if (UaeMetadata != UaeMetadata.None)
            {
                var uaeMetadataEntry = await GetUaeMetadataEntry(dirPath, relativePathComponents);

                if (uaeMetadataEntry != null)
                {
                    relativePathComponents = uaeMetadataEntry.UaePathComponents;
                    date = uaeMetadataEntry.Date ?? DateTime.Now;
                    if (uaeMetadataEntry.Comment != null)
                    {
                        properties[Core.Constants.EntryPropertyNames.Comment] = uaeMetadataEntry.Comment;
                    }

                    if (uaeMetadataEntry.ProtectionBits.HasValue)
                    {
                        properties[Core.Constants.EntryPropertyNames.ProtectionBits] = uaeMetadataEntry.ProtectionBits.ToString();
                    }
                }
            }

            var relativePath = string.Join(Path.DirectorySeparatorChar, relativePathComponents);

            this.nextEntries.Push(new Entry
            {
                Name = relativePath,
                FormattedName = relativePath,
                RawPath = dirInfo.FullName,
                FullPathComponents = fullPathComponents,
                RelativePathComponents = relativePathComponents,
                Date = date,
                Size = 0,
                Type = Models.FileSystems.EntryType.Dir,
                Properties = properties
            });
        }

        var fileInfos = currentDir.GetFiles().AsEnumerable();

        if (UaeMetadata != UaeMetadata.None)
        {
            fileInfos = RemoveUaeMetadataFiles(fileInfos);
        }

        foreach (var fileInfo in fileInfos.OrderByDescending(x => x.Name).ToList())
        {
            var fullPathComponents = GetPathComponents(fileInfo.FullName);

            var date = fileInfo.LastWriteTime;
            var properties = new Dictionary<string, string>();

            if (!pathComponentMatcher.IsMatch(fullPathComponents))
            {
                continue;
            }

            var relativePathComponents = fullPathComponents.Skip(DirPathComponents.Length).ToArray();

            if (UaeMetadata != UaeMetadata.None)
            {
                var uaeMetadataEntry = await GetUaeMetadataEntry(dirPath, relativePathComponents);

                if (uaeMetadataEntry != null)
                {
                    relativePathComponents = uaeMetadataEntry.UaePathComponents;
                    date = uaeMetadataEntry.Date ?? DateTime.Now;
                    if (uaeMetadataEntry.Comment != null)
                    {
                        properties[Core.Constants.EntryPropertyNames.Comment] = uaeMetadataEntry.Comment;
                    }

                    if (uaeMetadataEntry.ProtectionBits.HasValue)
                    {
                        properties[Core.Constants.EntryPropertyNames.ProtectionBits] = uaeMetadataEntry.ProtectionBits.ToString();
                    }

                }
            }

            var relativePath = string.Join(Path.DirectorySeparatorChar, relativePathComponents);

            this.nextEntries.Push(new Entry
            {
                Name = relativePath,
                FormattedName = relativePath,
                RawPath = fileInfo.FullName,
                FullPathComponents = fullPathComponents,
                RelativePathComponents = relativePathComponents,
                Date = date,
                Size = fileInfo.Length,
                Type = Models.FileSystems.EntryType.File,
                Properties = properties
            });
        }
    }

    public void Dispose()
    {
    }

    public UaeMetadata UaeMetadata { get; set; }

    private bool SkipEntry(string[] pathComponents)
    {
        if (!UsesPattern)
        {
            return false;
        }

        return !pathComponentMatcher.IsMatch(pathComponents);
    }

    private IEnumerable<FileInfo> RemoveUaeMetadataFiles(IEnumerable<FileInfo> fileInfos) =>
        fileInfos.Where(file =>
            !file.Name.Equals(Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName, StringComparison.OrdinalIgnoreCase) &&
            !file.Extension.Equals(Amiga.DataTypes.UaeMetafiles.Constants.UaeMetafileExtension, StringComparison.OrdinalIgnoreCase));

    private async Task<IEnumerable<UaeMetadataNode>> ReadUaeMetadataNodes(string path)
    {
        switch (UaeMetadata)
        {
            case UaeMetadata.UaeFsDb:
                return await ReadUaeFsDbFile(path);
            case UaeMetadata.UaeMetafile:
                return ReadUaeMetafiles(path);
            case UaeMetadata.None:
            default:
                return new List<UaeMetadataNode>();
        }
    }

    private static async Task<IEnumerable<UaeMetadataNode>> ReadUaeFsDbFile(string path)
    {
        var uaeFsDbFilePath = Path.Combine(path, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);

        if (File.Exists(uaeFsDbFilePath))
        {
            return await ReadUaeFsDbFileVersion1(path);
        }

        return await ReadUaeFsDbFileVersion2(path);
    }
    
    private static async Task<IEnumerable<UaeMetadataNode>> ReadUaeFsDbFileVersion1(string path)
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

    private static async Task<IEnumerable<UaeMetadataNode>> ReadUaeFsDbFileVersion2(string path)
    {
        var uaeMetadataNodes = new List<UaeMetadataNode>();

        var dirInfo = new DirectoryInfo(path);

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

            uaeMetadataNodes.AddRange(uaeFsDbNodes.Select(x => UaeMetadataNode.FromUaeFsDbNode(x)));
        }

        return uaeMetadataNodes;
    }

    private static IEnumerable<UaeMetadataNode> ReadUaeMetafiles(string path)
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

    public class UaeMetadataNode
    {
        public string AmigaName { get; set; }
        public string NormalName { get; set; }
        public int ProtectionBits { get; set; }
        public DateTime Date { get; set; }
        public string Comment { get; set; }

        public static UaeMetadataNode FromUaeFsDbNode(UaeFsDbNode uaeFsDbNode)
        {
            return new UaeMetadataNode
            {
                AmigaName = uaeFsDbNode.Version == UaeFsDbNode.NodeVersion.Version2
                    ? uaeFsDbNode.AmigaNameUnicode
                    : uaeFsDbNode.AmigaName,
                NormalName = uaeFsDbNode.Version == UaeFsDbNode.NodeVersion.Version2
                    ? uaeFsDbNode.NormalNameUnicode
                    : uaeFsDbNode.NormalName,
                ProtectionBits = (int)uaeFsDbNode.Mode,
                Comment = uaeFsDbNode.Comment
            };
        }

        public static UaeMetadataNode FromUaeMetafile(UaeMetafile uaeMetafile, string amigaName, string normalName)
        {
            return new UaeMetadataNode
            {
                AmigaName = amigaName,
                NormalName = normalName,
                ProtectionBits = (int)Amiga.FileSystems.ProtectionBitsConverter.ParseProtectionBits(uaeMetafile.ProtectionBits) ^ 0xf,
                Comment = uaeMetafile.Comment,
                Date = uaeMetafile.Date
            };
        }
    }
}