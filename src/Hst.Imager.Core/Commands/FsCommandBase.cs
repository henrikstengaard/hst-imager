﻿namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Amiga.FileSystems.FastFileSystem;
using Amiga.FileSystems.Pfs3;
using Amiga.RigidDiskBlocks;
using Compression.Lha;
using DiscUtils.Iso9660;
using Hst.Core;
using Hst.Core.Extensions;
using Hst.Imager.Core.Helpers;
using Models;
using Directory = System.IO.Directory;
using File = System.IO.File;
using FileMode = System.IO.FileMode;

public abstract class FsCommandBase : CommandBase
{
    protected readonly ICommandHelper commandHelper;
    protected readonly IEnumerable<IPhysicalDrive> physicalDrives;

    protected FsCommandBase(ICommandHelper commandHelper, IEnumerable<IPhysicalDrive> physicalDrives)
    {
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
    }

    private static readonly byte[]
        VhdMagicNumber = new byte[]
        {
            0x63, 0x6F, 0x6E, 0x65, 0x63, 0x74, 0x69, 0x78
        }; // "conectix" at offset 0 (Windows Virtual PC Virtual Hard Disk file format)

    private static readonly byte[]
        RdbMagicNumber = new byte[] { 0x52, 0x44, 0x53, 0x4B }; // "RDSK" at sector 0-15 (Amiga Rigid Disk Block)

    private static readonly byte[]
        Iso9660MagicNumber = new byte[]
            { 0x43, 0x44, 0x30, 0x30, 0x31 }; // CD001 at offset 0x8001, 0x8801 or 0x9001 (ISO9660 CD/DVD image file)

    private static readonly byte[]
        AdfDosMagicNumber = new byte[] { 0x44, 0x4f, 0x53 }; // "DOS" at offset 0 (Amiga Disk File)

    private static readonly byte[] LhaMagicNumber = new byte[] { 0x2D, 0x6C, 0x68 }; // "-lh" at offset 2 (Lha archive)

    private static readonly byte[] LzwMagicNumber = new byte[] { 0x1f, 0x9d }; // offset 0 (.Z archive)
    
    private static readonly byte[] ZipMagicNumber1 = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // "PK" at offset 0 (Zip archive, normal archive)
    private static readonly byte[] ZipMagicNumber2 = new byte[] { 0x50, 0x4B, 0x05, 0x06 }; // "PK" at offset 0 (Zip archive, empty archive)
    private static readonly byte[] ZipMagicNumber3 = new byte[] { 0x50, 0x4B, 0x07, 0x08 }; // "PK" at offset 0 (Zip archive, spanned archive)
    
    private async Task<bool> IsAdfMedia(MediaResult mediaResult)
    {
        // return false, if media file doesnt exist
        if (!File.Exists(mediaResult.MediaPath))
        {
            return false;
        }

        await using var stream = File.OpenRead(mediaResult.MediaPath);

        stream.Seek(0, SeekOrigin.Begin);
        var sectorBytes = await stream.ReadBytes(512);

        if (!HasMagicNumber(AdfDosMagicNumber, sectorBytes, 0))
        {
            return false;
        }

        return sectorBytes[3] <= 7;
    }

    private async Task<bool> IsZipMedia(MediaResult mediaResult)
    {
        // return false, if media file doesnt exist
        if (!File.Exists(mediaResult.MediaPath))
        {
            return false;
        }

        await using var stream = File.OpenRead(mediaResult.MediaPath);

        stream.Seek(0, SeekOrigin.Begin);
        var sectorBytes = await stream.ReadBytes(512);

        return HasMagicNumber(ZipMagicNumber1, sectorBytes, 0) || HasMagicNumber(ZipMagicNumber2, sectorBytes, 0) ||
               HasMagicNumber(ZipMagicNumber3, sectorBytes, 0);
    }
    
    private async Task<bool> IsLhaMedia(MediaResult mediaResult)
    {
        // return false, if media file doesnt exist
        if (!File.Exists(mediaResult.MediaPath))
        {
            return false;
        }

        await using var stream = File.OpenRead(mediaResult.MediaPath);

        stream.Seek(0, SeekOrigin.Begin);
        var sectorBytes = await stream.ReadBytes(512);

        return HasMagicNumber(LhaMagicNumber, sectorBytes, 2);
    }

