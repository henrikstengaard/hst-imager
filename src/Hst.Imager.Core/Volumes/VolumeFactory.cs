using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils.Iso9660;
using Hst.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.MagicBytes;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.UaeMetadatas;

namespace Hst.Imager.Core.Volumes;

/// <summary>
/// Volume access.
/// </summary>
public enum VolumeAccess
{
    Read,
    Write
}

/// <summary>
/// Volume and path components in volume resolved from a path.
/// </summary>
/// <param name="Volume">Volume.</param>
/// <param name="PathComponents">Path components in volume.</param>
public record VolumePath(IVolume Volume, string[] PathComponents);

/// <summary>
/// Volume factory resolves paths to local directories, archives, ADF, floppy and disk partitions
/// (MBR, GPT, RDB incl. PiStorm RDB) and opens volumes for them.
/// Volumes are cached by media and partition, so the same volume is returned for paths in the same
/// media and partition. Volumes opened are owned and disposed by the volume factory.
/// </summary>
public class VolumeFactory : IDisposable
{
    private readonly ICommandHelper _commandHelper;
    private readonly IEnumerable<IPhysicalDrive> _physicalDrives;
    private readonly UaeMetadataHelper _uaeMetadataHelper;
    private readonly UaeMetadata _uaeMetadata;
    private readonly Action<string> _debugMessage;
    private readonly VolumeMounter _volumeMounter;
    private readonly Dictionary<string, (IVolume Volume, bool Writable)> _volumes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IVolume> _openedVolumes = [];
    private LocalDirectoryVolume _localDirectoryVolume;
    private bool _disposed;

    /// <summary>
    /// Volume factory.
    /// </summary>
    /// <param name="commandHelper">Command helper to resolve and open media.</param>
    /// <param name="physicalDrives">Physical drives.</param>
    /// <param name="uaeMetadataHelper">UAE metadata helper used by local directory volumes.</param>
    /// <param name="uaeMetadata">UAE metadata to use for volumes supporting UAE metadata.</param>
    /// <param name="debugMessage">Action to report debug messages.</param>
    public VolumeFactory(ICommandHelper commandHelper, IEnumerable<IPhysicalDrive> physicalDrives,
        UaeMetadataHelper uaeMetadataHelper, UaeMetadata uaeMetadata = UaeMetadata.None,
        Action<string> debugMessage = null)
    {
        _commandHelper = commandHelper;
        _physicalDrives = physicalDrives;
        _uaeMetadataHelper = uaeMetadataHelper;
        _uaeMetadata = uaeMetadata;
        _debugMessage = debugMessage;
        _volumeMounter = new VolumeMounter(debugMessage);
    }

