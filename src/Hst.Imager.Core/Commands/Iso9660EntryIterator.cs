﻿namespace Hst.Imager.Core.Commands;

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
    private readonly string[] rootPathComponents;
    private readonly CDReader cdReader;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private bool disposed;

    public Iso9660EntryIterator(Stream stream, string rootPath, CDReader cdReader, bool recursive)
    {
        this.stream = stream;
        this.rootPath = string.IsNullOrEmpty(rootPath) ? string.Empty : rootPath.Replace("/", "\\");
        this.rootPathComponents = GetPathComponents(this.rootPath);
        this.cdReader = cdReader;
        this.recursive = recursive;
        this.nextEntries = new Stack<Entry>();
        this.currentEntry = null;
        this.isFirst = true;
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
            await EnqueueDirectory(rootPath);
        }

        if (this.nextEntries.Count <= 0)
        {
            return false;
        }

        currentEntry = this.nextEntries.Pop();
        if (this.recursive && currentEntry.Type == Models.FileSystems.EntryType.Dir)
        {
            await EnqueueDirectory(currentEntry.RawPath);
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
            var entryName = FormatPath(Path.Combine(currentPath, Path.GetFileName(dirName)));
            
            if (!string.IsNullOrEmpty(rootPath))
            {
                if (entryName.Replace("\\", "/").IndexOf(rootPath.Replace("\\", "/"), StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
            }

            var fullPathComponents = GetPathComponents(entryName);
            var relativePathComponents = fullPathComponents.Skip(this.rootPathComponents.Length).ToArray();
            var relativePath = string.Join("/", relativePathComponents);
            
            this.nextEntries.Push(new Iso9660Entry
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
            });
        }
        
        foreach (var fileName in cdReader.GetFiles(currentPath).OrderByDescending(x => x).ToList())
        {
            var formattedFilename = Iso9660ExtensionRegex.Replace(fileName, string.Empty);
            var entryName = FormatPath(Path.Combine(currentPath, GetFileName(formattedFilename)));
            
            if (!string.IsNullOrEmpty(rootPath))
            {
                if (entryName.Replace("\\", "/").IndexOf(rootPath.Replace("\\", "/"), StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
            }

            var fullPathComponents = GetPathComponents(entryName);
            var relativePathComponents = fullPathComponents.Skip(this.rootPathComponents.Length).ToArray();
            var relativePath = string.Join("/", relativePathComponents);
            
            this.nextEntries.Push(new Iso9660Entry
            {
                Name = relativePath,
                FormattedName = relativePath,
                RawPath = relativePath,
                FullPathComponents = fullPathComponents,
                RelativePathComponents = relativePathComponents,
                IsoPath = fileName,
                Date = cdReader.GetLastWriteTime(fileName),
                Size = cdReader.GetFileLength(fileName),
                Type = Models.FileSystems.EntryType.File
            });
        }

        return Task.CompletedTask;
    }

    private static readonly Regex Iso9660ExtensionRegex =
        new Regex(";\\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private string FormatPath(string path)
    {
        return path.StartsWith("\\") ? path.Substring(1) : path;
    }
    
    private string GetFileName(string path)
    {
        return Path.GetFileName(Iso9660ExtensionRegex.Replace(path, ""));
    }
    
    public string[] GetPathComponents(string path)
    {
        return path.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);
    }

    public bool UsesFileNameMatcher => false;
}