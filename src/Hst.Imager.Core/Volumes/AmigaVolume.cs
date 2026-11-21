using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.FileSystems;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.PathComponents;
using Hst.Imager.Core.UaeMetadatas;
using Entry = Hst.Imager.Core.Models.FileSystems.Entry;
using FileMode = Hst.Amiga.FileSystems.FileMode;
using ModelEntryType = Hst.Imager.Core.Models.FileSystems.EntryType;

namespace Hst.Imager.Core.Volumes;

/// <summary>
/// Amiga volume using Amiga file system volume (Fast File System, PFS3).
/// </summary>
public class AmigaVolume : IVolume
{
    private readonly byte[] _buffer = new byte[4096];
    private readonly IFileSystemVolume _fileSystemVolume;
    private readonly bool _ownsMedia;
    private List<string> _currentPathComponents = new(10);
    private uint _currentDirectoryBlockNumber;
    private bool _currentPathKnown;
    private bool _disposed;

    /// <summary>
    /// Amiga volume.
    /// </summary>
    /// <param name="media">Media file system volume is mounted from.</param>
    /// <param name="partitionTableType">Partition table type.</param>
    /// <param name="partitionNumber">Partition number.</param>
    /// <param name="fileSystemVolume">Mounted Amiga file system volume.</param>
    /// <param name="ownsMedia">Dispose file system volume and media, when volume is disposed.</param>
    public AmigaVolume(Media media, PartitionTableType partitionTableType, int partitionNumber,
        IFileSystemVolume fileSystemVolume, bool ownsMedia = true)
    {
        Media = media;
        PartitionTableType = partitionTableType;
        PartitionNumber = partitionNumber;
        _fileSystemVolume = fileSystemVolume;
        _ownsMedia = ownsMedia;
    }

    public Media Media { get; }
    public PartitionTableType PartitionTableType { get; }
    public int PartitionNumber { get; }
    public IMediaPath MediaPath { get; } = Core.PathComponents.MediaPath.AmigaOsPath;
    public AttributesMode AttributesMode => AttributesMode.Amiga;
    public VolumeEntryOrder EntryOrder => VolumeEntryOrder.DirectoriesFirst;
    public bool IsReadOnly => false;
    public bool SupportsUaeMetadata => true;
    public UaeMetadata UaeMetadata { get; set; }

    /// <summary>
    /// Amiga file system volume.
    /// </summary>
    public IFileSystemVolume FileSystemVolume => _fileSystemVolume;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _fileSystemVolume.Flush().GetAwaiter().GetResult();

        if (Media?.Stream is { CanWrite: true })
        {
            Media.Stream.Flush();
        }

        if (_ownsMedia)
        {
            _fileSystemVolume.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Media?.Dispose();
        }

