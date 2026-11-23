using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.FileSystems;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.PathComponents;
using Hst.Imager.Core.UaeMetadatas;
using Entry = Hst.Imager.Core.Models.FileSystems.Entry;
using EntryType = Hst.Imager.Core.Models.FileSystems.EntryType;

namespace Hst.Imager.Core.Volumes;

/// <summary>
/// Volume for local directories. Path components are absolute local path components,
/// e.g. "C:", "dir" on Windows and "/", "dir" on macOS and Linux.
/// When UAE metadata is used, path components use Amiga names, which are resolved to normal names using UAE metadata.
/// </summary>
public class LocalDirectoryVolume(Media media, UaeMetadataHelper uaeMetadataHelper) : IVolume
{
    private readonly byte[] _buffer = new byte[4096];

    /// <summary>
    /// Local paths created by volume indexed by path components. UAE metadata is cached by UAE metadata helper,
    /// so local paths created are remembered to resolve them without reading UAE metadata again.
    /// </summary>
    private readonly Dictionary<string, string> _createdLocalPaths = new(System.OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal);

    public Media Media { get; } = media;
    public PartitionTableType PartitionTableType => PartitionTableType.None;
    public int PartitionNumber => 0;
    public IMediaPath MediaPath { get; } = Core.PathComponents.MediaPath.GenericMediaPath;

    /// <summary>
    /// Attributes mode is Amiga protection bits, when UAE metadata is used. Otherwise attributes mode is auto
    /// using Windows attributes or Unix file mode depending on operating system.
    /// </summary>
    public AttributesMode AttributesMode => UaeMetadata == UaeMetadata.None ? AttributesMode.Auto : AttributesMode.Amiga;

    public VolumeEntryOrder EntryOrder => VolumeEntryOrder.RelativePath;
    public bool IsReadOnly => false;
    public bool SupportsUaeMetadata => true;
    public UaeMetadata UaeMetadata { get; set; }

    public void Dispose()
    {
    }

    /// <summary>
    /// Convert path components to local path.
    /// </summary>
    public static string ToLocalPath(string[] pathComponents)
    {
        if (pathComponents.Length == 0)
        {
            return string.Empty;
        }

        var first = pathComponents[0];
        var rest = pathComponents.Skip(1).ToArray();

        if (first == "/")
        {
            return string.Concat("/", Path.Combine(rest));
        }

        if (PathHelper.IsWindowsRootPath(first) && first.EndsWith(':'))
        {
            return Path.Combine([string.Concat(first, Path.DirectorySeparatorChar), ..rest]);
        }

        return Path.Combine(pathComponents);
    }

    /// <summary>
    /// Convert local path to path components.
    /// </summary>
    public static string[] ToPathComponents(string localPath) => PathHelper.Split(PathHelper.GetFullPath(localPath));

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private static string GetKey(string[] pathComponents) => string.Join("/", pathComponents);

    private static string[] GetNormalPathComponents(string[] pathComponents) =>
        pathComponents.Length == 0
            ? []
            : PathHelper.IsRootPath(pathComponents[0])
                ? [pathComponents[0], ..pathComponents.Skip(1).Select(UaeMetadataHelper.CreateNormalFilename)]
                : pathComponents.Select(UaeMetadataHelper.CreateNormalFilename).ToArray();

    /// <summary>
    /// Resolve local path for existing path components using UAE metadata, if used.
    /// </summary>
    private async Task<(string LocalPath, UaeMetadataEntry UaeMetadataEntry)> ResolveLocalPath(
        string[] pathComponents)
    {
        if (pathComponents.Length == 0)
        {
            return (string.Empty, null);
        }

        if (_createdLocalPaths.TryGetValue(GetKey(pathComponents), out var createdLocalPath))
        {
            return (createdLocalPath, null);
        }

        var uaeMetadataEntry = await uaeMetadataHelper.GetUaeMetadataEntry(UaeMetadata, pathComponents);
        if (uaeMetadataEntry != null)
        {
            return (ToLocalPath(uaeMetadataEntry.NormalPathComponents), uaeMetadataEntry);
        }

        var localPath = ToLocalPath(pathComponents);
        if (Exists(localPath))
        {
            return (localPath, null);
        }

        var normalPath = ToLocalPath(GetNormalPathComponents(pathComponents));
        return (Exists(normalPath) ? normalPath : localPath, null);
    }

