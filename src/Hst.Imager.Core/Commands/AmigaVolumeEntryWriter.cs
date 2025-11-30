using Hst.Core;
using Hst.Imager.Core.PathComponents;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using UaeMetadatas;
using Models;
using Entry = Models.FileSystems.Entry;
using FileMode = Amiga.FileSystems.FileMode;

public class AmigaVolumeEntryWriter(
    Media media,
    PartitionTableType partitionTableType,
    int partitionNumber,
    string fileSystemPath,
    string[] rootPathComponents,
    bool recursive,
    IFileSystemVolume fileSystemVolume,
    bool createDirectory,
    bool forceOverwrite)
    : IEntryWriter
{
    private readonly byte[] buffer = new byte[4096];

    /// <summary>
    /// dir path components after initialized
    /// is only used when creating single file
    /// </summary>
    private string[] dirPathComponents = [];
    private bool lastPathComponentExist = true;
    private Models.FileSystems.EntryType lastPathComponentEntryType = Models.FileSystems.EntryType.Dir;
    private bool isInitialized;
    
    private readonly IMediaPath mediaPath = PathComponents.MediaPath.AmigaOsPath;

    private List<string> currentPathComponents = new(10);
    private uint currentDirectoryBlockNumber = fileSystemVolume.CurrentDirectoryBlockNumber;
    private bool disposed;

    public string MediaPath => media.Path;
    public string FileSystemPath { get; } = fileSystemPath;

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

    public async Task<Result> Initialize()
    {
        await fileSystemVolume.ChangeDirectory("/");

        var exisingPathComponents = new List<string>(10);

        lastPathComponentExist = true;

        for (var i = 0; i < rootPathComponents.Length; i++)
        {
            var entries = (await fileSystemVolume.ListEntries()).ToList();

            var pathComponentEntry = entries.FirstOrDefault(x =>
                x.Name.Equals(rootPathComponents[i], StringComparison.OrdinalIgnoreCase));

            var nextDirPath = string.Join("/", rootPathComponents.Take(i + 1));
            
            if (pathComponentEntry != null && pathComponentEntry.Type == EntryType.File && i < rootPathComponents.Length - 1)
            {
                return new Result(new PathNotFoundError($"Path '{nextDirPath}' is a file and not a directory", nextDirPath));
            }

            if (pathComponentEntry == null)
            {
                if (!createDirectory)
                {
                    if (i != rootPathComponents.Length - 1)
                    {
                        return new Result(new PathNotFoundError($"Path not found '{nextDirPath}'", nextDirPath));
                    }
                
                    lastPathComponentExist = false;
                
                    break;
                }
                
                await fileSystemVolume.CreateDirectory(rootPathComponents[i]);
                
                await fileSystemVolume.ChangeDirectory(rootPathComponents[i]);
            }
            else
            {
                if (pathComponentEntry.Type != EntryType.File)
                {
                    await fileSystemVolume.ChangeDirectory(rootPathComponents[i]);
                }
                
                if (i == rootPathComponents.Length - 1)
                {
                    lastPathComponentEntryType = pathComponentEntry.Type == EntryType.Dir
                        ? Models.FileSystems.EntryType.Dir : Models.FileSystems.EntryType.File;

                    if (pathComponentEntry.Type != EntryType.Dir)
                    {
                        break;
                    }
                }
            }
            
            exisingPathComponents.Add(rootPathComponents[i]);
        }
        
        if (recursive && !lastPathComponentExist)
        {
            var path = string.Join("/", rootPathComponents);
            return new Result(new PathNotFoundError($"Path '{path}' not found. Directory must exist when using recursive!", path));
        }
        
        dirPathComponents = exisingPathComponents.ToArray();

        currentPathComponents = dirPathComponents.ToList();
        currentDirectoryBlockNumber = fileSystemVolume.CurrentDirectoryBlockNumber;

        isInitialized = true;
        
        return new Result();
    }

    private async Task<bool> IsDirectoryChanged(string[] fullPathComponents)
    {
        if (currentDirectoryBlockNumber != fileSystemVolume.CurrentDirectoryBlockNumber)
        {
            currentPathComponents = mediaPath.Split(await fileSystemVolume.GetCurrentPath()).ToList();
            currentDirectoryBlockNumber = fileSystemVolume.CurrentDirectoryBlockNumber;
        }
        
        if (currentPathComponents.Count != fullPathComponents.Length)
        {
            return true;
        }
        
        for (var i = fullPathComponents.Length - 1; i >= 0; i--)
        {
            if (currentPathComponents[i] == fullPathComponents[i])
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Changes the current directory to the specified absolute path components.
    /// </summary>
    /// <param name="existingAbsolutePathComponents">Absolute path components that must exist.</param>
    /// <param name="absolutePathComponents">Absolute path components to change to.</param>
    /// <returns></returns>
    private async Task<Result> ChangeDirectoryIfNeeded(string[] existingAbsolutePathComponents, string[] absolutePathComponents)
    {
        if (!await IsDirectoryChanged(absolutePathComponents))
        {
            return new Result();
        }

        var isRelativeDirChange = currentPathComponents.Count <= absolutePathComponents.Length &&
                       currentPathComponents.SequenceEqual(absolutePathComponents.Take(currentPathComponents.Count));

        if (!isRelativeDirChange)
        {
            await fileSystemVolume.ChangeDirectory("/");
            currentDirectoryBlockNumber = fileSystemVolume.CurrentDirectoryBlockNumber;

            currentPathComponents = [];
        }

        var pathComponents = isRelativeDirChange
            ? absolutePathComponents.Skip(currentPathComponents.Count).ToArray()
            : absolutePathComponents;
        
        for (var i = 0; i < pathComponents.Length; i++)
        {
            var part = pathComponents[i];
            var entries = (await fileSystemVolume.ListEntries()).ToList();

            var mustPathComponentExist = currentPathComponents.Count + i < existingAbsolutePathComponents.Length;
            
            var dirEntry = entries.FirstOrDefault(x =>
                x.Name.Equals(part, StringComparison.OrdinalIgnoreCase) && x.Type == EntryType.Dir);

            if (dirEntry == null)
            {
                if (mustPathComponentExist)
                {
                    return new Result(new PathNotFoundError($"Path not found '{part}'", 
                        string.Join("/", currentPathComponents.Concat([part]))));
                }

                await fileSystemVolume.CreateDirectory(part);
            }

            await fileSystemVolume.ChangeDirectory(part);
            currentPathComponents.Add(part);
            currentDirectoryBlockNumber = fileSystemVolume.CurrentDirectoryBlockNumber;
        }

        return new Result();
    }
    
    public async Task<Result> CreateDirectory(Entry entry, string[] entryPathComponents, bool skipAttributes,
        bool isSingleFileEntry)
    {
        if (!isInitialized)
        {
            return new Result(new Error("AmigaVolumeEntryWriter is not initialized."));
        }
        
        var fullPathComponents = PathComponentHelper.GetFullPathComponents(entry.Type, entryPathComponents,
            lastPathComponentEntryType, rootPathComponents, lastPathComponentExist, isSingleFileEntry);

        if (fullPathComponents.Length == 0)
        {
            return new Result();
        }
        
        var name = fullPathComponents[^1];

        var requiredPathComponentsToExist = isSingleFileEntry ? dirPathComponents : rootPathComponents;

        var changeDirectoryResult = await ChangeDirectoryIfNeeded(requiredPathComponentsToExist, fullPathComponents.Take(fullPathComponents.Length - 1).ToArray());
        if (changeDirectoryResult.IsFaulted)
        {
            return changeDirectoryResult;
        }
        
        IEnumerable<Hst.Amiga.FileSystems.Entry> entries = (await fileSystemVolume.ListEntries()).ToList();

        var dirEntry = entries.FirstOrDefault(x =>
            x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && x.Type == EntryType.Dir);
        
        if (dirEntry == null)
        {
            await fileSystemVolume.CreateDirectory(name);
        }
        
        if (!skipAttributes && entry.Properties.TryGetValue(Core.Constants.EntryPropertyNames.ProtectionBits, 
                out var protectionBitsProperty))
        {
            if (!int.TryParse(protectionBitsProperty, out var protectionBitsValue))
            {
                protectionBitsValue = 0;
            }

            var protectionBits = ProtectionBitsConverter.ToProtectionBits(protectionBitsValue);
            await fileSystemVolume.SetProtectionBits(name, protectionBits);
        }

        if (entry.Date.HasValue)
        {
            await fileSystemVolume.SetDate(name, entry.Date.Value);
        }

        if (entry.Properties.TryGetValue(Core.Constants.EntryPropertyNames.Comment, out var commentProperty) &&
            !string.IsNullOrWhiteSpace(commentProperty))
        {
            await fileSystemVolume.SetComment(name, commentProperty);
        }

        return new Result();
    }

    public async Task<Result> CreateFile(Entry entry, string[] entryPathComponents, Stream stream, bool skipAttributes,
        bool isSingleFileEntry)
    {
        if (!isInitialized)
        {
            return new Result(new Error("AmigaVolumeEntryWriter is not initialized."));
        }

        var fullPathComponents = PathComponentHelper.GetFullPathComponents(entry.Type, entryPathComponents,
            lastPathComponentEntryType, rootPathComponents, lastPathComponentExist, isSingleFileEntry);

        var requiredPathComponentsToExist = isSingleFileEntry ? dirPathComponents : rootPathComponents;

        var changeDirectoryResult = await ChangeDirectoryIfNeeded(requiredPathComponentsToExist, fullPathComponents.Take(fullPathComponents.Length - 1).ToArray());
        if (changeDirectoryResult.IsFaulted)
        {
            return changeDirectoryResult;
        }

        var fileName = fullPathComponents[^1];

        var entries = (await fileSystemVolume.ListEntries()).ToList();
        
        var fileEntry = entries.FirstOrDefault(x =>
            x.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase) && x.Type == EntryType.File);
        
        if (!forceOverwrite && fileEntry != null)
        {
            return new Result(new FileExistsError($"File already exists '{string.Join("/", fullPathComponents)}'"));
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

        if (!skipAttributes && entry.Properties.TryGetValue(Core.Constants.EntryPropertyNames.ProtectionBits,
                out var protectionBitsProperty))
        {
            if (!int.TryParse(protectionBitsProperty, out var protectionBitsValue))
            {
                protectionBitsValue = 0;
            }

            var protectionBits = ProtectionBitsConverter.ToProtectionBits(protectionBitsValue);
            await fileSystemVolume.SetProtectionBits(fileName, protectionBits);
        }

        if (entry.Properties.TryGetValue(Core.Constants.EntryPropertyNames.Comment, out var commentProperty) &&
            !string.IsNullOrWhiteSpace(commentProperty))
        {
            await fileSystemVolume.SetComment(fileName, commentProperty);
        }

        if (entry.Date.HasValue)
        {
            await fileSystemVolume.SetDate(fileName, entry.Date.Value);
        }

        return new Result();
    }

    public IEnumerable<string> GetDebugLogs()
    {
        return fileSystemVolume.GetStatus().ToList();
    }

    public IEnumerable<string> GetLogs() => [];

    public IEntryIterator CreateEntryIterator(string[] rootPathComponents, bool recursive)
    {
        return new AmigaVolumeEntryIterator(media, partitionTableType, partitionNumber, fileSystemVolume,
            rootPathComponents, recursive);
    }

    private bool IsSameMediaAndPartition(IEntryIterator entryIterator) =>
        entryIterator.Media != null && media.Equals(entryIterator.Media) &&
        entryIterator.PartitionTableType == partitionTableType &&
        entryIterator.PartitionNumber == partitionNumber;

    /// <summary>
    /// Examines if path components used by iterator and writer results in a self copy.
    /// 
    /// Examples of self copy paths:
    /// - dir1 -> dir1: Copy all files from dir1 to dir1.
    /// - dir1\* -> dir1: Copy all files from dir1 to dir1, same as not writing *.
    /// - dir1\file1.txt -> dir1: Copy single file file1.txt from dir1 to dir1.
    ///
    /// Examples of valid paths that are not self copy:
    /// - dir1\file1.txt -> dir2: Copy file1.txt from dir1 to dir2.
    /// - dir1\file1.txt -> dir1\file2.txt: Copy file1.txt from dir1 to file2.txt in dir1.
    /// </summary>
    /// <param name="entryIterator"></param>
    /// <returns>True, if iterator and writer path components result in a self copy.</returns>
    public bool ArePathComponentsSelfCopy(IEntryIterator entryIterator)
    {
        // return false, if not an amiga volume entry iterator or not same media.
        // self copy/extract is only possible, when copying/extracting from and to same media
        if (entryIterator is not AmigaVolumeEntryIterator ||
            !IsSameMediaAndPartition(entryIterator))
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
        // return false, if not an amiga volume entry iterator and not same media.
        // cyclic copy/extract are only possible, when copying/extracting from and to same media
        if (entryIterator is not AmigaVolumeEntryIterator ||
            (entryIterator.Media != null && !media.Equals(entryIterator.Media)))
        {
            return false;
        }

        // return false, if not recursive
        if (!recursive && entryIterator.IsSingleFileEntryNext)
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

    public UaeMetadata UaeMetadata { get; set; }

    public async Task Flush()
    {
        await fileSystemVolume.Flush();
    }
}