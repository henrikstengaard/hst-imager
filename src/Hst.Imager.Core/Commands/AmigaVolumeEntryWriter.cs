﻿namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Hst.Imager.Core.UaeMetadatas;
using Models;
using Entry = Models.FileSystems.Entry;
using FileMode = Amiga.FileSystems.FileMode;

public class AmigaVolumeEntryWriter : IEntryWriter
{
    private readonly bool isWindowsOperatingSystem;
    private readonly IList<string> logs;
    private readonly byte[] buffer;
    private readonly Media media;
    private readonly string[] pathComponents;
    private readonly IFileSystemVolume fileSystemVolume;
    private string[] currentPathComponents;
    private bool disposed;

    public AmigaVolumeEntryWriter(Media media, string fileSystemPath, string[] pathComponents, IFileSystemVolume fileSystemVolume)
    {
        this.isWindowsOperatingSystem = OperatingSystem.IsWindows();
        this.logs = new List<string>();
        this.buffer = new byte[4096];
        this.media = media;
        this.FileSystemPath = fileSystemPath;
        this.pathComponents = pathComponents;
        this.fileSystemVolume = fileSystemVolume;
        this.currentPathComponents = Array.Empty<string>();
    }

    public string MediaPath => this.media.Path;
    public string FileSystemPath { get; }

    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            fileSystemVolume.Flush().GetAwaiter().GetResult();
            if (media.Stream.CanRead)
            {
                media.Stream.Flush();
            }
        }

        disposed = true;
    }

    public void Dispose() => Dispose(true);

    public async Task CreateDirectory(Entry entry, string[] entryPathComponents, bool skipAttributes)
    {
        await fileSystemVolume.ChangeDirectory("/");

        var fullPathComponents = pathComponents.Concat(entryPathComponents).ToArray();

        for (var i = 0; i < fullPathComponents.Length; i++)
        {
            var part = fullPathComponents[i];

            IEnumerable<Hst.Amiga.FileSystems.Entry> entries = (await fileSystemVolume.ListEntries()).ToList();

            var dirEntry = entries.FirstOrDefault(x =>
                x.Name.Equals(part, StringComparison.OrdinalIgnoreCase) && x.Type == EntryType.Dir);

            if (dirEntry == null)
            {
                await fileSystemVolume.CreateDirectory(part);
            }

            if (i == fullPathComponents.Length - 1)
            {
                if (!skipAttributes && entry.Properties.ContainsKey(Core.Constants.EntryPropertyNames.ProtectionBits))
                {
                    if (!int.TryParse(entry.Properties[Core.Constants.EntryPropertyNames.ProtectionBits], out var protectionBitsValue))
                    {
                        protectionBitsValue = 0;
                    }

                    var protectionBits = ProtectionBitsConverter.ToProtectionBits(protectionBitsValue);
                    await fileSystemVolume.SetProtectionBits(part, protectionBits);
                }

                if (entry.Date.HasValue)
                {
                    await fileSystemVolume.SetDate(part, entry.Date.Value);
                }

                if (entry.Properties.ContainsKey(Core.Constants.EntryPropertyNames.Comment) &&
                    !string.IsNullOrWhiteSpace(entry.Properties[Core.Constants.EntryPropertyNames.Comment]))
                {
                    await fileSystemVolume.SetComment(part, entry.Properties[Core.Constants.EntryPropertyNames.Comment]);
                }
            }

            await fileSystemVolume.ChangeDirectory(part);
        }

        currentPathComponents = fullPathComponents;
    }

    public async Task WriteEntry(Entry entry, string[] entryPathComponents, Stream stream, bool skipAttributes)
    {
        var fullPathComponents = pathComponents.Concat(entryPathComponents).ToArray();
        var fileName = fullPathComponents[^1];

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var windowsReservedName = string.IsNullOrEmpty(fileNameWithoutExtension) ? fileName : fileNameWithoutExtension;

        if (isWindowsOperatingSystem && windowsReservedName.StartsWith(".") &&
            Regexs.WindowsReservedNamesRegex.IsMatch(windowsReservedName.Substring(1)))
        {
            fileName = fileName.Substring(1);
            logs.Add($"{(string.Join("/", entryPathComponents))} -> {fileName}");
        }

        var directoryChanged = currentPathComponents.Length != fullPathComponents.Length - 1;
        if (!directoryChanged)
        {
            for (var i = fullPathComponents.Length - 2; i >= 0; i--)
            {
                if (currentPathComponents[i] == fullPathComponents[i])
                {
                    continue;
                }

                directoryChanged = true;
                break;
            }
        }

        if (directoryChanged)
        {
            await fileSystemVolume.ChangeDirectory("/");

            for (var i = 0; i < fullPathComponents.Length - 1; i++)
            {
                var entries = (await fileSystemVolume.ListEntries()).ToList();

                if (!entries.Any(x => x.Name.Equals(fullPathComponents[i], StringComparison.OrdinalIgnoreCase)))
                {
                    await fileSystemVolume.CreateDirectory(fullPathComponents[i]);
                }

                await fileSystemVolume.ChangeDirectory(fullPathComponents[i]);
            }

            currentPathComponents = fullPathComponents.Take(fullPathComponents.Length - 1).ToArray();
        }

        await fileSystemVolume.CreateFile(fileName, true, true);

        await using (var entryStream = await fileSystemVolume.OpenFile(fileName, FileMode.Append, true))
        {
            int bytesRead;
            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                await entryStream.WriteAsync(buffer, 0, bytesRead);
            } while
                (bytesRead !=
                 0); // continue until bytes read is 0. reads from zip streams can return bytes between 0 to buffer length. 
        }

        if (!skipAttributes && entry.Properties.ContainsKey(Core.Constants.EntryPropertyNames.ProtectionBits))
        {
            if (!int.TryParse(entry.Properties[Core.Constants.EntryPropertyNames.ProtectionBits], out var protectionBitsValue))
            {
                protectionBitsValue = 0;
            }

            var protectionBits = ProtectionBitsConverter.ToProtectionBits(protectionBitsValue);
            await fileSystemVolume.SetProtectionBits(fileName, protectionBits);
        }

        if (entry.Properties.ContainsKey(Core.Constants.EntryPropertyNames.Comment) &&
            !string.IsNullOrWhiteSpace(entry.Properties[Core.Constants.EntryPropertyNames.Comment]))
        {
            await fileSystemVolume.SetComment(fileName, entry.Properties[Core.Constants.EntryPropertyNames.Comment]);
        }

        if (entry.Date.HasValue)
        {
            await fileSystemVolume.SetDate(fileName, entry.Date.Value);
        }
    }

    public IEnumerable<string> GetDebugLogs()
    {
        return fileSystemVolume.GetStatus().ToList();
    }

    public IEnumerable<string> GetLogs()
    {
        return this.logs.Count == 0
            ? new List<string>()
            : new[]
            {
                string.Empty,
                "Following files were renamed to restore filenames previously conflicted with Windows OS reserved filenames:"
            }.Concat(logs);
    }

    public IEntryIterator CreateEntryIterator(string rootPath, bool recursive)
    {
        return new AmigaVolumeEntryIterator(media.Stream, rootPath, fileSystemVolume, recursive);
    }

    public UaeMetadata UaeMetadata { get; set; }

    public async Task Flush()
    {
        await this.fileSystemVolume.Flush();
    }
}