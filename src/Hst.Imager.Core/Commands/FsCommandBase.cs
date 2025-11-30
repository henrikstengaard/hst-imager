using DiscUtils.Ntfs;
using Hst.Imager.Core.Extensions;

namespace Hst.Imager.Core.Commands;

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
using Compression.Lzx;
using DiscUtils;
using DiscUtils.Ext;
using DiscUtils.Fat;
using DiscUtils.Iso9660;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Core;
using Hst.Core.Extensions;
using Helpers;
using Models;
using Directory = System.IO.Directory;
using File = System.IO.File;
using PathComponents;
using ExFat.DiscUtils;
using System.Text.RegularExpressions;

public abstract partial class FsCommandBase : CommandBase
{
    protected readonly ICommandHelper commandHelper;
    protected readonly IEnumerable<IPhysicalDrive> physicalDrives;

    protected FsCommandBase(ICommandHelper commandHelper, IEnumerable<IPhysicalDrive> physicalDrives)
    {
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
    }
    
    protected async Task<bool> IsAdfMedia(MediaResult mediaResult)
    {
        // return false, if media file doesnt exist
        if (!File.Exists(mediaResult.MediaPath))
        {
            return false;
        }

        await using var stream = File.OpenRead(mediaResult.MediaPath);

        stream.Seek(0, SeekOrigin.Begin);
        var sectorBytes = await stream.ReadBytes(512);

        if (!MagicBytes.HasMagicNumber(MagicBytes.AdfDosMagicNumber, sectorBytes, 0))
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

        return MagicBytes.HasMagicNumber(MagicBytes.ZipMagicNumber1, sectorBytes, 0) || MagicBytes.HasMagicNumber(MagicBytes.ZipMagicNumber2, sectorBytes, 0) ||
               MagicBytes.HasMagicNumber(MagicBytes.ZipMagicNumber3, sectorBytes, 0);
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

        return MagicBytes.HasMagicNumber(MagicBytes.LhaMagicNumber, sectorBytes, 2);
    }

    private async Task<bool> IsLzxMedia(MediaResult mediaResult)
    {
        // return false, if media file doesnt exist
        if (!File.Exists(mediaResult.MediaPath))
        {
            return false;
        }

        await using var stream = File.OpenRead(mediaResult.MediaPath);

        stream.Seek(0, SeekOrigin.Begin);
        var sectorBytes = await stream.ReadBytes(512);

        return MagicBytes.HasMagicNumber(MagicBytes.LzxMagicNumber, sectorBytes, 0);
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

        return MagicBytes.HasMagicNumber(MagicBytes.LzwMagicNumber, sectorBytes, 0);
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
        if (MagicBytes.HasMagicNumber(MagicBytes.Iso9660MagicNumber, sectorBytes, 1))
        {
            return true;
        }

        // offset: 0x8800 / sector 68
        stream.Seek(0x8800, SeekOrigin.Begin);
        sectorBytes = await stream.ReadBytes(512);

        // return true, if offset 0x8801 has iso magic number
        if (MagicBytes.HasMagicNumber(MagicBytes.Iso9660MagicNumber, sectorBytes, 1))
        {
            return true;
        }

        // offset: 0x9000 / sector 72
        stream.Seek(0x9000, SeekOrigin.Begin);
        sectorBytes = await stream.ReadBytes(512);

        // return true, if offset 0x9001 has iso magic number
        return MagicBytes.HasMagicNumber(MagicBytes.Iso9660MagicNumber, sectorBytes, 1);
    }

    protected async Task<bool> IsDiskMedia(MediaResult mediaResult)
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
        var sectorBytes = await stream.ReadBytes(MagicBytes.VhdMagicNumber.Length);

        // virtual disk
        if (MagicBytes.HasMagicNumber(MagicBytes.VhdMagicNumber, sectorBytes, 0))
        {
            return true;
        }