    /// <summary>
    /// Resolve local path to write for path components. Parent directory is resolved and name is
    /// normalized or mapped using UAE metadata.
    /// </summary>
    private async Task<string> ResolveWriteLocalPath(string[] pathComponents)
    {
        if (_createdLocalPaths.TryGetValue(GetKey(pathComponents), out var createdLocalPath))
        {
            return createdLocalPath;
        }

        var (dirPath, _) = await ResolveLocalPath(pathComponents[..^1]);
        var normalName = await uaeMetadataHelper.GetFileName(UaeMetadata, string.IsNullOrEmpty(dirPath)
            ? Directory.GetCurrentDirectory()
            : dirPath, pathComponents[^1]);
        return Path.Combine(dirPath, normalName);
    }

    private PathNotFoundError CreatePathNotFoundError(string[] pathComponents)
    {
        var path = ToLocalPath(pathComponents);
        return new PathNotFoundError($"Path not found '{path}'", path);
    }

    private async Task<Entry> CreateEntry(string[] dirPathComponents, FileSystemInfo fileSystemInfo)
    {
        var isDir = fileSystemInfo is DirectoryInfo;
        var name = fileSystemInfo.Name;
        var date = fileSystemInfo.LastWriteTime;
        var properties = new Dictionary<string, string>
        {
            [Constants.EntryPropertyNames.WindowsAttributes] = ((uint)fileSystemInfo.Attributes).ToString(),
            [Constants.EntryPropertyNames.UnixFileMode] = ((uint)fileSystemInfo.UnixFileMode).ToString()
        };

        if (UaeMetadata != UaeMetadata.None)
        {
            var uaeMetadataEntry = await uaeMetadataHelper.GetUaeMetadataEntry(UaeMetadata,
                PathHelper.Split(fileSystemInfo.FullName));

            if (uaeMetadataEntry != null)
            {
                name = uaeMetadataEntry.UaePathComponents[^1];

                if (uaeMetadataEntry.Date.HasValue)
                {
                    date = uaeMetadataEntry.Date.Value;
                }

                if (uaeMetadataEntry.Comment != null)
                {
                    properties[Constants.EntryPropertyNames.Comment] = uaeMetadataEntry.Comment;
                }

                if (uaeMetadataEntry.ProtectionBits.HasValue)
                {
                    properties[Constants.EntryPropertyNames.ProtectionBits] =
                        uaeMetadataEntry.ProtectionBits.ToString();
                }
            }
        }

        return new Entry
        {
            Name = name,
            FormattedName = name,
            RawPath = fileSystemInfo.FullName,
            FullPathComponents = dirPathComponents.Concat([name]).ToArray(),
            RelativePathComponents = [name],
            Date = date,
            Size = fileSystemInfo is FileInfo fileInfo ? fileInfo.Length : 0,
            Type = isDir ? EntryType.Dir : EntryType.File,
            Properties = properties
        };
    }

    private bool IsUaeMetadataFile(FileInfo file) =>
        UaeMetadata != UaeMetadata.None &&
        (file.Name.Equals(Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName, StringComparison.OrdinalIgnoreCase) ||
         file.Extension.Equals(Amiga.DataTypes.UaeMetafiles.Constants.UaeMetafileExtension,
             StringComparison.OrdinalIgnoreCase));