        _disposed = true;
    }

    private async Task<bool> IsDirectoryChanged(string[] pathComponents)
    {
        if (!_currentPathKnown || _currentDirectoryBlockNumber != _fileSystemVolume.CurrentDirectoryBlockNumber)
        {
            _currentPathComponents = MediaPath.Split(await _fileSystemVolume.GetCurrentPath()).ToList();
            _currentDirectoryBlockNumber = _fileSystemVolume.CurrentDirectoryBlockNumber;
            _currentPathKnown = true;
        }

        if (_currentPathComponents.Count != pathComponents.Length)
        {
            return true;
        }

        for (var i = pathComponents.Length - 1; i >= 0; i--)
        {
            if (!_currentPathComponents[i].Equals(pathComponents[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Change current directory to absolute dir path components, if current directory is different.
    /// </summary>
    private async Task<Result> ChangeDirectory(string[] dirPathComponents)
    {
        if (!await IsDirectoryChanged(dirPathComponents))
        {
            return new Result();
        }

        var isRelativeDirChange = _currentPathComponents.Count <= dirPathComponents.Length &&
                                  _currentPathComponents.SequenceEqual(
                                      dirPathComponents.Take(_currentPathComponents.Count),
                                      StringComparer.OrdinalIgnoreCase);

        if (!isRelativeDirChange)
        {
            await _fileSystemVolume.ChangeDirectory("/");
            _currentPathComponents = [];
        }

        foreach (var part in dirPathComponents.Skip(_currentPathComponents.Count).ToArray())
        {
            var entries = await _fileSystemVolume.ListEntries();

            var dirEntry = entries.FirstOrDefault(x =>
                x.Name.Equals(part, StringComparison.OrdinalIgnoreCase) && x.Type == EntryType.Dir);

            if (dirEntry == null)
            {
                _currentPathKnown = false;
                var path = MediaPath.Join(_currentPathComponents.Concat([part]).ToArray());
                return new Result(new PathNotFoundError($"Path not found '{path}'", path));
            }

            await _fileSystemVolume.ChangeDirectory(dirEntry.Name);
            _currentPathComponents.Add(dirEntry.Name);
        }

        _currentDirectoryBlockNumber = _fileSystemVolume.CurrentDirectoryBlockNumber;

        return new Result();
    }

    private async Task<Hst.Amiga.FileSystems.Entry> FindEntryInCurrentDirectory(string name) =>
        (await _fileSystemVolume.ListEntries()).FirstOrDefault(x =>
            x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private PathNotFoundError CreatePathNotFoundError(string[] pathComponents)
    {
        var path = MediaPath.Join(pathComponents);
        return new PathNotFoundError($"Path not found '{path}'", path);
    }

    private Entry CreateEntry(string[] dirPathComponents, Hst.Amiga.FileSystems.Entry entry)
    {
        var fullPathComponents = dirPathComponents.Concat([entry.Name]).ToArray();

        var properties = new Dictionary<string, string>
        {
            { Constants.EntryPropertyNames.Comment, entry.Comment },
            { Constants.EntryPropertyNames.ProtectionBits, ((int)entry.ProtectionBits ^ 0xf).ToString() }
        };

        if (entry.Type is EntryType.DirLink or EntryType.FileLink && !string.IsNullOrEmpty(entry.LinkPath))
        {
            properties.Add(Constants.EntryPropertyNames.Link, entry.LinkPath);
        }

        return new Entry
        {
            Name = entry.Name,
            FormattedName = entry.Name,
            RawPath = MediaPath.Join(fullPathComponents),
            FullPathComponents = fullPathComponents,
            RelativePathComponents = [entry.Name],
            Date = entry.Date,
            Size = entry.Size,
            Type = entry.Type switch
            {
                EntryType.Dir => ModelEntryType.Dir,
                EntryType.DirLink => ModelEntryType.LinkDir,
                EntryType.FileLink => ModelEntryType.LinkFile,
                _ => ModelEntryType.File
            },
            Properties = properties
        };
    }

    public async Task<Result<Entry>> GetEntry(string[] pathComponents)
    {
        if (pathComponents.Length == 0)
        {
            return new Result<Entry>(VolumeEntryHelper.CreateRootEntry());
        }

        var dirPathComponents = pathComponents[..^1];
        var changeDirectoryResult = await ChangeDirectory(dirPathComponents);
        if (changeDirectoryResult.IsFaulted)
        {
            return new Result<Entry>(CreatePathNotFoundError(pathComponents));
        }

        var entry = await FindEntryInCurrentDirectory(pathComponents[^1]);

        return entry == null
            ? new Result<Entry>(CreatePathNotFoundError(pathComponents))
            : new Result<Entry>(CreateEntry(dirPathComponents, entry));
    }

    public async Task<Result<IEnumerable<Entry>>> ListEntries(string[] dirPathComponents)
    {
        var changeDirectoryResult = await ChangeDirectory(dirPathComponents);
        if (changeDirectoryResult.IsFaulted)
        {
            return new Result<IEnumerable<Entry>>(changeDirectoryResult.Error);
        }

        var entries = (await _fileSystemVolume.ListEntries())
            .Select(x => CreateEntry(dirPathComponents, x))
            .ToList();

        return new Result<IEnumerable<Entry>>(entries);
    }

    public async Task<Result<Stream>> OpenFile(string[] pathComponents)
    {
        if (pathComponents.Length == 0)
        {
            return new Result<Stream>(new Error("Root directory can't be opened as a file"));
        }

        var changeDirectoryResult = await ChangeDirectory(pathComponents[..^1]);
        if (changeDirectoryResult.IsFaulted)
        {
            return new Result<Stream>(changeDirectoryResult.Error);
        }

        var entry = await FindEntryInCurrentDirectory(pathComponents[^1]);
        if (entry == null)
        {
            return new Result<Stream>(CreatePathNotFoundError(pathComponents));
        }

        return new Result<Stream>(await _fileSystemVolume.OpenFile(entry.Name, FileMode.Read, false, true));
    }

    public async Task<Result> CreateDirectory(string[] pathComponents)
    {
        if (pathComponents.Length == 0)
        {
            return new Result();
        }

        var changeDirectoryResult = await ChangeDirectory(pathComponents[..^1]);
        if (changeDirectoryResult.IsFaulted)
        {
            return changeDirectoryResult;
        }

        var name = pathComponents[^1];
        var entry = await FindEntryInCurrentDirectory(name);

        if (entry != null && entry.Type != EntryType.Dir)
        {
            return new Result(new PathExistsError(
                $"Path already exists with same name '{MediaPath.Join(pathComponents)}'"));
        }

        if (entry == null)
        {
            await _fileSystemVolume.CreateDirectory(name);
        }

        return new Result();
    }

    public async Task<Result> WriteFile(string[] pathComponents, Stream stream, bool overwrite)
    {
        if (pathComponents.Length == 0)
        {
            return new Result(new Error("Root directory can't be written as a file"));
        }

        var changeDirectoryResult = await ChangeDirectory(pathComponents[..^1]);
        if (changeDirectoryResult.IsFaulted)
        {
            return changeDirectoryResult;
        }

        var fileName = pathComponents[^1];
        var entry = await FindEntryInCurrentDirectory(fileName);

        if (entry != null && entry.Type == EntryType.Dir)
        {
            return new Result(new PathExistsError(
                $"Path already exists as a directory '{MediaPath.Join(pathComponents)}'"));
        }

        if (entry != null && !overwrite)
        {
            return new Result(new FileExistsError($"File already exists '{MediaPath.Join(pathComponents)}'"));
        }

        await _fileSystemVolume.CreateFile(fileName, true, true);

        await using var entryStream = await _fileSystemVolume.OpenFile(fileName, FileMode.Append, true, true);

        // continue until bytes read is 0. reads from zip streams can return bytes between 0 to buffer length.
        int bytesRead;
        do
        {
            bytesRead = await stream.ReadAsync(_buffer, 0, _buffer.Length);
            await entryStream.WriteAsync(_buffer, 0, bytesRead);
        } while (bytesRead != 0);

        return new Result();
    }

    public async Task<Result> SetEntryInfo(string[] pathComponents, Entry entry, bool skipAttributes)
    {
        if (pathComponents.Length == 0)
        {
            return new Result();
        }

        var changeDirectoryResult = await ChangeDirectory(pathComponents[..^1]);
        if (changeDirectoryResult.IsFaulted)
        {
            return changeDirectoryResult;
        }

        var name = pathComponents[^1];

        if (!skipAttributes && entry.Properties.TryGetValue(Constants.EntryPropertyNames.ProtectionBits,
                out var protectionBitsProperty))
        {
            if (!int.TryParse(protectionBitsProperty, out var protectionBitsValue))
            {
                protectionBitsValue = 0;
            }

            await _fileSystemVolume.SetProtectionBits(name,
                ProtectionBitsConverter.ToProtectionBits(protectionBitsValue));
        }

        if (entry.Properties.TryGetValue(Constants.EntryPropertyNames.Comment, out var commentProperty) &&
            !string.IsNullOrWhiteSpace(commentProperty))
        {
            await _fileSystemVolume.SetComment(name, commentProperty);
        }

        if (entry.Date.HasValue)
        {
            await _fileSystemVolume.SetDate(name, entry.Date.Value);
        }

        return new Result();
    }

    public async Task<Result<int>> SetAttributes(string[] pathComponents, AttributesChange attributesChange)
    {
        if (attributesChange.Mode != AttributesMode.Amiga)
        {
            return new Result<int>(new Error(
                $"Attributes mode '{attributesChange.Mode}' is not supported for Amiga volume, use Amiga protection bits"));
        }

        if (pathComponents.Length == 0)
        {
            return new Result<int>(new Error("Attributes can't be set for root directory of Amiga volume"));
        }

        var changeDirectoryResult = await ChangeDirectory(pathComponents[..^1]);
        if (changeDirectoryResult.IsFaulted)
        {
            return new Result<int>(changeDirectoryResult.Error);
        }

        var entry = await FindEntryInCurrentDirectory(pathComponents[^1]);
        if (entry == null)
        {
            return new Result<int>(CreatePathNotFoundError(pathComponents));
        }

        var protectionBits = attributesChange.Apply((int)entry.ProtectionBits);

        await _fileSystemVolume.SetProtectionBits(entry.Name, (ProtectionBits)protectionBits);

        return new Result<int>(protectionBits);
    }

    public async Task<Result> CreateLink(string[] pathComponents, string linkPath)
    {
        if (pathComponents.Length == 0)
        {
            return new Result(new Error("Link can't be created for root directory"));
        }

        var changeDirectoryResult = await ChangeDirectory(pathComponents[..^1]);
        if (changeDirectoryResult.IsFaulted)
        {
            return changeDirectoryResult;
        }

        await _fileSystemVolume.CreateLink(pathComponents[^1], linkPath);

        return new Result();
    }

    public async Task<Result> Delete(string[] pathComponents)
    {
        if (pathComponents.Length == 0)
        {
            return new Result(new Error("Root directory can't be deleted"));
        }

        var changeDirectoryResult = await ChangeDirectory(pathComponents[..^1]);
        if (changeDirectoryResult.IsFaulted)
        {
            return changeDirectoryResult;
        }

        await _fileSystemVolume.Delete(pathComponents[^1], true);

        return new Result();
    }

    public async Task<Result> Move(string[] srcPathComponents, string[] destPathComponents, bool overwrite)
    {
        if (srcPathComponents.Length == 0 || destPathComponents.Length == 0)
        {
            return new Result(new Error("Root directory can't be moved"));
        }

        // check if destination exists
        var destEntryResult = await GetEntry(destPathComponents);
        if (destEntryResult.IsSuccess)
        {
            if (!overwrite)
            {
                return new Result(new PathExistsError(
                    $"Destination path '{MediaPath.Join(destPathComponents)}' already exists"));
            }

            if (destEntryResult.Value.Type == ModelEntryType.Dir)
            {
                return new Result(new PathExistsError(
                    $"Can't overwrite destination path '{MediaPath.Join(destPathComponents)}' because it is a directory"));
            }

            await _fileSystemVolume.Delete(destEntryResult.Value.Name, true);
        }

        var changeDirectoryResult = await ChangeDirectory(srcPathComponents[..^1]);
        if (changeDirectoryResult.IsFaulted)
        {
            return changeDirectoryResult;
        }

        var srcEntry = await FindEntryInCurrentDirectory(srcPathComponents[^1]);
        if (srcEntry == null)
        {
            var srcPath = MediaPath.Join(srcPathComponents);
            return new Result(new PathNotFoundError($"Source path '{srcPath}' not found", srcPath));
        }

        var destEntryPath = string.Concat(MediaPath.PathSeparator, MediaPath.Join(destPathComponents));
        await _fileSystemVolume.Rename(srcEntry.Name, destEntryPath);

        return new Result();
    }

    public bool IsSameVolume(IVolume other) => VolumeEntryHelper.IsSameMediaAndPartition(this, other);

    public Task Flush() => _fileSystemVolume.Flush();

    public IEnumerable<string> GetDebugLogs() => _fileSystemVolume.GetStatus().ToList();

    public IEnumerable<string> GetLogs() => [];
}