    private async Task<bool> IsLzwMedia(MediaResult mediaResult)
    {
        // return false, if media file doesnt exist
        if (!File.Exists(mediaResult.MediaPath))
        {
            return false;
        }

        await using var stream = File.OpenRead(mediaResult.MediaPath);

        stream.Seek(0, SeekOrigin.Begin);
        var sectorBytes = await stream.ReadBytes(512);

        return HasMagicNumber(LzwMagicNumber, sectorBytes, 0);
    }

    private async Task<bool> IsIso9660Media(MediaResult mediaResult)
    {
        // return false, if media file doesnt exist
        if (!File.Exists(mediaResult.MediaPath))
        {
            return false;
        }

        await using var stream = File.OpenRead(mediaResult.MediaPath);

        // offset: 0x8000 / sector 64
        stream.Seek(0x8000, SeekOrigin.Begin);
        var sectorBytes = await stream.ReadBytes(512);

        // return true, if offset 0x8001 has iso magic number
        if (HasMagicNumber(Iso9660MagicNumber, sectorBytes, 1))
        {
            return true;
        }

        // offset: 0x8800 / sector 68
        stream.Seek(0x8800, SeekOrigin.Begin);
        sectorBytes = await stream.ReadBytes(512);

        // return true, if offset 0x8801 has iso magic number
        if (HasMagicNumber(Iso9660MagicNumber, sectorBytes, 1))
        {
            return true;
        }

        // offset: 0x9000 / sector 72
        stream.Seek(0x9000, SeekOrigin.Begin);
        sectorBytes = await stream.ReadBytes(512);

        // return true, if offset 0x9001 has iso magic number
        return HasMagicNumber(Iso9660MagicNumber, sectorBytes, 1);
    }

    private async Task<bool> IsDiskMedia(MediaResult mediaResult)
    {
        // physical drive
        if (Regexs.PhysicalDrivePathRegex.IsMatch(mediaResult.MediaPath) ||
            Regexs.DevicePathRegex.IsMatch(mediaResult.MediaPath))
        {
            return true;
        }

        // return false, if media file doesnt exist
        if (!File.Exists(mediaResult.MediaPath))
        {
            return false;
        }

        await using var stream = File.OpenRead(mediaResult.MediaPath);
        return await HasDiskMediaMagicNumber(stream);
    }

    private async Task<bool> HasDiskMediaMagicNumber(Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        var sectorBytes = await stream.ReadBytes(VhdMagicNumber.Length);

        // virtual disk
        if (HasMagicNumber(VhdMagicNumber, sectorBytes, 0))
        {
            return true;
        }

        // rigid disk block
        for (var i = 1; i <= 16; i++)
        {
            if (HasMagicNumber(RdbMagicNumber, sectorBytes, 0))
            {
                return true;
            }

            // rigid disk block can only exist in sector 0-15
            if (i == 16)
            {
                break;
            }

            stream.Seek(i * 512, SeekOrigin.Begin);
            sectorBytes = await stream.ReadBytes(VhdMagicNumber.Length);
        }

        return false;
    }

    private bool HasMagicNumber(byte[] magicNumber, byte[] data, int dataOffset)
    {
        for (var i = 0; i < magicNumber.Length && dataOffset + i < data.Length; i++)
        {
            if (magicNumber[i] != data[dataOffset + i])
            {
                return false;
            }
        }

        return true;
    }

    protected Task<Result<IEntryIterator>> GetDirectoryEntryIterator(string path, bool recursive)
    {
        var fileName = Path.GetFileName(path);
        var hasPattern = fileName.IndexOf("*", StringComparison.OrdinalIgnoreCase) >= 0;
        
        var rootPath = hasPattern ? Path.GetDirectoryName(path) : path;
        var pattern = hasPattern ? fileName : null;
        
        return Task.FromResult(!Directory.Exists(rootPath)
            ? null
            : new Result<IEntryIterator>(new DirectoryEntryIterator(rootPath, pattern, recursive)));
    }