        // rigid disk block
        for (var i = 1; i <= 16; i++)
        {
            if (MagicBytes.HasMagicNumber(MagicBytes.RdbMagicNumber, sectorBytes, 0))
            {
                return true;
            }

            // rigid disk block can only exist in sector 0-15
            if (i == 16)
            {
                break;
            }

            stream.Seek(i * 512, SeekOrigin.Begin);
            sectorBytes = await stream.ReadBytes(MagicBytes.VhdMagicNumber.Length);
        }

        return false;
    }

    protected Task<Result<IEntryIterator>> GetDirectoryEntryIterator(string path, bool recursive)
    {
        var dirPath = Path.GetDirectoryName(path) ?? string.Empty;

        return Task.FromResult(Directory.Exists(path) || Directory.Exists(dirPath)
            ? new Result<IEntryIterator>(new DirectoryEntryIterator(path, recursive))
            : new Result<IEntryIterator>(new PathNotFoundError($"Path not found '{path}'", path)));
    }

    protected Task<Result<IEntryIterator>> GetFileEntryIterator(string path, bool recursive)
    {
        path = PathHelper.GetFullPath(path);
        return Task.FromResult(new Result<IEntryIterator>(
            new DirectoryEntryIterator(path, recursive)));
    }

    protected async Task<Result<IEntryIterator>> GetZipEntryIterator(MediaResult resolvedMedia, bool recursive)
    {
        if (!await IsZipMedia(resolvedMedia))
        {
            return null;
        }

        var mediaResult = await commandHelper.GetReadableFileMedia(resolvedMedia.MediaPath);
        if (mediaResult.IsFaulted)
        {
            return new Result<IEntryIterator>(mediaResult.Error);
        }
        
        var zipArchive = new ZipArchive(mediaResult.Value.Stream, ZipArchiveMode.Read);

        var rootPathComponents = resolvedMedia.FileSystemPath.Split(resolvedMedia.DirectorySeparatorChar);
        var rootPath = MediaPath.ZipArchivePath.Join(rootPathComponents);

        return new Result<IEntryIterator>(new ZipArchiveEntryIterator(mediaResult.Value.Stream, rootPath, zipArchive,
            recursive));
    }
    
    protected async Task<Result<IEntryIterator>> GetLhaEntryIterator(MediaResult resolvedMedia, bool recursive)
    {
        if (!await IsLhaMedia(resolvedMedia))
        {
            return null;
        }

        var mediaResult = await commandHelper.GetReadableFileMedia(resolvedMedia.MediaPath);
        if (mediaResult.IsFaulted)
        {
            return new Result<IEntryIterator>(mediaResult.Error);
        }
        
        var lhaArchive = new LhaArchive(mediaResult.Value.Stream, LhaOptions.AmigaLhaOptions);

        var rootPathComponents = resolvedMedia.FileSystemPath.Split(resolvedMedia.DirectorySeparatorChar);
        var rootPath = MediaPath.LhaArchivePath.Join(rootPathComponents);

        return new Result<IEntryIterator>(new LhaArchiveEntryIterator(mediaResult.Value.Stream, rootPath, lhaArchive,
            recursive));
    }

    protected async Task<Result<IEntryIterator>> GetLzxEntryIterator(MediaResult resolvedMedia, bool recursive)
    {
        if (!await IsLzxMedia(resolvedMedia))
        {
            return null;
        }

        var mediaResult = await commandHelper.GetReadableFileMedia(resolvedMedia.MediaPath);
        if (mediaResult.IsFaulted)
        {
            return new Result<IEntryIterator>(mediaResult.Error);
        }

        var lzxArchive = new LzxArchive(mediaResult.Value.Stream, LzxOptions.AmigaLzxOptions);

        var rootPathComponents = resolvedMedia.FileSystemPath.Split(resolvedMedia.DirectorySeparatorChar);
        var rootPath = MediaPath.LzxArchivePath.Join(rootPathComponents);

        return new Result<IEntryIterator>(new LzxArchiveEntryIterator(mediaResult.Value.Stream, rootPath, lzxArchive,
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
    
    protected async Task<Result<IEntryIterator>> GetAdfEntryIterator(MediaResult resolvedMedia, bool recursive)
    {
        if (!await IsAdfMedia(resolvedMedia))
        {
            return null;
        }

        var mediaResult = await commandHelper.GetReadableFileMedia(resolvedMedia.MediaPath);
        if (mediaResult.IsFaulted)
        {
            return new Result<IEntryIterator>(mediaResult.Error);
        }
        
        var fileSystemVolumeResult = await MountAdfFileSystemVolume(mediaResult.Value.Stream);
        if (fileSystemVolumeResult.IsFaulted)
        {
            return new Result<IEntryIterator>(fileSystemVolumeResult.Error);
        }

        var rootPathComponents = string.IsNullOrEmpty(resolvedMedia.FileSystemPath)
            ? []
            : resolvedMedia.FileSystemPath.Split(resolvedMedia.DirectorySeparatorChar);

        return new Result<IEntryIterator>(new AmigaVolumeEntryIterator(mediaResult.Value,
            PartitionTableType.RigidDiskBlock, 0, fileSystemVolumeResult.Value, rootPathComponents,
            recursive));
    }

    protected async Task<Result<IEntryIterator>> GetIso9660EntryIterator(MediaResult resolvedMedia, bool recursive)
    {
        if (!await IsIso9660Media(resolvedMedia))
        {
            return null;
        }

        var mediaResult = await commandHelper.GetReadableFileMedia(resolvedMedia.MediaPath);
        if (mediaResult.IsFaulted)
        {
            return new Result<IEntryIterator>(mediaResult.Error);
        }

        var cdReader = new CDReader(mediaResult.Value.Stream, true);

        var rootPathComponents = resolvedMedia.FileSystemPath.Split(resolvedMedia.DirectorySeparatorChar);
        var rootPath = MediaPath.Iso9660Path.Join(rootPathComponents);

        return new Result<IEntryIterator>(new Iso9660EntryIterator(mediaResult.Value.Stream, rootPath, cdReader,
            recursive));
    }

    protected async Task<Result<IEntryIterator>> GetDiskEntryIterator(MediaResult mediaResult, bool recursive,
        bool useCache, int cacheSize, int blockSize)
    {
        if (string.IsNullOrEmpty(mediaResult.FileSystemPath))
        {
            return new Result<IEntryIterator>(
                new Error($"Partition table not found in path '{mediaResult.FileSystemPath}'"));
        }

        var readableMediaResult = await commandHelper.GetReadableMedia(physicalDrives, mediaResult.MediaPath, mediaResult.Modifiers);
        if (readableMediaResult.IsFaulted)
        {
            return new Result<IEntryIterator>(readableMediaResult.Error);
        }

        var fileSystemPath = mediaResult.FileSystemPath ?? string.Empty;
        var directorySeparatorChar = mediaResult.DirectorySeparatorChar;

        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(
            readableMediaResult.Value, fileSystemPath, directorySeparatorChar);

        var media = piStormRdbMediaResult.Media;
        fileSystemPath = piStormRdbMediaResult.FileSystemPath;

        var parts = fileSystemPath.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);

        if (media.Type == Media.MediaType.Floppy)
        {
            return await GetFloppyFileSystemEntryIterator(media, parts, recursive);
        }
        
        if (parts.Length == 0)
        {
            return new Result<IEntryIterator>(new Error($"No partition table in path"));
        }

        return parts[0].ToLowerInvariant() switch
        {
            "mbr" => await GetMbrFileSystemEntryIterator(media, parts, recursive),
            "gpt" => await GetGptFileSystemEntryIterator(media, parts, recursive),
            "rdb" => await GetAmigaVolumeEntryIterator(media, parts, recursive),
            _ => new Result<IEntryIterator>(new Error($"Unsupported partition table '{parts[0]}'"))
        };
    }

    private async Task<Result<IEntryIterator>> GetAmigaVolumeEntryIterator(Media media, string[] parts, bool recursive)
    {
        if (parts.Length < 2)
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
        //var stream = media.Stream;
        //var stream = new CachedBlockStream(media.Value.Stream);
        var disk = media is DiskMedia diskMedia 
            ? diskMedia.Disk
            : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);

        var fileSystemVolumeResult = await MountRdbFileSystemVolume(media, parts[1]);
        if (fileSystemVolumeResult.IsFaulted)
        {
            return new Result<IEntryIterator>(fileSystemVolumeResult.Error);
        }

        var (partitionNumber, fileSystemVolume) = fileSystemVolumeResult.Value;

        var rootPathComponents = parts.Skip(2).ToArray();
        return new Result<IEntryIterator>(new AmigaVolumeEntryIterator(media, PartitionTableType.RigidDiskBlock,
            partitionNumber, fileSystemVolume, rootPathComponents, recursive));
    }

    private async Task<Result<IEntryIterator>> GetFloppyFileSystemEntryIterator(Media media, string[] parts, bool recursive)
    {
        var fileSystemResult = await MountFileSystem(media.Stream);
        if (fileSystemResult.IsFaulted)
        {
            return new Result<IEntryIterator>(fileSystemResult.Error);
        }

        return new Result<IEntryIterator>(new FileSystemEntryIterator(media, PartitionTableType.None, 0,
            fileSystemResult.Value, parts, recursive));
    }

    private async Task<Result<IEntryIterator>> GetMbrFileSystemEntryIterator(Media media, string[] parts, bool recursive)
    {
        if (parts.Length < 2)
        {
            return new Result<IEntryIterator>(new Error($"No partition number in path"));
        }

        // open stream as disk
        var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);

        var mbrFileSystemResult = await MountMbrFileSystem(disk, parts[1]);
        if (mbrFileSystemResult.IsFaulted)
        {
            return new Result<IEntryIterator>(mbrFileSystemResult.Error);
        }
        
        var (partitionNumber, fileSystem) = mbrFileSystemResult.Value;

        var rootPathComponents = parts.Skip(2).ToArray();
        return new Result<IEntryIterator>(new FileSystemEntryIterator(media, PartitionTableType.MasterBootRecord,
            partitionNumber, fileSystem, rootPathComponents, recursive));
    }

    private async Task<Result<IEntryIterator>> GetGptFileSystemEntryIterator(Media media, string[] parts, bool recursive)
    {
        if (parts.Length < 2)
        {
            return new Result<IEntryIterator>(new Error($"No partition number in path"));
        }

        // open stream as disk
        var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);

        var gptFileSystemResult = await MountGptFileSystem(disk, parts[1]);
        if (gptFileSystemResult.IsFaulted)
        {
            return new Result<IEntryIterator>(gptFileSystemResult.Error);
        }

        var (partitionNumber, fileSystem) = gptFileSystemResult.Value;

        var rootPathComponents = parts.Skip(2).ToArray();
        return new Result<IEntryIterator>(new FileSystemEntryIterator(media, PartitionTableType.GuidPartitionTable,
            partitionNumber, fileSystem, rootPathComponents, recursive));
    }

    private async Task<Result<IEntryWriter>> GetDirectoryEntryWriter(string path, bool recursive, bool createDirectory,
        bool forceOverwrite)
    {
        // ensure path is full path
        path = PathHelper.GetFullPath(path);

        var dirPath = Path.GetDirectoryName(path);
        
        // return path not found error, if path doesn't exist, dir path doesn't exist and not create directory.
        // last path component is allow to support local directory writer can write a non existing file.
        if (!Directory.Exists(path) && !Directory.Exists(dirPath) && !createDirectory)
        {
            return new Result<IEntryWriter>(new PathNotFoundError($"Path not found '{path}'", path));
        }
        
        var directoryEntryWriter = new DirectoryEntryWriter(path, recursive, createDirectory, forceOverwrite);

        var initializeResult = await directoryEntryWriter.Initialize();
        return initializeResult.IsFaulted
            ? new Result<IEntryWriter>(initializeResult.Error)
            : new Result<IEntryWriter>(directoryEntryWriter);
    }

    private async Task<Result<bool>> IsFileDiskMedia(string path, ModifierEnum modifiers)
    {
        var readableMediaResult = await commandHelper.GetReadableFileMedia(path, modifiers);
        if (readableMediaResult.IsFaulted)
        {
            return new Result<bool>(readableMediaResult.Error);
        }

        using var media = readableMediaResult.Value;
        var stream = media.Stream;

        // read sector 0
        var sectorBytes = new byte[512];
        var bytesRead = await stream.ReadAsync(sectorBytes, 0, sectorBytes.Length);
        if (bytesRead != sectorBytes.Length)
        {
            return new Result<bool>(false);
        }

        // return true, if media has adf size and has dos magic number
        if (media.Size == Amiga.FloppyDiskConstants.DoubleDensity.Size &&
            sectorBytes.HasMagicNumber(MagicBytes.AdfDosMagicNumber))
        {
            return new Result<bool>(true);
        }
        
        // return true, if sector 0 has mbr, rdb or vhd magic number
        if (sectorBytes.HasMagicNumber(MagicBytes.MbrMagicNumber, 0x1fe) ||
            sectorBytes.HasMagicNumber(MagicBytes.RdbMagicNumber) ||
            sectorBytes.HasMagicNumber(MagicBytes.VhdMagicNumber))
        {
            return new Result<bool>(true);
        }

        // read sector 1
        bytesRead = await stream.ReadAsync(sectorBytes, 0, sectorBytes.Length);
        if (bytesRead != sectorBytes.Length)
        {
            return new Result<bool>(false);
        }

        // return true, if sector 1 has gpr or rdb magic number
        if (sectorBytes.HasMagicNumber(MagicBytes.GptMagicNumber) ||
            sectorBytes.HasMagicNumber(MagicBytes.RdbMagicNumber))
        {
            return new Result<bool>(true);
        }

        // read sector 2-15
        for (var i = 2; i <= 15; i++)
        {
            // read sector
            bytesRead = await stream.ReadAsync(sectorBytes, 0, sectorBytes.Length);
            if (bytesRead != sectorBytes.Length)
            {
                return new Result<bool>(false);
            }
            
            // return true, if sector 1 has rdb magic number
            if (sectorBytes.HasMagicNumber(MagicBytes.RdbMagicNumber))
            {
                return new Result<bool>(true);
            }
        }
        
        return new Result<bool>(false);
    }
    
    protected async Task<Result<IEntryWriter>> GetEntryWriter(string destPath, bool recursive, bool createDestDirectory,
        bool forceOverwrite)
    {
        // resolve media path
        var mediaResult = commandHelper.ResolveMedia(destPath);

        // return directory writer, if media path doesn't exist. otherwise return error
        if (mediaResult.IsFaulted)
        {
            if (mediaResult.Error is not PathNotFoundError)
            {
                return new Result<IEntryWriter>(mediaResult.Error);
            }

            return await GetDirectoryEntryWriter(destPath, recursive, createDestDirectory, forceOverwrite);
        }

        if (Directory.Exists(destPath))
        {
            return await GetDirectoryEntryWriter(destPath, recursive, createDestDirectory, forceOverwrite);
        }

        OnDebugMessage($"Media Path: '{mediaResult.Value.MediaPath}'");
        OnDebugMessage($"Virtual Path: '{mediaResult.Value.FileSystemPath}'");

        if (File.Exists(destPath) &&
            string.IsNullOrWhiteSpace(mediaResult.Value.FileSystemPath))
        {
            return forceOverwrite
                ? await GetDirectoryEntryWriter(destPath, recursive, createDestDirectory, true)
                : new Result<IEntryWriter>(new FileExistsError($"File already exists '{destPath}'"));
        }

        var writableMediaResult = await commandHelper.GetWritableMedia(physicalDrives, mediaResult.Value.MediaPath, mediaResult.Value.Modifiers);
        if (writableMediaResult.IsFaulted)
        {
            return new Result<IEntryWriter>(writableMediaResult.Error);
        }

        var fileSystemPath = mediaResult.Value.FileSystemPath ?? string.Empty;
        var directorySeparatorChar = mediaResult.Value.DirectorySeparatorChar;

        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(
            writableMediaResult.Value, fileSystemPath, directorySeparatorChar);

        var media = piStormRdbMediaResult.Media;
        fileSystemPath = piStormRdbMediaResult.FileSystemPath;

        // adf
        if ((Path.GetExtension(mediaResult.Value.MediaPath) ?? string.Empty).Equals(".adf",
                StringComparison.OrdinalIgnoreCase))
        {
            var stream = media.Stream;
            var fileSystemVolumeResult = await MountAdfFileSystemVolume(stream);
            if (fileSystemVolumeResult.IsFaulted)
            {
                return new Result<IEntryWriter>(fileSystemVolumeResult.Error);
            }

            var adfAmigaVolumeEntryWriter = new AmigaVolumeEntryWriter(media, PartitionTableType.RigidDiskBlock,
                0, string.Empty,
                fileSystemPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries), recursive,
                fileSystemVolumeResult.Value, createDestDirectory, forceOverwrite);

            var adfInitializeResult = await adfAmigaVolumeEntryWriter.Initialize();
            return adfInitializeResult.IsFaulted
                ? new Result<IEntryWriter>(adfInitializeResult.Error)
                : new Result<IEntryWriter>(adfAmigaVolumeEntryWriter);
        }

        // disk
        var parts = fileSystemPath.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return new Result<IEntryWriter>(new Error($"No partition table in path"));
        }

        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);

        switch (parts[0].ToLowerInvariant())
        {
            case "mbr":
                var mbrFileSystemResult = await MountMbrFileSystem(disk, parts[1]);
                if (mbrFileSystemResult.IsFaulted)
                {
                    return new Result<IEntryWriter>(mbrFileSystemResult.Error);
                }
                
                var (mbrPartitionNumber, mbrFileSystem) = mbrFileSystemResult.Value;
                
                // skip 2 first parts, partition table and partition number
                var fileSystemEntryWriter = new FileSystemEntryWriter(media, PartitionTableType.MasterBootRecord,
                    mbrPartitionNumber, mbrFileSystem, parts.Skip(2).ToArray(), recursive,
                    createDestDirectory, forceOverwrite);

                // initialize file system entry writer
                var mbrInitializeResult = await fileSystemEntryWriter.Initialize();
                return mbrInitializeResult.IsFaulted
                    ? new Result<IEntryWriter>(mbrInitializeResult.Error)
                    : new Result<IEntryWriter>(fileSystemEntryWriter);
            case "gpt":
                var gptFileSystemResult = await MountGptFileSystem(disk, parts[1]);
                if (gptFileSystemResult.IsFaulted)
                {
                    return new Result<IEntryWriter>(gptFileSystemResult.Error);
                }
                
                var (gptPartitionNumber, gptFileSystem) = gptFileSystemResult.Value;

                // skip 2 first parts, partition table and partition number
                var gptFileSystemEntryWriter = new FileSystemEntryWriter(media, PartitionTableType.GuidPartitionTable,
                    gptPartitionNumber, gptFileSystem, parts.Skip(2).ToArray(), recursive,
                    createDestDirectory, forceOverwrite);
                
                // initialize file system entry writer
                var gptInitializeResult = await gptFileSystemEntryWriter.Initialize();
                return gptInitializeResult.IsFaulted
                    ? new Result<IEntryWriter>(gptInitializeResult.Error)
                    : new Result<IEntryWriter>(gptFileSystemEntryWriter);
            case "rdb":
                if (parts.Length == 1)
                {
                    return new Result<IEntryWriter>(new Error($"No device name in path"));
                }

                var rdbRileSystemVolumeResult = await MountRdbFileSystemVolume(media, parts[1]);
                if (rdbRileSystemVolumeResult.IsFaulted)
                {
                    return new Result<IEntryWriter>(rdbRileSystemVolumeResult.Error);
                }

                var (rdbPartitionNumber, rdbFileSystemVolume) = rdbRileSystemVolumeResult.Value;

                // skip 2 first parts, partition table and device/drive name
                var amigaVolumeEntryWriter = new AmigaVolumeEntryWriter(media, PartitionTableType.RigidDiskBlock,
                    rdbPartitionNumber, fileSystemPath, parts.Skip(2).ToArray(), recursive,
                    rdbFileSystemVolume, createDestDirectory, forceOverwrite);
                
                // initialize amiga volume entry writer
                var rdbInitializeResult = await amigaVolumeEntryWriter.Initialize();
                return rdbInitializeResult.IsFaulted
                    ? new Result<IEntryWriter>(rdbInitializeResult.Error)
                    : new Result<IEntryWriter>(amigaVolumeEntryWriter);
            default:
                return new Result<IEntryWriter>(new Error($"Unsupported partition table '{parts[0]}'"));
        }
    }

    protected async Task<Result<IFileSystemVolume>> MountAdfFileSystemVolume(Stream stream)
    {
        OnDebugMessage("Mounting ADF file system volume using Fast File System");

        var fileSystemVolume = await FastFileSystemVolume.MountAdf(stream);

        OnDebugMessage($"Volume '{fileSystemVolume.Name}'");

        return new Result<IFileSystemVolume>(fileSystemVolume);
    }

    protected async Task<Result<(int, IFileSystem)>> MountMbrFileSystem(VirtualDisk disk, string partitionNumber)
    {
        if (!int.TryParse(partitionNumber, out var partitionNumberIntValue))
        {
            return new Result<(int, IFileSystem)>(new Error($"Invalid partition number '{partitionNumber}'"));
        }

        OnDebugMessage("Reading Master Boot Record");

        BiosPartitionTable biosPartitionTable;
        try
        {
            biosPartitionTable = new BiosPartitionTable(disk);
        }
        catch (Exception)
        {
            return new Result<(int, IFileSystem)>(new Error("Master Boot Record not found"));
        }

        OnDebugMessage($"Partition number '{partitionNumber}'");

        if (partitionNumberIntValue < 0 || partitionNumberIntValue - 1 > biosPartitionTable.Partitions.Count)
        {
            return new Result<(int, IFileSystem)>(new Error(
                $"Invalid partition number {partitionNumber}, expected to between 1-{biosPartitionTable.Partitions.Count}"));
        }

        var partitionInfo = biosPartitionTable.Partitions[partitionNumberIntValue - 1];

        OnDebugMessage($"Mounting file system");

        var stream = partitionInfo.Open();

        var fileSystemResult = await MountFileSystem(stream);
        return fileSystemResult.IsFaulted
            ? new Result<(int, IFileSystem)>(fileSystemResult.Error)
            : new Result<(int, IFileSystem)>((partitionNumberIntValue, fileSystemResult.Value));
    }
    
    protected static async Task<Result<IFileSystem>> MountFileSystem(Stream stream)
    {
        // read first 64kb block bytes of partition
        var blockBytes = await stream.ReadBytes(512 * 128);

        // ext super block magic signature
        if (blockBytes[1024 + 0x38] == 0x53 && blockBytes[1024 + 0x39] == 0xEF)
        {
            return new Result<IFileSystem>(new ExtFileSystem(stream));
        }
        
        if (FatFileSystem.Detect(stream))
        {
            return new Result<IFileSystem>(new FatFileSystem(stream));
        }

        if (ExFatFileSystem.Detect(stream))
        {
            return new Result<IFileSystem>(new ExFatFileSystem(stream));
        }

        if (NtfsFileSystem.Detect(stream))
        {
            return new Result<IFileSystem>(new NtfsFileSystem(stream));
        }
        
        return new Result<IFileSystem>(new Error("Unsupported Master Boot Record file system"));
    }

    protected async Task<Result<(int, IFileSystem)>> MountGptFileSystem(VirtualDisk disk, string partitionNumber)
    {
        if (!int.TryParse(partitionNumber, out var partitionNumberIntValue))
        {
            return new Result<(int, IFileSystem)>(new Error($"Invalid partition number '{partitionNumber}'"));
        }
        
        OnDebugMessage("Reading Guid Partition Table");
        
        GuidPartitionTable guidPartitionTable;
        try
        {
            guidPartitionTable = new GuidPartitionTable(disk);
        }
        catch (Exception)
        {
            return new Result<(int, IFileSystem)>(new Error("Guid Partition Table not found"));
        }

        OnDebugMessage($"Partition number '{partitionNumber}'");

        if (partitionNumberIntValue < 0 || partitionNumberIntValue - 1 > guidPartitionTable.Partitions.Count)
        {
            return new Result<(int, IFileSystem)>(new Error($"Invalid partition number {partitionNumber}, expected to between 1-{guidPartitionTable.Partitions.Count}"));
        }

        var partitionInfo = guidPartitionTable.Partitions[partitionNumberIntValue - 1];

        OnDebugMessage($"Mounting file system");

        var stream = partitionInfo.Open();

        var fileSystemResult = await MountFileSystem(stream);
        return fileSystemResult.IsFaulted
            ? new Result<(int, IFileSystem)>(fileSystemResult.Error)
            : new Result<(int, IFileSystem)>((partitionNumberIntValue, fileSystemResult.Value));
    }

    protected async Task<Result<(int, IFileSystemVolume)>> MountRdbFileSystemVolume(Media media, string partition)
    {
        OnDebugMessage("Reading Rigid Disk Block");
        
        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(media);

        if (rigidDiskBlock == null)
        {
            return new Result<(int, IFileSystemVolume)>(new Error("No rigid disk block"));
        }

        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
        
        OnDebugMessage($"Partition '{partition}'");

        var partitionNumberMatch = NumberRegex().Match(partition);

        var partitionBlock = partitionNumberMatch.Success &&
            int.TryParse(partition, out var partitionNumber) &&
            partitionNumber >= 1 &&
            partitionNumber <= partitionBlocks.Count
            ? partitionBlocks[partitionNumber - 1]
            : partitionBlocks.FirstOrDefault(x =>
            x.DriveName.Equals(partition, StringComparison.OrdinalIgnoreCase));

        if (partitionBlock == null)
        {
            return new Result<(int, IFileSystemVolume)>(new Error($"Partition '{partition}' not found"));
        }

        OnDebugMessage($"Mounting file system");

        var fileSystemVolumeResult = await MountPartitionFileSystemVolume(media, partitionBlock);
        if (fileSystemVolumeResult.IsFaulted)
        {
            return new Result<(int, IFileSystemVolume)>(fileSystemVolumeResult.Error);
        }

        OnDebugMessage($"Volume '{fileSystemVolumeResult.Value.Name}'");

        var fileSystemVolume = fileSystemVolumeResult.Value;
        if (fileSystemVolume == null)
        {
            return new Result<(int, IFileSystemVolume)>(new Error("No file system volume mounted"));
        }

        return new Result<(int, IFileSystemVolume)>((partitionBlocks.IndexOf(partitionBlock), fileSystemVolume));
    }

    protected async Task<Result<IFileSystemVolume>> MountPartitionFileSystemVolume(Media media,
        PartitionBlock partitionBlock)
    {
        var stream = MediaHelper.GetStreamFromMedia(media);

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

    [GeneratedRegex("^\\d+$", RegexOptions.IgnoreCase, "en-DK")]
    private static partial Regex NumberRegex();
}