using DiscUtils.Ntfs;

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
using Hst.Imager.Core.PathComponents;

public abstract class FsCommandBase : CommandBase
{
    protected readonly ICommandHelper commandHelper;
    protected readonly IEnumerable<IPhysicalDrive> physicalDrives;

    protected FsCommandBase(ICommandHelper commandHelper, IEnumerable<IPhysicalDrive> physicalDrives)
    {
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
    }
    
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
        path = PathHelper.GetFullPath(path);
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
        path = PathHelper.GetFullPath(path);
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

        var rootPathComponents = mediaResult.FileSystemPath.Split(mediaResult.DirectorySeparatorChar);
        var rootPath = MediaPath.ZipArchivePath.Join(rootPathComponents);

        return new Result<IEntryIterator>(new ZipArchiveEntryIterator(zipStream, rootPath, zipArchive,
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

        var rootPathComponents = mediaResult.FileSystemPath.Split(mediaResult.DirectorySeparatorChar);
        var rootPath = MediaPath.LhaArchivePath.Join(rootPathComponents);

        return new Result<IEntryIterator>(new LhaArchiveEntryIterator(lhaStream, rootPath, lhaArchive,
            recursive));
    }

    protected async Task<Result<IEntryIterator>> GetLzxEntryIterator(MediaResult mediaResult, bool recursive)
    {
        if (!await IsLzxMedia(mediaResult))
        {
            return null;
        }

        var lzxStream = File.OpenRead(mediaResult.MediaPath);
        var lzxArchive = new LzxArchive(lzxStream, LzxOptions.AmigaLzxOptions);

        var rootPathComponents = mediaResult.FileSystemPath.Split(mediaResult.DirectorySeparatorChar);
        var rootPath = MediaPath.LzxArchivePath.Join(rootPathComponents);

        return new Result<IEntryIterator>(new LzxArchiveEntryIterator(lzxStream, rootPath, lzxArchive,
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

        var rootPathComponents = mediaResult.FileSystemPath.Split(mediaResult.DirectorySeparatorChar);
        var rootPath = MediaPath.AmigaOsPath.Join(rootPathComponents);

        return new Result<IEntryIterator>(new AmigaVolumeEntryIterator(adfStream, rootPath,
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

        var rootPathComponents = mediaResult.FileSystemPath.Split(mediaResult.DirectorySeparatorChar);
        var rootPath = MediaPath.Iso9660Path.Join(rootPathComponents);

        return new Result<IEntryIterator>(new Iso9660EntryIterator(iso96690Stream, rootPath, cdReader,
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

        var piStormRdbMediaResult = await MediaHelper.GetPiStormRdbMedia(
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

        var rootPath = string.Join("/", parts.Skip(2));
        return new Result<IEntryIterator>(new AmigaVolumeEntryIterator(media.Stream, rootPath,
            fileSystemVolumeResult.Value, recursive));
    }

    private async Task<Result<IEntryIterator>> GetFloppyFileSystemEntryIterator(Media media, string[] parts, bool recursive)
    {
        var fileSystemResult = await MountFileSystem(media.Stream);
        if (fileSystemResult.IsFaulted)
        {
            return new Result<IEntryIterator>(fileSystemResult.Error);
        }

        var rootPath = string.Join("\\", parts);
        return new Result<IEntryIterator>(new FileSystemEntryIterator(media, fileSystemResult.Value, rootPath, recursive));
    }

    private async Task<Result<IEntryIterator>> GetMbrFileSystemEntryIterator(Media media, string[] parts, bool recursive)
    {
        if (parts.Length < 2)
        {
            return new Result<IEntryIterator>(new Error($"No partition number in path"));
        }

        if (!int.TryParse(parts[1], out var partitionNumber))
        {
            return new Result<IEntryIterator>(new Error($"Invalid partition number '{parts[1]}'"));
        }
        
        // open stream as disk
        var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);

        var mbrFileSystemResult = await MountMbrFileSystem(disk, parts[1]);
        if (mbrFileSystemResult.IsFaulted)
        {
            return new Result<IEntryIterator>(mbrFileSystemResult.Error);
        }

        var rootPath = string.Join("\\", parts.Skip(2));
        return new Result<IEntryIterator>(new FileSystemEntryIterator(media, mbrFileSystemResult.Value, rootPath, recursive));
    }

    private async Task<Result<IEntryIterator>> GetGptFileSystemEntryIterator(Media media, string[] parts, bool recursive)
    {
        if (parts.Length < 2)
        {
            return new Result<IEntryIterator>(new Error($"No partition number in path"));
        }

        if (!int.TryParse(parts[1], out var partitionNumber))
        {
            return new Result<IEntryIterator>(new Error($"Invalid partition number '{parts[1]}'"));
        }
        
        // open stream as disk
        var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);

        var gptFileSystemResult = await MountGptFileSystem(disk, parts[1]);
        if (gptFileSystemResult.IsFaulted)
        {
            return new Result<IEntryIterator>(gptFileSystemResult.Error);
        }

        var rootPath = string.Join("\\", parts.Skip(2));
        return new Result<IEntryIterator>(new FileSystemEntryIterator(media, gptFileSystemResult.Value, rootPath, recursive));
    }
    
    protected async Task<Result<IEntryWriter>> GetEntryWriter(string destPath, bool useCache)
    {
        // resolve media path
        var mediaResult = commandHelper.ResolveMedia(destPath);

        // return directory writer, if media path doesn't exist. otherwise return error
        if (mediaResult.IsFaulted)
        {
            return mediaResult.Error is PathNotFoundError
                ? new Result<IEntryWriter>(new DirectoryEntryWriter(destPath))
                : new Result<IEntryWriter>(mediaResult.Error);
        }

        OnDebugMessage($"Media Path: '{mediaResult.Value.MediaPath}'");
        OnDebugMessage($"Virtual Path: '{mediaResult.Value.FileSystemPath}'");

        var writableMediaResult = await commandHelper.GetWritableMedia(physicalDrives, mediaResult.Value.MediaPath, mediaResult.Value.Modifiers);
        if (writableMediaResult.IsFaulted)
        {
            return new Result<IEntryWriter>(writableMediaResult.Error);
        }

        var fileSystemPath = mediaResult.Value.FileSystemPath ?? string.Empty;
        var directorySeparatorChar = mediaResult.Value.DirectorySeparatorChar;

        var piStormRdbMediaResult = await MediaHelper.GetPiStormRdbMedia(
            writableMediaResult.Value, fileSystemPath, directorySeparatorChar);

        var media = piStormRdbMediaResult.Media;
        fileSystemPath = piStormRdbMediaResult.FileSystemPath;

        // adf
        if (File.Exists(mediaResult.Value.MediaPath) &&
            (Path.GetExtension(mediaResult.Value.MediaPath) ?? string.Empty).Equals(".adf",
                StringComparison.OrdinalIgnoreCase))
        {
            var stream = media.Stream;
            var fileSystemVolumeResult = await MountAdfFileSystemVolume(stream);
            if (fileSystemVolumeResult.IsFaulted)
            {
                return new Result<IEntryWriter>(fileSystemVolumeResult.Error);
            }

            return new Result<IEntryWriter>(new AmigaVolumeEntryWriter(media, string.Empty,
                fileSystemPath.Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries), 
                fileSystemVolumeResult.Value));
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
                
                // skip 2 first parts, partition table and partition number
                return new Result<IEntryWriter>(new FileSystemEntryWriter(media, mbrFileSystemResult.Value, parts.Skip(2).ToArray()));
            case "gpt":
                var gptFileSystemResult = await MountGptFileSystem(disk, parts[1]);
                if (gptFileSystemResult.IsFaulted)
                {
                    return new Result<IEntryWriter>(gptFileSystemResult.Error);
                }
                
                // skip 2 first parts, partition table and partition number
                return new Result<IEntryWriter>(new FileSystemEntryWriter(media, gptFileSystemResult.Value, parts.Skip(2).ToArray()));
            case "rdb":
                if (parts.Length == 1)
                {
                    return new Result<IEntryWriter>(new Error($"No device name in path"));
                }

                var fileSystemVolumeResult = await MountRdbFileSystemVolume(media, parts[1]);
                if (fileSystemVolumeResult.IsFaulted)
                {
                    return new Result<IEntryWriter>(fileSystemVolumeResult.Error);
                }

                // skip 2 first parts, partition table and device/drive name
                return new Result<IEntryWriter>(new AmigaVolumeEntryWriter(media, fileSystemPath, parts.Skip(2).ToArray(),
                    fileSystemVolumeResult.Value));
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

    protected async Task<Result<IFileSystem>> MountMbrFileSystem(VirtualDisk disk, string partitionNumber)
    {
        if (!int.TryParse(partitionNumber, out var partitionNumberIntValue))
        {
            return new Result<IFileSystem>(new Error($"Invalid partition number '{partitionNumber}'"));
        }

        OnDebugMessage("Reading Master Boot Record");

        BiosPartitionTable biosPartitionTable;
        try
        {
            biosPartitionTable = new BiosPartitionTable(disk);
        }
        catch (Exception)
        {
            return new Result<IFileSystem>(new Error("Master Boot Record not found"));
        }

        OnDebugMessage($"Partition number '{partitionNumber}'");

        if (partitionNumberIntValue < 0 || partitionNumberIntValue - 1 > biosPartitionTable.Partitions.Count)
        {
            return new Result<IFileSystem>(new Error(
                $"Invalid partition number {partitionNumber}, expected to between 1-{biosPartitionTable.Partitions.Count}"));
        }

        var partitionInfo = biosPartitionTable.Partitions[partitionNumberIntValue - 1];

        OnDebugMessage($"Mounting file system");

        var stream = partitionInfo.Open();

        return await MountFileSystem(stream);
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
        
        // for (var blockOffset = 0; blockOffset < blockBytes.Length; blockOffset += 512)
        // {
        //     // fat magic Signature
        //     if (blockBytes[blockOffset + 0x1fe] == 0x55 && blockBytes[blockOffset + 0x1ff] == 0xaa)
        //     {
        //     }
        // }
        
        if (FatFileSystem.Detect(stream))
        {
            return new Result<IFileSystem>(new FatFileSystem(stream));
        }

        if (NtfsFileSystem.Detect(stream))
        {
            return new Result<IFileSystem>(new NtfsFileSystem(stream));
        }
        
        return new Result<IFileSystem>(new Error("Unsupported Master Boot Record file system"));
    }

    protected async Task<Result<IFileSystem>> MountGptFileSystem(VirtualDisk disk, string partitionNumber)
    {
        if (!int.TryParse(partitionNumber, out var partitionNumberIntValue))
        {
            return new Result<IFileSystem>(new Error($"Invalid partition number '{partitionNumber}'"));
        }
        
        OnDebugMessage("Reading Guid Partition Table");
        
        GuidPartitionTable guidPartitionTable;
        try
        {
            guidPartitionTable = new GuidPartitionTable(disk);
        }
        catch (Exception)
        {
            return new Result<IFileSystem>(new Error("Guid Partition Table not found"));
        }

        OnDebugMessage($"Partition number '{partitionNumber}'");

        if (partitionNumberIntValue < 0 || partitionNumberIntValue - 1 > guidPartitionTable.Partitions.Count)
        {
            return new Result<IFileSystem>(new Error($"Invalid partition number {partitionNumber}, expected to between 1-{guidPartitionTable.Partitions.Count}"));
        }

        var partitionInfo = guidPartitionTable.Partitions[partitionNumberIntValue - 1];
        
        OnDebugMessage($"Mounting file system");

        var stream = partitionInfo.Open();
        var partitionOffset = stream.Position;

        // read first 64kb block bytes of partition
        var blockBytes = await stream.ReadBytes(512 * 128);

        // ext super block magic signature
        if (blockBytes[1024 + 0x38] == 0x53 && blockBytes[1024 + 0x39] == 0xEF)
        {
            return new Result<IFileSystem>(new ExtFileSystem(stream));
        }
        
        // for (var blockOffset = 0; blockOffset < blockBytes.Length; blockOffset += 512)
        // {
        //     // fat magic Signature
        //     if (blockBytes[blockOffset + 0x1fe] == 0x55 && blockBytes[blockOffset + 0x1ff] == 0xaa)
        //     {
        //     }
        // }
        
        if (FatFileSystem.Detect(stream))
        {
            return new Result<IFileSystem>(new FatFileSystem(stream));
        }

        if (NtfsFileSystem.Detect(stream))
        {
            return new Result<IFileSystem>(new NtfsFileSystem(stream));
        }
        
        return new Result<IFileSystem>(new Error("Unsupported Guid Partition Table file system"));
    }

    protected async Task<Result<IFileSystemVolume>> MountRdbFileSystemVolume(Media media, string partitionName)
    {
        OnDebugMessage("Reading Rigid Disk Block");
        
        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(media);

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

        var fileSystemVolumeResult = await MountPartitionFileSystemVolume(media, partitionBlock);
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
}