    protected Task<Result<IEntryIterator>> GetFileEntryIterator(string path, bool recursive)
    {
        return Task.FromResult(!File.Exists(path) ? null : new Result<IEntryIterator>(new FileEntryIterator(path)));
    }

    protected async Task<Result<IEntryIterator>> GetZipEntryIterator(MediaResult mediaResult, bool recursive)
    {
        if (!await IsZipMedia(mediaResult))
        {
            return null;
        }

        var zipStream = File.OpenRead(mediaResult.MediaPath);
        var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        return new Result<IEntryIterator>(new ZipArchiveEntryIterator(zipStream, mediaResult.FileSystemPath, zipArchive,
            recursive));
    }
    
    protected async Task<Result<IEntryIterator>> GetLhaEntryIterator(MediaResult mediaResult, bool recursive)
    {
        if (!await IsLhaMedia(mediaResult))
        {
            return null;
        }

        var lhaStream = File.OpenRead(mediaResult.MediaPath);
        var lhaArchive = new LhaArchive(lhaStream, LhaOptions.AmigaLhaOptions);

        return new Result<IEntryIterator>(new LhaArchiveEntryIterator(lhaStream, mediaResult.FileSystemPath, lhaArchive,
            recursive));
    }

    protected async Task<Result<IEntryIterator>> GetLzwEntryIterator(MediaResult mediaResult)
    {
        if (!await IsLzwMedia(mediaResult))
        {
            return null;
        }

        return new Result<IEntryIterator>(new LzwArchiveEntryIterator(mediaResult.MediaPath));
    }
    
    protected async Task<Result<IEntryIterator>> GetAdfEntryIterator(MediaResult mediaResult, bool recursive)
    {
        if (!await IsAdfMedia(mediaResult))
        {
            return null;
        }

        var adfStream = File.OpenRead(mediaResult.MediaPath);
        var fileSystemVolumeResult = await MountAdfFileSystemVolume(adfStream);
        if (fileSystemVolumeResult.IsFaulted)
        {
            return new Result<IEntryIterator>(fileSystemVolumeResult.Error);
        }

        return new Result<IEntryIterator>(new AmigaVolumeEntryIterator(adfStream, mediaResult.FileSystemPath,
            fileSystemVolumeResult.Value, recursive));
    }

    protected async Task<Result<IEntryIterator>> GetIso9660EntryIterator(MediaResult mediaResult, bool recursive)
    {
        if (!await IsIso9660Media(mediaResult))
        {
            return null;
        }

        var iso96690Stream = File.OpenRead(mediaResult.MediaPath);
        var cdReader = new CDReader(iso96690Stream, true);

        return new Result<IEntryIterator>(new Iso9660EntryIterator(iso96690Stream, mediaResult.FileSystemPath, cdReader,
            recursive));
    }

    protected async Task<Result<IEntryIterator>> GetDiskEntryIterator(MediaResult mediaResult, bool recursive,
        bool useCache, int cacheSize, int blockSize)
    {
        if (!await IsDiskMedia(mediaResult))
        {
            return null;
        }

        if (string.IsNullOrEmpty(mediaResult.FileSystemPath))
        {
            return new Result<IEntryIterator>(
                new Error($"Partition table not found in path '{mediaResult.FileSystemPath}'"));
        }

        var media = commandHelper.GetReadableMedia(physicalDrives, mediaResult.MediaPath);
        if (media.IsFaulted)
        {
            return new Result<IEntryIterator>(media.Error);
        }

        var parts = mediaResult.FileSystemPath.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return new Result<IEntryIterator>(new Error($"No partition table in path"));
        }
        
        if (!parts[0].Equals("rdb", StringComparison.OrdinalIgnoreCase))
        {
            return new Result<IEntryIterator>(new Error($"Unsupported partition table '{parts[0]}'"));
        }

        if (parts.Length == 1)
        {
            return new Result<IEntryIterator>(new Error($"No device name in path"));
        }
        
        // if (blockSize < 1024 * 512)
        // {
        //     blockSize = 1024 * 512;
        // }

        // var blocksLimit = cacheSize / blockSize;
        //
        // if (blocksLimit < 10)
        // {
        //     blocksLimit = 10;
        // }
        
