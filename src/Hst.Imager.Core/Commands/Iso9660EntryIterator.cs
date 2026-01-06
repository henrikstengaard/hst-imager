using Hst.Core;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.Models.FileSystems;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DiscUtils.Iso9660;
using Helpers;
using PathComponents;
using UaeMetadatas;

public class Iso9660EntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly IMediaPath mediaPath;
    private readonly string rootPath;
    private readonly string[] rootPathComponents;
    private PathComponentMatcher pathComponentMatcher;
    private readonly CDReader cdReader;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private bool initialized;
    private readonly HashSet<string> dirHasEntries = [];

    public PartitionTableType PartitionTableType => PartitionTableType.None;
    public int PartitionNumber => 0;

    public Iso9660EntryIterator(Stream stream, string rootPath, CDReader cdReader, bool recursive)
    {
        this.stream = stream;
        mediaPath = MediaPath.GenericMediaPath;
        this.rootPath = string.IsNullOrEmpty(rootPath) ? string.Empty : rootPath;
        this.cdReader = cdReader;
        this.recursive = recursive;
        nextEntries = new Stack<Entry>();
        currentEntry = null;
        isFirst = true;
        rootPathComponents = GetPathComponents(rootPath);
    }

    public void Dispose()
    {
        cdReader.Dispose();
        stream.Dispose();
    }

    public Task<Result> Initialize()
    {
        if (rootPathComponents.Length == 0)
        {
            DirPathComponents = [];
            pathComponentMatcher = new PathComponentMatcher(rootPathComponents, recursive: recursive);
            initialized = true;
            return Task.FromResult(new Result());
        }
        
        var dirComponents = new List<string>();
        var usePattern = false;

        var validDirComponents = new List<string>();
        
        foreach(var pathComponent in rootPathComponents)
        {
            dirComponents.Add(pathComponent);

            var dirPath = mediaPath.Join(dirComponents.ToArray());
            
            if (cdReader.DirectoryExists(dirPath))
            {
                validDirComponents.Add(pathComponent);
                continue;
            }
            
            // use pattern, if last path component is not a directory
            if (validDirComponents.Count == PathComponents.Length - 1)
            {
                usePattern = true;
                IsSingleFileEntryNext = cdReader.FileExists(dirPath) && PathComponents.Length > 0;
                if (IsSingleFileEntryNext || PathComponentHelper.HasWildcard(pathComponent))
                {
                    break;
                }
            }
            
            return Task.FromResult(new Result(new PathNotFoundError($"Path not found '{dirPath}'", dirPath)));
        }
        
        DirPathComponents = validDirComponents.ToArray();
        pathComponentMatcher = new PathComponentMatcher(usePattern ? dirComponents.ToArray() : [], recursive: recursive);
        initialized = true;
        return Task.FromResult(new Result());
    }

    private void ThrowIfNotInitialized()
    {
        if (initialized)
        {
            return;
        }

        throw new InvalidOperationException("File system entry iterator not initialized");
    }
    
    /// <summary>
    /// Root path components of iterator.
    /// </summary>
    public string[] PathComponents => rootPathComponents;

    /// <summary>
    /// Dir path components from root path components that exist and is set during initialization.
    /// </summary>
    public string[] DirPathComponents { get; private set; } = [];
    
    public Media Media => null;
    public string RootPath => rootPath;
    
    public Entry Current => currentEntry;

    public bool HasMoreEntries => nextEntries.Count > 0;
    public bool IsSingleFileEntryNext { get; private set; }

    public async Task<bool> Next()
    {
        ThrowIfNotInitialized();
        
        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            
            await EnqueueDirectory(DirPathComponents);
        }

        if (nextEntries.Count <= 0)
        {
            return false;
        }

        bool skipEntry;
        do
        {
            currentEntry = nextEntries.Pop();
            
            if (currentEntry.Type == EntryType.File)
            {
                return true;
            }

            if (recursive)
            {
                var dirPath = mediaPath.Join(currentEntry.FullPathComponents);
                skipEntry = pathComponentMatcher.UsesPattern && !dirHasEntries.Contains(dirPath);
            }
            else
            {
                skipEntry = !EntryIteratorFunctions.IsRelativePathComponentsValid(pathComponentMatcher,
                    currentEntry.RelativePathComponents, recursive);
            }

        } while (nextEntries.Count > 0 && skipEntry);
        
        return true;
    }

    public Task<Stream> OpenEntry(Entry entry)
    {
        return Task.FromResult<Stream>(cdReader.OpenFile(entry.RawPath, FileMode.Open));
    }

    private Task EnqueueDirectory(string[] pathComponents)
    {
        var uniqueEntries = new Dictionary<string, Entry>();

        var path = mediaPath.Join(pathComponents);

        foreach (var dirName in cdReader.GetDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(x => x).ToList())
        {
            var attributes = FileAttributesFormatter.FormatMsDosAttributes((int)cdReader.GetAttributes(dirName));
            var properties = new Dictionary<string, string>();

            var dirAttributes = FileAttributesFormatter.FormatMsDosAttributes((int)FileAttributes.Archive);

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, DirPathComponents,
                recursive, dirName, dirName, true, cdReader.GetLastWriteTime(dirName), 0,
                attributes, properties, dirAttributes).ToList();

            foreach (var entry in entries)
            {
                if ((entry.Type == EntryType.Dir && rootPath.Equals(entry.RawPath)) ||
                    (entry.Type == EntryType.Dir && UsesPattern))
                {
                    continue;
                }

                uniqueEntries[entry.Name] = entry;
            }
        }

        foreach (var fileName in cdReader.GetFiles(path, "*", SearchOption.AllDirectories).OrderByDescending(x => x).ToList())
        {
            var formattedFilename = Iso9660ExtensionRegex.Replace(fileName, string.Empty);
            var entryName = FormatPath(StripIso9660Extension(formattedFilename));

            var attributes = FileAttributesFormatter.FormatMsDosAttributes((int)cdReader.GetAttributes(entryName));
            var properties = new Dictionary<string, string>();

            var date = cdReader.GetLastWriteTime(fileName);
            var size = cdReader.GetFileLength(fileName);
            var dirAttributes = string.Empty;

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, DirPathComponents,
                recursive, entryName, fileName, false, date, size,
                attributes, properties, dirAttributes).ToList();

            foreach (var entry in entries)
            {
                if ((entry.Type == EntryType.Dir && rootPath.Equals(entry.Name)) ||
                    uniqueEntries.ContainsKey(entry.Name))
                {
                    continue;
                }

                if (entry.Type == EntryType.File)
                {
                    var dirPath =
                        mediaPath.Join(entry.FullPathComponents.Take(entry.FullPathComponents.Length - 1).ToArray());
                    dirHasEntries.Add(dirPath);
                }

                uniqueEntries[entry.Name] = entry;
            }
        }

        foreach (var entry in uniqueEntries.Values.OrderByDescending(x => x.Name))
        {
            nextEntries.Push(entry);
        }

        return Task.CompletedTask;
    }

    private static readonly Regex Iso9660ExtensionRegex =
        new Regex(";\\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private string FormatPath(string path)
    {
        return path.StartsWith("\\") || path.StartsWith("/") ? path.Substring(1) : path;
    }
    
    private string StripIso9660Extension(string path)
    {
        return Iso9660ExtensionRegex.Replace(path, "");
    }
    
    public string[] GetPathComponents(string path)
    {
        return path.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);
    }

    public bool UsesPattern => pathComponentMatcher.UsesPattern;

    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public bool SupportsUaeMetadata => false;

    public UaeMetadata UaeMetadata { get; set; }
}