    public async Task<Result<Entry>> GetEntry(string[] pathComponents)
    {
        if (pathComponents.Length == 0)
        {
            return new Result<Entry>(VolumeEntryHelper.CreateRootEntry());
        }

        var (localPath, _) = await ResolveLocalPath(pathComponents);

        FileSystemInfo fileSystemInfo = Directory.Exists(localPath)
            ? new DirectoryInfo(localPath)
            : File.Exists(localPath)
                ? new FileInfo(localPath)
                : null;

        if (fileSystemInfo == null)
        {
            return new Result<Entry>(CreatePathNotFoundError(pathComponents));
        }

        var entry = await CreateEntry(pathComponents[..^1], fileSystemInfo);

        // root path components like "C:" or "/" are kept as is
        if (pathComponents.Length == 1)
        {
            entry.Name = entry.FormattedName = pathComponents[0];
            entry.FullPathComponents = pathComponents;
        }

        return new Result<Entry>(entry);
    }

    public async Task<Result<IEnumerable<Entry>>> ListEntries(string[] dirPathComponents)
    {
        var (localPath, _) = await ResolveLocalPath(dirPathComponents);

        var dirInfo = new DirectoryInfo(localPath);
        if (!dirInfo.Exists)
        {
            return new Result<IEnumerable<Entry>>(CreatePathNotFoundError(dirPathComponents));
        }

        var entries = new List<Entry>();

        foreach (var subDirInfo in dirInfo.GetDirectories())
        {
            entries.Add(await CreateEntry(dirPathComponents, subDirInfo));
        }

        foreach (var fileInfo in dirInfo.GetFiles().Where(x => !IsUaeMetadataFile(x)))
        {
            entries.Add(await CreateEntry(dirPathComponents, fileInfo));
        }

        return new Result<IEnumerable<Entry>>(entries);
    }

    public async Task<Result<Stream>> OpenFile(string[] pathComponents)
    {
        var (localPath, _) = await ResolveLocalPath(pathComponents);

        return File.Exists(localPath)
            ? new Result<Stream>(File.OpenRead(localPath))
            : new Result<Stream>(CreatePathNotFoundError(pathComponents));
    }

    public async Task<Result> CreateDirectory(string[] pathComponents)
    {
        if (pathComponents.Length == 0)
        {
            return new Result();
        }

        var (existingPath, _) = await ResolveLocalPath(pathComponents);
        if (Directory.Exists(existingPath))
        {
            return new Result();
        }

        var (parentPath, _) = await ResolveLocalPath(pathComponents[..^1]);
        if (pathComponents.Length > 1 && !Directory.Exists(parentPath))
        {
            return new Result(CreatePathNotFoundError(pathComponents[..^1]));
        }

        var localPath = await ResolveWriteLocalPath(pathComponents);

        if (File.Exists(localPath))
        {
            return new Result(new PathExistsError(
                $"Create directory path '{localPath}' failed. Path already exists as a file!"));
        }

        Directory.CreateDirectory(localPath);
        _createdLocalPaths[GetKey(pathComponents)] = localPath;

        return new Result();
    }

    public async Task<Result> WriteFile(string[] pathComponents, Stream stream, bool overwrite)
    {
        if (pathComponents.Length == 0)
        {
            return new Result(new Error("Root directory can't be written as a file"));
        }

        var localPath = await ResolveWriteLocalPath(pathComponents);

        if (Directory.Exists(localPath))
        {
            return new Result(new PathExistsError($"Path already exists as a directory '{localPath}'"));
        }

        if (!overwrite && File.Exists(localPath))
        {
            return new Result(new FileExistsError($"File already exists '{localPath}'"));
        }

        var dirPath = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
        {
            return new Result(CreatePathNotFoundError(pathComponents[..^1]));
        }

        _createdLocalPaths[GetKey(pathComponents)] = localPath;

        await using var fileStream = File.Open(localPath, System.IO.FileMode.Create, FileAccess.ReadWrite);

        // continue until bytes read is 0. reads from zip streams can return bytes between 0 to buffer length.
        int bytesRead;
        do
        {
            bytesRead = await stream.ReadAsync(_buffer, 0, _buffer.Length);
            await fileStream.WriteAsync(_buffer, 0, bytesRead);
        } while (bytesRead != 0);

        return new Result();
    }

