using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Ext;
using DiscUtils.Fat;
using DiscUtils.Iso9660;
using DiscUtils.Ntfs;
using Hst.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.FileSystems;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.PathComponents;
using Hst.Imager.Core.UaeMetadatas;
using Entry = Hst.Imager.Core.Models.FileSystems.Entry;
using EntryType = Hst.Imager.Core.Models.FileSystems.EntryType;

namespace Hst.Imager.Core.Volumes;

/// <summary>
/// Volume using DiscUtils file system (FAT, exFAT, NTFS, ext, ISO9660, UDF).
/// </summary>
public partial class DiscUtilsVolume : IVolume
{
    private const string PathSeparator = "\\";
    private readonly byte[] _buffer = new byte[4096];
    private readonly IFileSystem _fileSystem;
    private readonly bool _ownsMedia;
    private readonly bool _isIso9660;
    private bool _disposed;

    /// <summary>
    /// DiscUtils volume.
    /// </summary>
    /// <param name="media">Media file system is mounted from. Can be null.</param>
    /// <param name="partitionTableType">Partition table type.</param>
    /// <param name="partitionNumber">Partition number.</param>
    /// <param name="fileSystem">Mounted file system.</param>
    /// <param name="ownsMedia">Dispose file system and media, when volume is disposed.</param>
    public DiscUtilsVolume(Media media, PartitionTableType partitionTableType, int partitionNumber,
        IFileSystem fileSystem, bool ownsMedia = true)
    {
        Media = media;
        PartitionTableType = partitionTableType;
        PartitionNumber = partitionNumber;
        _fileSystem = fileSystem;
        _ownsMedia = ownsMedia;
        _isIso9660 = fileSystem is CDReader;
        MediaPath = _isIso9660 ? Core.PathComponents.MediaPath.Iso9660Path : Core.PathComponents.MediaPath.GenericMediaPath;
        AttributesMode = fileSystem switch
        {
            ExtFileSystem => AttributesMode.Unix,
            FatFileSystem or NtfsFileSystem or CDReader or DiscUtils.Udf.UdfReader => AttributesMode.Windows,
            _ => AttributesMode.Auto
        };
    }

    public Media Media { get; }
    public PartitionTableType PartitionTableType { get; }
    public int PartitionNumber { get; }
    public IMediaPath MediaPath { get; }
    public AttributesMode AttributesMode { get; }
    public VolumeEntryOrder EntryOrder => VolumeEntryOrder.RelativePath;
    public bool IsReadOnly => !_fileSystem.CanWrite;
    public bool SupportsUaeMetadata => false;
    public UaeMetadata UaeMetadata { get; set; }

