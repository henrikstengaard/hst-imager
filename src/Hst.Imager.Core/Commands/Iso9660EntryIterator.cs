namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DiscUtils.Iso9660;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.PathComponents;
using Hst.Imager.Core.UaeMetadatas;
using Entry = Models.FileSystems.Entry;

public class Iso9660EntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly IMediaPath mediaPath;
    private readonly string rootPath;
    private string[] rootPathComponents;
    private PathComponentMatcher pathComponentMatcher;
    private readonly CDReader cdReader;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private bool disposed;

    public Iso9660EntryIterator(Stream stream, string rootPath, CDReader cdReader, bool recursive)
    {
        this.stream = stream;
        this.mediaPath = MediaPath.Iso9660Path;
        this.rootPath = string.IsNullOrEmpty(rootPath) ? string.Empty : rootPath;
        this.cdReader = cdReader;
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

    public string RootPath => rootPath;
    
    public Entry Current => currentEntry;

    public async Task<bool> Next()
    {
        if (isFirst)
        {
            isFirst = false;
            currentEntry = null;
            
            try
            {
                await EnqueueDirectory(this.rootPathComponents);
            }
            catch (DirectoryNotFoundException)
            {
                if (this.rootPathComponents.Length == 0)
                {
                    throw;
                }
                
                // path not found, retry without last part of root path components as it could a filename
                await EnqueueDirectory(this.rootPathComponents.Take(this.rootPathComponents.Length - 1).ToArray());
            }
        }

        if (this.nextEntries.Count <= 0)
        {
            return false;
        }

        currentEntry = this.nextEntries.Pop();

        return true;
    }

    public Task<Stream> OpenEntry(Entry entry)
    {
        return Task.FromResult<Stream>(cdReader.OpenFile(entry.RawPath, System.IO.FileMode.Open));
    }

    private Task EnqueueDirectory(string[] pathComponents)
    {
        var uniqueEntries = new Dictionary<string, Entry>();

        var path = mediaPath.Join(pathComponents);

        foreach (var dirName in cdReader.GetDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(x => x).ToList())
        {
            var fullPathComponents = mediaPath.Split(dirName);

            var attributes = FileAttributesFormatter.FormatMsDosAttributes((int)cdReader.GetAttributes(dirName));
            var properties = new Dictionary<string, string>();

            var dirAttributes = FileAttributesFormatter.FormatMsDosAttributes((int)FileAttributes.Archive);

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, rootPathComponents,
                recursive, dirName, dirName, true, cdReader.GetLastWriteTime(dirName), 0,
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

        foreach (var fileName in cdReader.GetFiles(path, "*", SearchOption.AllDirectories).OrderByDescending(x => x).ToList())
        {
            var formattedFilename = Iso9660ExtensionRegex.Replace(fileName, string.Empty);
            var entryName = FormatPath(StripIso9660Extension(formattedFilename));

            var fullPathComponents = mediaPath.Split(entryName);

            var attributes = FileAttributesFormatter.FormatMsDosAttributes((int)cdReader.GetAttributes(entryName));
            var properties = new Dictionary<string, string>();

            var date = cdReader.GetLastWriteTime(fileName);
            var size = cdReader.GetFileLength(fileName);
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

    public bool UsesPattern => this.pathComponentMatcher?.UsesPattern ?? false;

    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public UaeMetadata UaeMetadata { get; set; }
}