    private static string GetProperty(IDictionary<string, string> properties, string name) =>
        properties.TryGetValue(name, out var value) ? value : null;

    public async Task<Result> SetEntryInfo(string[] pathComponents, Entry entry, bool skipAttributes)
    {
        if (pathComponents.Length == 0)
        {
            return new Result();
        }

        var localPath = await ResolveWriteLocalPath(pathComponents);
        var isDir = Directory.Exists(localPath);

        if (!isDir && !File.Exists(localPath))
        {
            return new Result(CreatePathNotFoundError(pathComponents));
        }

        if (!isDir && entry.Date.HasValue)
        {
            File.SetCreationTime(localPath, entry.Date.Value);
            File.SetLastWriteTime(localPath, entry.Date.Value);
            File.SetLastAccessTime(localPath, entry.Date.Value);
        }

        if (UaeMetadata == UaeMetadata.None)
        {
            return new Result();
        }

        int? protectionBits = int.TryParse(GetProperty(entry.Properties,
            Constants.EntryPropertyNames.ProtectionBits), out var parsedProtectionBits)
            ? parsedProtectionBits
            : isDir ? null : 0;
        var comment = GetProperty(entry.Properties, Constants.EntryPropertyNames.Comment);

        var amigaName = pathComponents[^1];
        var normalName = Path.GetFileName(localPath);

        var writeUaeMetadata = UaeMetadataHelper.RequiresUaeMetadataProperties(protectionBits, comment) ||
                               UaeMetadataHelper.RequiresUaeMetadataFileName(UaeMetadata, amigaName);

        if (!writeUaeMetadata)
        {
            return new Result();
        }

        var dirPath = Path.GetDirectoryName(localPath);
        await UaeMetadataHelper.WriteUaeMetadata(UaeMetadata, string.IsNullOrEmpty(dirPath) ? "." : dirPath,
            amigaName, normalName, protectionBits, entry.Date ?? DateTime.Now, comment);

        return new Result();
    }

    public async Task<Result<int>> SetAttributes(string[] pathComponents, AttributesChange attributesChange)
    {
        var (localPath, uaeMetadataEntry) = await ResolveLocalPath(pathComponents);

        if (!Exists(localPath))
        {
            return new Result<int>(CreatePathNotFoundError(pathComponents));
        }

        switch (attributesChange.Mode)
        {
            case AttributesMode.Windows:
                var windowsAttributes = attributesChange.Apply((int)File.GetAttributes(localPath));
                File.SetAttributes(localPath,
                    windowsAttributes == 0 ? FileAttributes.Normal : (FileAttributes)windowsAttributes);
                return new Result<int>(windowsAttributes);
            case AttributesMode.Unix:
                if (System.OperatingSystem.IsWindows())
                {
                    return new Result<int>(new Error("Unix file mode is not supported on Windows"));
                }

                var unixFileMode = attributesChange.Apply((int)File.GetUnixFileMode(localPath));
                File.SetUnixFileMode(localPath, (UnixFileMode)unixFileMode);
                return new Result<int>(unixFileMode);
            case AttributesMode.Amiga:
                if (UaeMetadata == UaeMetadata.None)
                {
                    return new Result<int>(new Error(
                        "UAE metadata is required to set Amiga protection bits in local directory"));
                }

                // protection bits in uae metadata has "rwed" set when not allowed
                var protectionBits = attributesChange.Apply(
                    (int)ProtectionBitsConverter.ToProtectionBits(uaeMetadataEntry?.ProtectionBits ?? 0));

                var dirPath = Path.GetDirectoryName(localPath) ?? string.Empty;
                var normalName = Path.GetFileName(localPath);
                var amigaName = uaeMetadataEntry != null ? uaeMetadataEntry.UaePathComponents[^1] : normalName;
                var date = uaeMetadataEntry?.Date ?? (File.Exists(localPath)
                    ? File.GetLastWriteTime(localPath)
                    : Directory.GetLastWriteTime(localPath));

                await UaeMetadataHelper.WriteUaeMetadata(UaeMetadata, dirPath, amigaName, normalName,
                    ProtectionBitsConverter.ToProtectionValue((ProtectionBits)protectionBits), date,
                    uaeMetadataEntry?.Comment);

                return new Result<int>(protectionBits);
            default:
                return new Result<int>(new Error($"Unsupported attributes mode '{attributesChange.Mode}'"));
        }
    }

