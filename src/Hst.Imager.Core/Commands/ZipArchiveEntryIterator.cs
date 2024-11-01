namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Hst.Imager.Core.Compressions.Zip;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.PathComponents;
using Hst.Imager.Core.UaeMetadatas;
using Entry = Models.FileSystems.Entry;
using EntryType = Models.FileSystems.EntryType;

public class ZipArchiveEntryIterator : IEntryIterator
{
    private readonly Stream stream;
    private readonly IMediaPath mediaPath;
    private readonly string rootPath;
    private string[] rootPathComponents;
    private PathComponentMatcherV3 pathComponentMatcher;
    private readonly ZipArchive zipArchive;
    private readonly bool recursive;
    private readonly Stack<Entry> nextEntries;
    private bool isFirst;
    private Entry currentEntry;
    private bool disposed;
    private readonly IDictionary<string, ZipArchiveEntry> zipEntryIndex;

    public ZipArchiveEntryIterator(Stream stream, string rootPath, ZipArchive zipArchive, bool recursive)
    {
        this.stream = stream;
        this.mediaPath = MediaPath.ZipArchivePath;
        this.rootPath = rootPath;
        this.zipArchive = zipArchive;
        this.recursive = recursive;
        this.nextEntries = new Stack<Entry>();
        this.currentEntry = null;
        this.isFirst = true;
        this.zipEntryIndex = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);

        var pathComponents = GetPathComponents(rootPath);
        var hasPattern = pathComponents.Length > 0 &&
            pathComponents[^1].IndexOf("*", StringComparison.OrdinalIgnoreCase) >= 0;
        this.rootPathComponents =
            hasPattern ? pathComponents.Take(pathComponents.Length - 1).ToArray() : pathComponents;
        this.pathComponentMatcher = new PathComponentMatcherV3(pathComponents, recursive);
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
            await EnqueueEntries();
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
        if (!zipEntryIndex.ContainsKey(entry.RawPath))
        {
            throw new IOException($"Entry '{entry.RawPath}' not found");
        }

        return Task.FromResult(zipEntryIndex[entry.RawPath].Open());
    }

    public string[] GetPathComponents(string path) => mediaPath.Split(path);

    private async IAsyncEnumerable<CentralDirectoryFileHeader> ReadCentralDirectoryFileHeaders()
    {
        stream.Seek(0, SeekOrigin.Begin);
        var zipArchiveReader = new ZipArchiveReader(stream);

        IZipHeader zipHeader;
        while ((zipHeader = await zipArchiveReader.Read()) != null)
        {
            if (zipHeader is CentralDirectoryFileHeader centralDirectoryFileHeader)
            {
                yield return centralDirectoryFileHeader;
            }
        }
    }

    private async Task EnqueueEntries()
    {
        var centralDirectoryFileHeaderIndex = new Dictionary<string, CentralDirectoryFileHeader>();
        await foreach (var centralDirectoryFileHeader in ReadCentralDirectoryFileHeaders())
        {
            centralDirectoryFileHeaderIndex[centralDirectoryFileHeader.FileName] = centralDirectoryFileHeader;
        }

        var zipEntries = this.zipArchive.Entries.OrderBy(x => x.FullName).ToList();

        var uniqueEntries = new Dictionary<string, Entry>();

        for (var i = zipEntries.Count - 1; i >= 0; i--)
        {
            var zipEntry = zipEntries[i];

            var entryPath = GetEntryName(zipEntry.FullName);

            zipEntryIndex.Add(entryPath, zipEntry);

            var isDir = zipEntry.FullName.EndsWith("/");

            var centralDirectoryFileHeader = centralDirectoryFileHeaderIndex.ContainsKey(zipEntry.FullName)
                ? centralDirectoryFileHeaderIndex[zipEntry.FullName]
                : null;

            var attributes = GetAttributes(centralDirectoryFileHeader);
            var properties = GetProperties(centralDirectoryFileHeader);

            var dirAttributes = EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(0));

            var entries = EntryIteratorFunctions.CreateEntries(mediaPath, pathComponentMatcher, rootPathComponents,
                recursive, entryPath, entryPath, isDir, zipEntry.LastWriteTime.LocalDateTime, zipEntry.Length,
                attributes, properties, dirAttributes).ToList();

            foreach (var entry in entries)
            {
                if (entry.Type == EntryType.Dir && rootPath.Equals(entry.RawPath))
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
    }

    private static string GetAttributes(CentralDirectoryFileHeader centralDirectoryFileHeader)
    {
        if (centralDirectoryFileHeader == null)
        {
            return string.Empty;
        }

        var hostOs = (HostOsFlags)centralDirectoryFileHeader.HostOs;
        switch(hostOs)
        {
            case HostOsFlags.MsDos:
                return FileAttributesFormatter.FormatMsDosAttributes((int)centralDirectoryFileHeader.ExternalFileAttributes);
            case HostOsFlags.Amiga:
                var protectionBitsValue = (int)((centralDirectoryFileHeader.ExternalFileAttributes >> 16) & 0xff);
                return EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(protectionBitsValue ^ 0xf));
            default:
                return string.Empty;
        }
    }

    private static IDictionary<string, string> GetProperties(CentralDirectoryFileHeader centralDirectoryFileHeader)
    {
        var properties = new Dictionary<string, string>();

        if (centralDirectoryFileHeader == null)
        {
            return properties;
        }

        if (!string.IsNullOrEmpty(centralDirectoryFileHeader.FileComment))
        {
            properties["Comment"] = centralDirectoryFileHeader.FileComment;
        }

        var hostOs = (HostOsFlags)centralDirectoryFileHeader.HostOs;
        if (hostOs == HostOsFlags.Amiga)
        {
            var protectionBitsValue = (int)((centralDirectoryFileHeader.ExternalFileAttributes >> 16) & 0xff);
            properties["ProtectionBits"] = (protectionBitsValue ^ 0xf).ToString();
        }

        return properties;
    }

    private string GetEntryName(string name)
    {
        return name.EndsWith("/") ? name[..^1] : name;
    }
    
    public bool UsesPattern => this.pathComponentMatcher?.UsesPattern ?? false;
    
    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public UaeMetadata UaeMetadata { get; set; }
}