    private void OnDebugMessage(string message) => _debugMessage?.Invoke(message);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var volume in _openedVolumes.Distinct())
        {
            volume.Dispose();
        }

        _openedVolumes.Clear();
        _volumes.Clear();
        _disposed = true;
    }

    private T AddVolume<T>(T volume, string cacheKey = null, bool writable = false) where T : IVolume
    {
        volume.UaeMetadata = volume.SupportsUaeMetadata ? _uaeMetadata : UaeMetadata.None;
        _openedVolumes.Add(volume);

        if (cacheKey != null)
        {
            _volumes[cacheKey] = (volume, writable);
        }

        return volume;
    }

    /// <summary>
    /// Open local directory volume for local path.
    /// </summary>
    public async Task<VolumePath> OpenLocalDirectory(string path)
    {
        var pathComponents = LocalDirectoryVolume.ToPathComponents(path);

        if (_localDirectoryVolume == null)
        {
            var media = await MediaHelper.CreateLocalDirectoryMediaFromPath(path, _uaeMetadata, _uaeMetadataHelper);
            _localDirectoryVolume = AddVolume(new LocalDirectoryVolume(media, _uaeMetadataHelper));
        }

        return new VolumePath(_localDirectoryVolume, pathComponents);
    }

    private static string[] SplitFileSystemPath(MediaResult mediaResult) =>
        (mediaResult.FileSystemPath ?? string.Empty).Split(mediaResult.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);

    private static bool IsArchive(DataType dataType) =>
        dataType is DataType.Zip or DataType.Lha or DataType.Lzx or DataType.Lzw or DataType.Iso9660;

    private static bool IsAdf(DataType dataType, string mediaPath) =>
        dataType == DataType.Adf ||
        (Path.GetExtension(mediaPath) ?? string.Empty).Equals(".adf", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Get cache key for media path and file system path components identifying the partition.
    /// </summary>
    private static string GetCacheKey(MediaResult mediaResult, string[] fileSystemPathComponents)
    {
        var partitionPathComponentsCount = 0;
        if (fileSystemPathComponents.Length >= 2 &&
            fileSystemPathComponents[0].ToLowerInvariant() is "mbr" or "gpt" or "rdb")
        {
            // pistorm rdb partition in mbr partition, e.g. "mbr\2\rdb\dh0"
            partitionPathComponentsCount = fileSystemPathComponents.Length >= 4 &&
                                           fileSystemPathComponents[2].Equals("rdb",
                                               StringComparison.OrdinalIgnoreCase)
                ? 4
                : 2;
        }

        return string.Join("|", PathHelper.GetFullPath(mediaResult.MediaPath), mediaResult.Modifiers,
            string.Join("/", fileSystemPathComponents.Take(partitionPathComponentsCount)));
    }

    /// <summary>
    /// Open volume for path.
    /// </summary>
    /// <param name="path">Path to open volume for, e.g. local directory, "disk.img\rdb\dh0\dir" or "archive.zip\dir".</param>
    /// <param name="access">Access to volume.</param>
    /// <param name="openArchives">Open archive and ADF files without a file system path as volumes.
    /// Otherwise, they are opened as files in a local directory volume.</param>
    /// <returns>Volume and path components in volume.</returns>
    public async Task<Result<VolumePath>> Open(string path, VolumeAccess access, bool openArchives = true)
    {
        var mediaResult = _commandHelper.ResolveMedia(path);

        // open local directory volume, if media path doesn't exist. otherwise return error
        if (mediaResult.IsFaulted)
        {
            return mediaResult.Error is PathNotFoundError
                ? new Result<VolumePath>(await OpenLocalDirectory(path))
                : new Result<VolumePath>(mediaResult.Error);
        }

        var resolvedMedia = mediaResult.Value;
        var hasFileSystemPath = !string.IsNullOrWhiteSpace(resolvedMedia.FileSystemPath);

        if (!hasFileSystemPath && Directory.Exists(path))
        {
            return new Result<VolumePath>(await OpenLocalDirectory(path));
        }

        OnDebugMessage($"Media Path: '{resolvedMedia.MediaPath}'");
        OnDebugMessage($"File System Path: '{resolvedMedia.FileSystemPath}'");

        var dataType = await _commandHelper.DetectDataType(resolvedMedia.MediaPath);

        if (!hasFileSystemPath && File.Exists(path) &&
            (!openArchives || !(IsArchive(dataType) || IsAdf(dataType, resolvedMedia.MediaPath))))
        {
            return new Result<VolumePath>(await OpenLocalDirectory(path));
        }

        var fileSystemPathComponents = SplitFileSystemPath(resolvedMedia);
        var cacheKey = GetCacheKey(resolvedMedia, fileSystemPathComponents);

        if (openArchives && IsArchive(dataType))
        {
            return await OpenArchive(resolvedMedia, dataType, fileSystemPathComponents, cacheKey);
        }

        if (_volumes.TryGetValue(cacheKey, out var cachedVolume) &&
            (cachedVolume.Writable || access == VolumeAccess.Read))
        {
            return new Result<VolumePath>(new VolumePath(cachedVolume.Volume,
                GetVolumePathComponents(cachedVolume.Volume, fileSystemPathComponents)));
        }

        var writable = access == VolumeAccess.Write;
        var mediaOpenResult = writable
            ? await _commandHelper.GetWritableMedia(_physicalDrives, resolvedMedia.MediaPath, resolvedMedia.Modifiers)
            : await _commandHelper.GetReadableMedia(_physicalDrives, resolvedMedia.MediaPath, resolvedMedia.Modifiers);
        if (mediaOpenResult.IsFaulted)
        {
            return new Result<VolumePath>(mediaOpenResult.Error);
        }

        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(mediaOpenResult.Value,
            resolvedMedia.FileSystemPath ?? string.Empty, resolvedMedia.DirectorySeparatorChar);

        var media = piStormRdbMediaResult.Media;
        var parts = (piStormRdbMediaResult.FileSystemPath ?? string.Empty).Split(
            resolvedMedia.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        var volumeResult = await MountVolume(media, dataType, resolvedMedia.MediaPath, parts, path);
        if (volumeResult.IsFaulted)
        {
            media.Dispose();
            return new Result<VolumePath>(volumeResult.Error);
        }

        var (volume, pathComponents) = volumeResult.Value;
        AddVolume(volume, cacheKey, writable);

        return new Result<VolumePath>(new VolumePath(volume, pathComponents));
    }

    /// <summary>
    /// Get path components in volume from file system path components.
    /// </summary>
    private static string[] GetVolumePathComponents(IVolume volume, string[] fileSystemPathComponents)
    {
        if (volume.PartitionTableType == PartitionTableType.None || volume.Media?.Type == Media.MediaType.Floppy ||
            fileSystemPathComponents.Length < 2 ||
            fileSystemPathComponents[0].ToLowerInvariant() is not ("mbr" or "gpt" or "rdb"))
        {
            return fileSystemPathComponents;
        }

        var skip = fileSystemPathComponents.Length >= 4 &&
                   fileSystemPathComponents[2].Equals("rdb", StringComparison.OrdinalIgnoreCase)
            ? 4
            : 2;
        return fileSystemPathComponents.Skip(skip).ToArray();
    }

    private async Task<Result<VolumePath>> OpenArchive(MediaResult resolvedMedia, DataType dataType,
        string[] pathComponents, string cacheKey)
    {
        if (_volumes.TryGetValue(cacheKey, out var cachedVolume))
        {
            return new Result<VolumePath>(new VolumePath(cachedVolume.Volume, pathComponents));
        }

        if (dataType == DataType.Lzw)
        {
            var lzwVolume = AddVolume(ArchiveVolumeReader.ReadLzw(resolvedMedia.MediaPath), cacheKey);
            return new Result<VolumePath>(new VolumePath(lzwVolume, pathComponents));
        }

        var mediaResult = await _commandHelper.GetReadableFileMedia(resolvedMedia.MediaPath);
        if (mediaResult.IsFaulted)
        {
            return new Result<VolumePath>(mediaResult.Error);
        }

        var media = mediaResult.Value;

        IVolume volume = dataType switch
        {
            DataType.Zip => await ArchiveVolumeReader.ReadZip(media.Stream),
            DataType.Lha => await ArchiveVolumeReader.ReadLha(media.Stream),
            DataType.Lzx => await ArchiveVolumeReader.ReadLzx(media.Stream),
            DataType.Iso9660 => new DiscUtilsVolume(media, PartitionTableType.None, 0,
                new CDReader(media.Stream, true)),
            _ => null
        };

        if (volume == null)
        {
            media.Dispose();
            return new Result<VolumePath>(new Error($"Unsupported archive '{resolvedMedia.MediaPath}'"));
        }

        AddVolume(volume, cacheKey);

        return new Result<VolumePath>(new VolumePath(volume, pathComponents));
    }

    private async Task<Result<(IVolume, string[])>> MountVolume(Media media, DataType dataType, string mediaPath,
        string[] parts, string path)
    {
        if (media.Type == Media.MediaType.Floppy)
        {
            var fileSystemResult = await VolumeMounter.MountFileSystem(media.Stream);
            if (fileSystemResult.IsFaulted)
            {
                return new Result<(IVolume, string[])>(fileSystemResult.Error);
            }

            return new Result<(IVolume, string[])>((new DiscUtilsVolume(media, PartitionTableType.None, 0,
                fileSystemResult.Value), parts));
        }

        if (IsAdf(dataType, mediaPath))
        {
            var fileSystemVolumeResult = await _volumeMounter.MountAdfFileSystemVolume(media.Stream);
            if (fileSystemVolumeResult.IsFaulted)
            {
                return new Result<(IVolume, string[])>(fileSystemVolumeResult.Error);
            }

            return new Result<(IVolume, string[])>((new AmigaVolume(media, PartitionTableType.RigidDiskBlock, 0,
                fileSystemVolumeResult.Value), parts));
        }

        if (parts.Length == 0)
        {
            return new Result<(IVolume, string[])>(new Error($"No partition table in path '{path}'"));
        }

        if (parts.Length < 2)
        {
            return new Result<(IVolume, string[])>(new Error(
                $"Path '{path}' doesn't contain partition table and partition number"));
        }

        var rootPathComponents = parts.Skip(2).ToArray();

        switch (parts[0].ToLowerInvariant())
        {
            case "mbr":
            {
                var disk = await MediaHelper.ResolveVirtualDisk(media);
                var fileSystemResult = await _volumeMounter.MountMbrFileSystem(disk, parts[1]);
                if (fileSystemResult.IsFaulted)
                {
                    return new Result<(IVolume, string[])>(fileSystemResult.Error);
                }

                var (partitionNumber, fileSystem) = fileSystemResult.Value;
                return new Result<(IVolume, string[])>((new DiscUtilsVolume(media,
                    PartitionTableType.MasterBootRecord, partitionNumber, fileSystem), rootPathComponents));
            }
            case "gpt":
            {
                var disk = await MediaHelper.ResolveVirtualDisk(media);
                var fileSystemResult = await _volumeMounter.MountGptFileSystem(disk, parts[1]);
                if (fileSystemResult.IsFaulted)
                {
                    return new Result<(IVolume, string[])>(fileSystemResult.Error);
                }

                var (partitionNumber, fileSystem) = fileSystemResult.Value;
                return new Result<(IVolume, string[])>((new DiscUtilsVolume(media,
                    PartitionTableType.GuidPartitionTable, partitionNumber, fileSystem), rootPathComponents));
            }
            case "rdb":
            {
                var fileSystemVolumeResult = await _volumeMounter.MountRdbFileSystemVolume(media, parts[1]);
                if (fileSystemVolumeResult.IsFaulted)
                {
                    return new Result<(IVolume, string[])>(fileSystemVolumeResult.Error);
                }

                var (partitionNumber, fileSystemVolume) = fileSystemVolumeResult.Value;
                return new Result<(IVolume, string[])>((new AmigaVolume(media, PartitionTableType.RigidDiskBlock,
                    partitionNumber, fileSystemVolume), rootPathComponents));
            }
            default:
                return new Result<(IVolume, string[])>(new Error(
                    $"Unsupported partition table '{parts[0]}' in path '{path}'"));
        }
    }
}
