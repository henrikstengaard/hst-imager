using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DiscUtils.Udf;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.Models.FileSystems;
using Hst.Imager.Core.PathComponents;
using Hst.Imager.Core.UaeMetadatas;

namespace Hst.Imager.Core.Commands;

public class UdfEntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly IMediaPath mediaPath;
    private readonly string rootPath;
    private string[] rootPathComponents;
    private PathComponentMatcher pathComponentMatcher;
    private readonly UdfReader udfReader;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private bool disposed;

    public UdfEntryIterator(Stream stream, string rootPath, UdfReader udfReader, bool recursive)
    {
        this.stream = stream;
        this.mediaPath = new BackslashMediaPath();
        this.rootPath = string.IsNullOrEmpty(rootPath) ? string.Empty : rootPath;
        this.udfReader = udfReader;
        this.recursive = recursive;
        this.nextEntries = new Stack<Entry>();
        this.currentEntry = null;
        this.isFirst = true;

        var pathComponents = GetPathComponents(rootPath);
        this.pathComponentMatcher = new PathComponentMatcher(pathComponents, recursive);
        this.rootPathComponents = this.pathComponentMatcher.PathComponents;
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

    public Media Media => null;
    public string RootPath => rootPath;
    
    public Entry Current => currentEntry;

    public bool HasMoreEntries => nextEntries.Count > 0;
    public bool IsSingleFileEntryNext => 1 == nextEntries.Count && 
                                         nextEntries.All(x => x.Type == Models.FileSystems.EntryType.File);

    public Task<bool> Next()
    {
        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            
            try
            {
                EnqueueDirectory(this.rootPathComponents);
            }
            catch (DirectoryNotFoundException)
            {
                if (this.rootPathComponents.Length == 0)
                {
                    throw;
                }
                
                // path not found, retry without last part of root path components as it could a filename
                EnqueueDirectory(this.rootPathComponents.Take(this.rootPathComponents.Length - 1).ToArray());
            }
        }

        if (this.nextEntries.Count <= 0)
        {
            return Task.FromResult(false);
        }

        currentEntry = this.nextEntries.Pop();

        return Task.FromResult(true);
    }

    public Task<Stream> OpenEntry(Entry entry)
    {
        return Task.FromResult<Stream>(udfReader.OpenFile(entry.RawPath, FileMode.Open));
    }

    private int EnqueueDirectory(string[] pathComponents)
    {
        var uniqueEntries = new Dictionary<string, Entry>();

        var path = mediaPath.Join(pathComponents);

        foreach (var dirName in udfReader.GetDirectories(path).OrderByDescending(x => x).ToList())
        {
            var fullPathComponents = mediaPath.Split(dirName);

            var attributes = FileAttributesFormatter.FormatMsDosAttributes((int)udfReader.GetAttributes(dirName));
            var properties = new Dictionary<string, string>();

            var dirAttributes = FileAttributesFormatter.FormatMsDosAttributes((int)FileAttributes.Archive);

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, rootPathComponents,
                recursive, dirName, dirName, true, udfReader.GetLastWriteTime(dirName), 0,
                attributes, properties, dirAttributes).ToList();

            foreach (var entry in entries)
            {
                if (rootPath.Equals(entry.RawPath))
                {
                    continue;
                }

                uniqueEntries[entry.Name] = entry;
            }
        }

        foreach (var fileName in udfReader.GetFiles(path).OrderByDescending(x => x).ToList())
        {
            var formattedFilename = Iso9660ExtensionRegex.Replace(fileName, string.Empty);
            var entryName = FormatPath(StripIso9660Extension(formattedFilename));

            var fullPathComponents = mediaPath.Split(entryName);

            var attributes = FileAttributesFormatter.FormatMsDosAttributes((int)udfReader.GetAttributes(entryName));
            var properties = new Dictionary<string, string>();

            var date = udfReader.GetLastWriteTime(fileName);
            var size = udfReader.GetFileLength(fileName);
            var dirAttributes = string.Empty;

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, rootPathComponents,
                recursive, entryName, fileName, false, date, size,
                attributes, properties, dirAttributes).ToList();

            foreach (var entry in entries)
            {
                if ((entry.Type == Models.FileSystems.EntryType.Dir && rootPath.Equals(entry.Name)) ||
                    uniqueEntries.ContainsKey(entry.Name))
                {
                    continue;
                }

                uniqueEntries[entry.Name] = entry;
            }
        }

        foreach (var entry in uniqueEntries.Values.OrderByDescending(x => x.Name))
        {
            nextEntries.Push(entry);
        }

        return uniqueEntries.Values.Count;
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

    public UaeMetadata UaeMetadata { get; set; }
}