    /// <summary>
    /// DiscUtils file system.
    /// </summary>
    public IFileSystem FileSystem => _fileSystem;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsMedia)
        {
            if (_fileSystem is IDisposable disposable)
            {
                disposable.Dispose();
            }

            if (Media?.Stream is { CanWrite: true })
            {
                Media.Stream.Flush();
            }

            Media?.Dispose();
        }

        _disposed = true;
    }

    private static string GetPath(string[] pathComponents) => string.Join(PathSeparator, pathComponents);

    [GeneratedRegex(";\\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex Iso9660VersionRegex();

    private string GetName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd('\\', '/'));
        return _isIso9660 ? Iso9660VersionRegex().Replace(name, string.Empty) : name;
    }

    private PathNotFoundError CreatePathNotFoundError(string[] pathComponents)
    {
        var path = MediaPath.Join(pathComponents);
        return new PathNotFoundError($"Path not found '{path}'", path);
    }

    private Result CheckWritable(string operation) =>
        IsReadOnly ? VolumeEntryHelper.CreateReadOnlyResult(this, operation) : new Result();

    private Dictionary<string, string> GetAttributeProperties(string path)
    {
        var properties = new Dictionary<string, string>();

        try
        {
            // attributes are stored as numeric values to be parsed by attributes formatter
            switch (_fileSystem)
            {
                case ExtFileSystem extFileSystem:
                    var unixFileInfo = extFileSystem.GetUnixFileInfo(path);
                    if (unixFileInfo != null)
                    {
                        properties[Constants.EntryPropertyNames.UnixFileMode] =
                            ((uint)unixFileInfo.Permissions).ToString();
                    }

                    break;
                default:
                    properties[Constants.EntryPropertyNames.WindowsAttributes] =
                        ((uint)_fileSystem.GetAttributes(path)).ToString();
                    break;
            }
        }
        catch (Exception)
        {
            // ignored
        }

        return properties;
    }

    private Entry CreateDirEntry(string[] dirPathComponents, string dirPath)
    {
        DateTime? lastWriteTime = null;
        try
        {
            lastWriteTime = _fileSystem.GetLastWriteTime(dirPath);
        }
        catch (Exception)
        {
            // ignored
        }

        var name = GetName(dirPath);
        return new Entry
        {
            Name = name,
            FormattedName = name,
            RawPath = dirPath,
            FullPathComponents = dirPathComponents.Concat([name]).ToArray(),
            RelativePathComponents = [name],
            Date = lastWriteTime ?? DateTime.Now,
            Size = 0,
            Type = EntryType.Dir,
            Properties = GetAttributeProperties(dirPath)
        };
    }

    private Entry CreateFileEntry(string[] dirPathComponents, string filePath)
    {
        DiscFileInfo fileInfo;

        try
        {
            fileInfo = _fileSystem.GetFileInfo(filePath);

            // file is opened to verify the file can be accessed
            using var fileStream = _fileSystem.OpenFile(filePath, FileMode.Open, FileAccess.Read);
        }
        catch (Exception)
        {
            // skip file, if it's not possible to get file info or open the file
            return null;
        }

        var name = GetName(filePath);
        return new Entry
        {
            Name = name,
            FormattedName = name,
            RawPath = filePath,
            FullPathComponents = dirPathComponents.Concat([name]).ToArray(),
            RelativePathComponents = [name],
            Date = fileInfo.LastWriteTime,
            Size = fileInfo.Length,
            Type = EntryType.File,
            Properties = GetAttributeProperties(_isIso9660 ? GetPath(dirPathComponents.Concat([name]).ToArray()) : filePath)
        };
    }

    public Task<Result<Entry>> GetEntry(string[] pathComponents)
    {
        if (pathComponents.Length == 0)
        {
            return Task.FromResult(new Result<Entry>(VolumeEntryHelper.CreateRootEntry()));
        }

        var path = GetPath(pathComponents);
        var dirPathComponents = pathComponents[..^1];

        try
        {
            if (_fileSystem.DirectoryExists(path))
            {
                return Task.FromResult(new Result<Entry>(CreateDirEntry(dirPathComponents, path)));
            }

            if (_fileSystem.FileExists(path))
            {
                var fileEntry = CreateFileEntry(dirPathComponents, path);
                if (fileEntry != null)
                {
                    return Task.FromResult(new Result<Entry>(fileEntry));
                }
            }
        }
        catch (Exception)
        {
            // ignored, path not found
        }

        return Task.FromResult(new Result<Entry>(CreatePathNotFoundError(pathComponents)));
    }

    public Task<Result<IEnumerable<Entry>>> ListEntries(string[] dirPathComponents)
    {
        var path = GetPath(dirPathComponents);

        if (dirPathComponents.Length > 0 && !_fileSystem.DirectoryExists(path))
        {
            return Task.FromResult(new Result<IEnumerable<Entry>>(CreatePathNotFoundError(dirPathComponents)));
        }

        var entries = new List<Entry>();

        entries.AddRange(_fileSystem.GetDirectories(path, "*", SearchOption.TopDirectoryOnly).ToList()
            .Select(dirPath => CreateDirEntry(dirPathComponents, dirPath)));

        entries.AddRange(_fileSystem.GetFiles(path, "*", SearchOption.TopDirectoryOnly).ToList()
            .Select(filePath => CreateFileEntry(dirPathComponents, filePath))
            .Where(x => x != null));

        return Task.FromResult(new Result<IEnumerable<Entry>>(entries));
    }

    public Task<Result<Stream>> OpenFile(string[] pathComponents)
    {
        var path = GetPath(pathComponents);

        if (!_fileSystem.FileExists(path))
        {
            return Task.FromResult(new Result<Stream>(CreatePathNotFoundError(pathComponents)));
        }

        Stream stream = _fileSystem.GetFileLength(path) == 0
            ? new MemoryStream()
            : _fileSystem.OpenFile(path, FileMode.Open, FileAccess.Read);
        return Task.FromResult(new Result<Stream>(stream));
    }

    public Task<Result> CreateDirectory(string[] pathComponents)
    {
        var writableResult = CheckWritable("Create directory");
        if (writableResult.IsFaulted || pathComponents.Length == 0)
        {
            return Task.FromResult(writableResult);
        }

        var path = GetPath(pathComponents);

        if (_fileSystem.FileExists(path))
        {
            return Task.FromResult(new Result(new PathExistsError(
                $"Create directory path '{MediaPath.Join(pathComponents)}' failed. Path already exists as a file!")));
        }

        var parentPath = GetPath(pathComponents[..^1]);
        if (pathComponents.Length > 1 && !_fileSystem.DirectoryExists(parentPath))
        {
            return Task.FromResult(new Result(CreatePathNotFoundError(pathComponents[..^1])));
        }

        if (!_fileSystem.DirectoryExists(path))
        {
            _fileSystem.CreateDirectory(path);
        }

        return Task.FromResult(new Result());
    }

    public async Task<Result> WriteFile(string[] pathComponents, Stream stream, bool overwrite)
    {
        var writableResult = CheckWritable("Write file");
        if (writableResult.IsFaulted)
        {
            return writableResult;
        }

        var path = GetPath(pathComponents);

        if (_fileSystem.DirectoryExists(path))
        {
            return new Result(new PathExistsError(
                $"Path already exists as a directory '{MediaPath.Join(pathComponents)}'"));
        }

        if (!overwrite && _fileSystem.FileExists(path))
        {
            return new Result(new FileExistsError($"File already exists '{MediaPath.Join(pathComponents)}'"));
        }

        await using var entryStream = _fileSystem.OpenFile(path, FileMode.Create, FileAccess.ReadWrite);

        int bytesRead;
        do
        {
            bytesRead = await stream.ReadAsync(_buffer, 0, _buffer.Length);
            await entryStream.WriteAsync(_buffer, 0, bytesRead);
        } while (bytesRead != 0);

        return new Result();
    }

    public Task<Result> SetEntryInfo(string[] pathComponents, Entry entry, bool skipAttributes)
    {
        // entry info like attributes and dates are not written to DiscUtils file systems
        return Task.FromResult(CheckWritable("Set entry info"));
    }

    public Task<Result<int>> SetAttributes(string[] pathComponents, AttributesChange attributesChange)
    {
        if (IsReadOnly)
        {
            return Task.FromResult(VolumeEntryHelper.CreateReadOnlyResult<int>(this, "Set attributes"));
        }

        if (attributesChange.Mode != AttributesMode.Windows || _fileSystem is ExtFileSystem)
        {
            return Task.FromResult(new Result<int>(new Error(
                $"Attributes mode '{attributesChange.Mode}' is not supported for file system '{_fileSystem.GetType().Name}'")));
        }

        var path = GetPath(pathComponents);

        if (!_fileSystem.Exists(path))
        {
            return Task.FromResult(new Result<int>(CreatePathNotFoundError(pathComponents)));
        }

        try
        {
            var attributes = attributesChange.Apply((int)_fileSystem.GetAttributes(path));
            _fileSystem.SetAttributes(path, (FileAttributes)attributes);
            return Task.FromResult(new Result<int>(attributes));
        }
        catch (Exception e)
        {
            return Task.FromResult(new Result<int>(new Error(e.Message)));
        }
    }

    public Task<Result> CreateLink(string[] pathComponents, string linkPath)
    {
        return Task.FromResult(new Result(new Error(
            $"Creating link is not supported for file system '{_fileSystem.GetType().Name}'")));
    }

    public Task<Result> Delete(string[] pathComponents)
    {
        var writableResult = CheckWritable("Delete");
        if (writableResult.IsFaulted)
        {
            return Task.FromResult(writableResult);
        }

        var path = GetPath(pathComponents);

        if (_fileSystem.FileExists(path))
        {
            _fileSystem.DeleteFile(path);
            return Task.FromResult(new Result());
        }

        if (!_fileSystem.DirectoryExists(path))
        {
            return Task.FromResult(new Result(CreatePathNotFoundError(pathComponents)));
        }

        _fileSystem.DeleteDirectory(path);
        return Task.FromResult(new Result());
    }

    public Task<Result> Move(string[] srcPathComponents, string[] destPathComponents, bool overwrite)
    {
        var writableResult = CheckWritable("Move");
        if (writableResult.IsFaulted)
        {
            return Task.FromResult(writableResult);
        }

        var srcPath = GetPath(srcPathComponents);
        var destPath = GetPath(destPathComponents);

        if (string.IsNullOrEmpty(destPath))
        {
            return Task.FromResult(new Result(new Error("Destination path is null or empty.")));
        }

        if (!_fileSystem.Exists(srcPath))
        {
            var srcMediaPath = MediaPath.Join(srcPathComponents);
            return Task.FromResult(new Result(new PathNotFoundError($"Source path '{srcMediaPath}' not found",
                srcMediaPath)));
        }

        var destExists = _fileSystem.Exists(destPath);
        if (!overwrite && destExists)
        {
            return Task.FromResult(new Result(new PathExistsError(
                $"Destination path '{MediaPath.Join(destPathComponents)}' already exists")));
        }

        if (_fileSystem.DirectoryExists(srcPath))
        {
            _fileSystem.MoveDirectory(srcPath, destPath);
            return Task.FromResult(new Result());
        }

        if (destExists)
        {
            _fileSystem.DeleteFile(destPath);
        }

        _fileSystem.MoveFile(srcPath, destPath);
        return Task.FromResult(new Result());
    }

    public bool IsSameVolume(IVolume other) => VolumeEntryHelper.IsSameMediaAndPartition(this, other);

    public Task Flush() => Task.CompletedTask;

    public IEnumerable<string> GetDebugLogs() => [];

    public IEnumerable<string> GetLogs() => [];
}
