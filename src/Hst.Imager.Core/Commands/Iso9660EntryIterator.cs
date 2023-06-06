namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DiscUtils.Iso9660;
using Entry = Models.FileSystems.Entry;

public class Iso9660EntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly string rootPath;
    private string[] rootPathComponents;
    private PathComponentMatcher pathComponentMatcher;
    private readonly CDReader cdReader;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private bool disposed;

    // backslash is required by diskutils iso9660 to list directories and files in a given iso file
    private const string PathSeparator = "\\";

    public Iso9660EntryIterator(Stream stream, string rootPath, CDReader cdReader, bool recursive)
    {
        this.stream = stream;
        this.rootPath = string.IsNullOrEmpty(rootPath) ? string.Empty : rootPath;
        this.rootPathComponents = GetPathComponents(this.rootPath);
        this.pathComponentMatcher = null;
        this.cdReader = cdReader;
        this.recursive = recursive;
        this.nextEntries = new Stack<Entry>();
        this.currentEntry = null;
        this.isFirst = true;
    }

    private void ResolvePathComponentMatcher()
    {
        var pathComponents = GetPathComponents(rootPath);

        if (pathComponents.Length == 0)
        {
            this.rootPathComponents = pathComponents;
            this.pathComponentMatcher = new PathComponentMatcher(pathComponents, recursive: recursive);
            return;
        }

        var hasPattern = pathComponents[^1].IndexOf("*", StringComparison.OrdinalIgnoreCase) >= 0;
        this.rootPathComponents =
            hasPattern ? pathComponents.Take(pathComponents.Length - 1).ToArray() : pathComponents;
        this.pathComponentMatcher =
            new PathComponentMatcher(rootPathComponents, hasPattern ? pathComponents[^1] : null, recursive);
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
            ResolvePathComponentMatcher();
            
            try
            {
                await EnqueueDirectory(string.Join(PathSeparator, this.rootPathComponents));
            }
            catch (DirectoryNotFoundException)
            {
                if (this.rootPathComponents.Length == 0)
                {
                    throw;
                }
                
                // path not found, retry without last part of root path components as it could a filename
                await EnqueueDirectory(string.Join(PathSeparator, this.rootPathComponents.Take(this.rootPathComponents.Length - 1)));
            }
        }

        if (this.nextEntries.Count <= 0)
        {
            return false;
        }

        if (this.pathComponentMatcher.UsesPattern)
        {
            do
            {
                if (this.nextEntries.Count <= 0)
                {
                    return false;
                }
                currentEntry = this.nextEntries.Pop();
                if (this.recursive && currentEntry.Type == Models.FileSystems.EntryType.Dir)
                {
                    await EnqueueDirectory(currentEntry.RawPath);
                }
            } while (currentEntry.Type == Models.FileSystems.EntryType.Dir);
        }
        else
        {
            currentEntry = this.nextEntries.Pop();
            if (this.recursive && currentEntry.Type == Models.FileSystems.EntryType.Dir)
            {
                await EnqueueDirectory(currentEntry.RawPath);
            }
        }
        
        return true;
    }

    public Task<Stream> OpenEntry(Entry entry)
    {
        if (entry is Iso9660Entry isoEntry)
        {
            return Task.FromResult<Stream>(cdReader.OpenFile(isoEntry.IsoPath, FileMode.Open));
        }

        throw new ArgumentException("Entry is not Iso9660Entry", nameof(entry));
    }

    private Task EnqueueDirectory(string currentPath)
    {
        foreach (var dirName in cdReader.GetDirectories(currentPath).OrderByDescending(x => x).ToList())
        {
            var fullPathComponents = GetPathComponents(dirName);
            var relativePathComponents = fullPathComponents.Skip(this.rootPathComponents.Length).ToArray();
            var relativePath = string.Join(PathSeparator, relativePathComponents);
            
            var dirEntry = new Iso9660Entry
            {
                Name = relativePath,
                FormattedName = relativePath,
                RawPath = dirName,
                FullPathComponents = fullPathComponents,
                RelativePathComponents = relativePathComponents,
                IsoPath = dirName,
                Date = cdReader.GetLastWriteTime(dirName),
                Size = 0,
                Type = Models.FileSystems.EntryType.Dir
            };
            
            if (recursive || this.pathComponentMatcher.IsMatch(dirEntry.FullPathComponents))
            {
                this.nextEntries.Push(dirEntry);
            }
        }
        
        foreach (var fileName in cdReader.GetFiles(currentPath).OrderByDescending(x => x).ToList())
        {
            var formattedFilename = Iso9660ExtensionRegex.Replace(fileName, string.Empty);
            var entryName = FormatPath(StripIso9660Extension(formattedFilename));
            
            var entryFullPathComponents = GetPathComponents(entryName);
            var entryRelativePathComponents = this.rootPathComponents.SequenceEqual(entryFullPathComponents)
                ? new[] { entryFullPathComponents[^1] }
                : entryFullPathComponents.Skip(this.rootPathComponents.Length).ToArray();
            var entryRelativePath = string.Join(PathSeparator, entryRelativePathComponents);
            
            var fileEntry = new Iso9660Entry
            {
                Name = entryRelativePath,
                FormattedName = entryRelativePath,
                RawPath = fileName,
                FullPathComponents = entryFullPathComponents,
                RelativePathComponents = entryRelativePathComponents,
                IsoPath = fileName,
                Date = cdReader.GetLastWriteTime(fileName),
                Size = cdReader.GetFileLength(fileName),
                Type = Models.FileSystems.EntryType.File
            };

            if (this.pathComponentMatcher.IsMatch(fileEntry.FullPathComponents))
            {
                this.nextEntries.Push(fileEntry);
            }
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
}