    public Task<Result> CreateLink(string[] pathComponents, string linkPath)
    {
        return Task.FromResult(new Result(new Error("Creating link is not supported for local directory")));
    }

    public async Task<Result> Delete(string[] pathComponents)
    {
        _createdLocalPaths.Clear();
        var (localPath, _) = await ResolveLocalPath(pathComponents);

        if (!Exists(localPath))
        {
            return new Result(CreatePathNotFoundError(pathComponents));
        }

        if (File.Exists(localPath))
        {
            File.Delete(localPath);
        }

        if (Directory.Exists(localPath))
        {
            Directory.Delete(localPath, true);
        }

        await UaeMetadataHelper.RemoveUaeMetadata(localPath);

        return new Result();
    }

    public async Task<Result> Move(string[] srcPathComponents, string[] destPathComponents, bool overwrite)
    {
        _createdLocalPaths.Clear();
        var (srcPath, srcUaeMetadataEntry) = await ResolveLocalPath(srcPathComponents);

        if (!Exists(srcPath))
        {
            return new Result(new PathNotFoundError($"Source path '{srcPath}' not found", srcPath));
        }

        var destPath = await ResolveWriteLocalPath(destPathComponents);

        if (string.IsNullOrEmpty(destPath))
        {
            return new Result(new Error("Destination path is null or empty."));
        }

        if (Exists(destPath) && !overwrite)
        {
            return new Result(new PathExistsError($"Destination path '{destPath}' already exists"));
        }

        if (Directory.Exists(srcPath))
        {
            if (Directory.Exists(destPath))
            {
                Directory.Delete(destPath, true);
            }

            Directory.Move(srcPath, destPath);
            return new Result();
        }

        File.Move(srcPath, destPath, overwrite);

        var protectionBits = srcUaeMetadataEntry?.ProtectionBits ?? 0;
        var comment = srcUaeMetadataEntry?.Comment;
        var date = srcUaeMetadataEntry?.Date;

        await UaeMetadataHelper.RemoveUaeMetadata(srcPath);

        var destAmigaName = destPathComponents[^1];
        var destNormalName = Path.GetFileName(destPath);

        var requiresUaeMetadata = UaeMetadata != UaeMetadata.None &&
                                  (UaeMetadataHelper.RequiresUaeMetadataFileName(UaeMetadata, destAmigaName) ||
                                   UaeMetadataHelper.RequiresUaeMetadataProperties(protectionBits, comment));

        if (requiresUaeMetadata)
        {
            await UaeMetadataHelper.WriteUaeMetadata(UaeMetadata, Path.GetDirectoryName(destPath) ?? string.Empty,
                destAmigaName, destNormalName, protectionBits, date ?? DateTime.Now, comment);
        }

        return new Result();
    }

    public bool IsSameVolume(IVolume other) => other is LocalDirectoryVolume;

    public Task Flush() => Task.CompletedTask;

    public IEnumerable<string> GetDebugLogs() => [];

    public IEnumerable<string> GetLogs() => [];
}