        // var stream = useCache ? new CachedStream(media.Value.Stream, blockSize, blocksLimit) : media.Value.Stream;
        var stream = media.Value.Stream;
        //var stream = new CachedBlockStream(media.Value.Stream);

        var fileSystemVolumeResult = await MountRdbFileSystemVolume(stream, parts[1]);
        if (fileSystemVolumeResult.IsFaulted)
        {
            return new Result<IEntryIterator>(fileSystemVolumeResult.Error);
        }

        var rootPath = string.Join("/", parts.Skip(2));
        return new Result<IEntryIterator>(new AmigaVolumeEntryIterator(media.Value.Stream, rootPath,
            fileSystemVolumeResult.Value, recursive));
    }

    protected async Task<Result<IEntryWriter>> GetEntryWriter(string destPath, bool useCache)
    {
        // resolve media path
        var mediaResult = ResolveMedia(destPath);

        // return directory writer, if media path doesn't exist. otherwise return error
        if (mediaResult.IsFaulted)
        {
            return mediaResult.Error is PathNotFoundError
                ? new Result<IEntryWriter>(new DirectoryEntryWriter(destPath))
                : new Result<IEntryWriter>(mediaResult.Error);
        }

        OnDebugMessage($"Media Path: '{mediaResult.Value.MediaPath}'");
        OnDebugMessage($"Virtual Path: '{mediaResult.Value.FileSystemPath}'");

        // adf
        if (File.Exists(mediaResult.Value.MediaPath) &&
            (Path.GetExtension(mediaResult.Value.MediaPath) ?? string.Empty).Equals(".adf",
                StringComparison.OrdinalIgnoreCase))
        {
            var adfStream = File.Open(mediaResult.Value.MediaPath, FileMode.Open, FileAccess.ReadWrite);
            var fileSystemVolumeResult = await MountAdfFileSystemVolume(adfStream);
            if (fileSystemVolumeResult.IsFaulted)
            {
                return new Result<IEntryWriter>(fileSystemVolumeResult.Error);
            }

            var adfMedia = new Media(mediaResult.Value.MediaPath, Path.GetFileName(mediaResult.Value.MediaPath),
                adfStream.Length, Media.MediaType.Raw, false, adfStream);

            return new Result<IEntryWriter>(new AmigaVolumeEntryWriter(adfMedia,
                mediaResult.Value.FileSystemPath.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries), 
                fileSystemVolumeResult.Value));
        }

        // disk
        var media = commandHelper.GetWritableMedia(physicalDrives, mediaResult.Value.MediaPath);
        if (media.IsFaulted)
        {
            return new Result<IEntryWriter>(media.Error);
        }

        var parts = mediaResult.Value.FileSystemPath.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return new Result<IEntryWriter>(new Error($"No partition table in path"));
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "rdb":
                if (parts.Length == 1)
                {
                    return new Result<IEntryWriter>(new Error($"No device name in path"));
                }

                var stream = useCache ? new CachedBlockStream(media.Value.Stream) : media.Value.Stream;
                var fileSystemVolumeResult = await MountRdbFileSystemVolume(stream, parts[1]);
                if (fileSystemVolumeResult.IsFaulted)
                {
                    return new Result<IEntryWriter>(fileSystemVolumeResult.Error);
                }

                // skip 2 first parts, partition table and device/drive name
                return new Result<IEntryWriter>(new AmigaVolumeEntryWriter(media.Value, parts.Skip(2).ToArray(),
                    fileSystemVolumeResult.Value));
            default:
                return new Result<IEntryWriter>(new Error($"Unsupported partition table '{parts[0]}'"));
        }
    }

    protected Result<MediaResult> ResolveMedia(string path)
    {
        path = PathHelper.GetFullPath(path);
        string mediaPath;
        var directorySeparatorChar = Path.DirectorySeparatorChar.ToString();

        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] == '\\' || path[i] == '/')
            {
                directorySeparatorChar = path[i].ToString();
                break;
            }
        }

        // physical drive
        var physicalDrivePathMatch = Regexs.PhysicalDrivePathRegex.Match(path);
        if (physicalDrivePathMatch.Success)
        {
            mediaPath = physicalDrivePathMatch.Value;
            var firstSeparatorIndex = path.IndexOf(directorySeparatorChar, mediaPath.Length, StringComparison.Ordinal);

            return new Result<MediaResult>(new MediaResult
            {
                FullPath = path,
                MediaPath = mediaPath,
                FileSystemPath = firstSeparatorIndex >= 0
                    ? path.Substring(firstSeparatorIndex + 1, path.Length - (firstSeparatorIndex + 1))
                    : string.Empty,
                DirectorySeparatorChar = directorySeparatorChar
            });
        }

        // media file
        var next = 0;
        do
        {
            next = path.IndexOf(directorySeparatorChar, next + 1, StringComparison.OrdinalIgnoreCase);
            mediaPath = path.Substring(0, next == -1 ? path.Length : next);

            if (File.Exists(mediaPath))
            {
                return new Result<MediaResult>(new MediaResult
                {
                    FullPath = path,
                    MediaPath = mediaPath,
                    FileSystemPath = mediaPath.Length + 1 < path.Length
                        ? path.Substring(mediaPath.Length + 1, path.Length - (mediaPath.Length + 1))
                        : string.Empty,
                    DirectorySeparatorChar = directorySeparatorChar
                });
            }

            if (!Directory.Exists(mediaPath))
            {
                break;
            }
        } while (next != -1);

        return new Result<MediaResult>(new PathNotFoundError($"Media not '{path}' found", path));
    }

    protected async Task<Result<IFileSystemVolume>> MountAdfFileSystemVolume(Stream stream)
    {
        OnDebugMessage("Mounting ADF file system volume using Fast File System");

        var fileSystemVolume = await FastFileSystemVolume.MountAdf(stream);

        OnDebugMessage($"Volume '{fileSystemVolume.Name}'");

        return new Result<IFileSystemVolume>(fileSystemVolume);
    }

    protected async Task<Result<IFileSystemVolume>> MountRdbFileSystemVolume(Stream stream, string partitionName)
    {
        OnDebugMessage("Reading Rigid Disk Block");

        var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);

        if (rigidDiskBlock == null)
        {
            return new Result<IFileSystemVolume>(new Error("No rigid disk block"));
        }

        OnDebugMessage($"Partition '{partitionName}'");

        var partitionBlock = rigidDiskBlock.PartitionBlocks.FirstOrDefault(x =>
            x.DriveName.Equals(partitionName, StringComparison.OrdinalIgnoreCase));

        if (partitionBlock == null)
        {
            return new Result<IFileSystemVolume>(new Error($"Partition '{partitionName}' not found"));
        }

        OnDebugMessage($"Mounting file system");

        var fileSystemVolumeResult = await MountPartitionFileSystemVolume(stream, partitionBlock);
        if (fileSystemVolumeResult.IsFaulted)
        {
            return new Result<IFileSystemVolume>(fileSystemVolumeResult.Error);
        }

        OnDebugMessage($"Volume '{fileSystemVolumeResult.Value.Name}'");

        var fileSystemVolume = fileSystemVolumeResult.Value;
        if (fileSystemVolume == null)
        {
            return new Result<IFileSystemVolume>(new Error("No file system volume mounted"));
        }

        return new Result<IFileSystemVolume>(fileSystemVolume);
    }

    protected async Task<Result<IFileSystemVolume>> MountPartitionFileSystemVolume(Stream stream,
        PartitionBlock partitionBlock)
    {
        // var cached = new CachedBlockStream(stream, 512);
        switch (partitionBlock.DosTypeFormatted)
        {
            case "DOS\\0":
            case "DOS\\1":
            case "DOS\\2":
            case "DOS\\3":
            case "DOS\\4":
            case "DOS\\5":
            case "DOS\\6":
            case "DOS\\7":
                return new Result<IFileSystemVolume>(await FastFileSystemVolume.MountPartition(stream, partitionBlock));
            case "PDS\\3":
            case "PFS\\3":
                return new Result<IFileSystemVolume>(await Pfs3Volume.Mount(stream, partitionBlock));
        }

        return new Result<IFileSystemVolume>(new Error($"Unsupported file system '{partitionBlock.DosTypeFormatted}'"));
    }
}