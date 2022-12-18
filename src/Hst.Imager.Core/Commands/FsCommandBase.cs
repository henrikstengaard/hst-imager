namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Amiga.FileSystems.FastFileSystem;
using Amiga.FileSystems.Pfs3;
using Amiga.RigidDiskBlocks;
using Hst.Core;
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

    protected async Task<Result<IEntryIterator>> GetEntryIterator(string path, bool recursive)
    {
        if (Directory.Exists(path))
        {
            return new Result<IEntryIterator>(new DirectoryEntryIterator(path, recursive));
        }

        var mediaResult = ResolveMedia(path);
        if (mediaResult.IsFaulted)
        {
            return new Result<IEntryIterator>(mediaResult.Error);
        }

        OnDebugMessage($"Media Path: '{mediaResult.Value.MediaPath}'");
        OnDebugMessage($"Virtual Path: '{mediaResult.Value.VirtualPath}'");

        if (string.IsNullOrWhiteSpace(mediaResult.Value.MediaPath))
        {
            return new Result<IEntryIterator>(
                new PathNotFoundError($"Media path not defined",
                    mediaResult.Value.MediaPath));
        }

        // adf
        if (File.Exists(mediaResult.Value.MediaPath) && (Path.GetExtension(mediaResult.Value.MediaPath) ?? string.Empty).Equals(".adf",
                StringComparison.OrdinalIgnoreCase))
        {
            var adfStream = File.OpenRead(mediaResult.Value.MediaPath);
            var fileSystemVolumeResult = await MountAdfFileSystemVolume(adfStream);
            if (fileSystemVolumeResult.IsFaulted)
            {
                return new Result<IEntryIterator>(fileSystemVolumeResult.Error);
            }

            return new Result<IEntryIterator>(new AmigaVolumeEntryIterator(adfStream,
                mediaResult.Value.VirtualPath, fileSystemVolumeResult.Value, recursive));
        }

        // disk
        if (string.IsNullOrEmpty(mediaResult.Value.VirtualPath))
        {
            return new Result<IEntryIterator>(
                new Error($"Partition table not found in path '{mediaResult.Value.VirtualPath}'"));
        }

        var media = commandHelper.GetReadableMedia(physicalDrives, mediaResult.Value.MediaPath);
        if (media.IsFaulted)
        {
            return new Result<IEntryIterator>(media.Error);
        }

        var parts = mediaResult.Value.VirtualPath.Split('\\', '/');

        if (parts[0].Equals("rdb", StringComparison.OrdinalIgnoreCase))
        {
            var fileSystemVolumeResult = await MountRdbFileSystemVolume(media.Value.Stream, parts[1]);
            if (fileSystemVolumeResult.IsFaulted)
            {
                return new Result<IEntryIterator>(fileSystemVolumeResult.Error);
            }

            var rootPath = string.Join("/", parts.Skip(2));
            return new Result<IEntryIterator>(new AmigaVolumeEntryIterator(media.Value.Stream, rootPath,
                fileSystemVolumeResult.Value, recursive));
        }

        return new Result<IEntryIterator>(new Error($"Unsupported partition table '{parts[0]}'"));
    }

    protected async Task<Result<IEntryWriter>> GetEntryWriter(string path)
    {
        // directory must exist, should be able to handle only part of the directory exists
        if (Directory.Exists(path))
        {
            return new Result<IEntryWriter>(new DirectoryEntryWriter(path));
        }

        var mediaResult = ResolveMedia(path);
        if (mediaResult.IsFaulted)
        {
            return new Result<IEntryWriter>(mediaResult.Error);
        }

        OnDebugMessage($"Media Path: '{mediaResult.Value.MediaPath}'");
        OnDebugMessage($"Virtual Path: '{mediaResult.Value.VirtualPath}'");

        // adf
        if (File.Exists(mediaResult.Value.MediaPath) && (Path.GetExtension(mediaResult.Value.MediaPath) ?? string.Empty).Equals(".adf",
                StringComparison.OrdinalIgnoreCase))
        {
            var adfStream = File.Open(mediaResult.Value.MediaPath, FileMode.Open, FileAccess.ReadWrite);
            var fileSystemVolumeResult = await MountAdfFileSystemVolume(adfStream);
            if (fileSystemVolumeResult.IsFaulted)
            {
                return new Result<IEntryWriter>(fileSystemVolumeResult.Error);
            }

            return new Result<IEntryWriter>(new AmigaVolumeEntryWriter(adfStream,
                mediaResult.Value.VirtualPath, fileSystemVolumeResult.Value));
        }
        
        // disk
        var media = commandHelper.GetWritableMedia(physicalDrives, mediaResult.Value.MediaPath);
        if (media.IsFaulted)
        {
            return new Result<IEntryWriter>(media.Error);
        }

        var parts = mediaResult.Value.VirtualPath.Split('\\', '/');

        if (parts[0].Equals("rdb", StringComparison.OrdinalIgnoreCase))
        {
            var fileSystemVolumeResult = await MountRdbFileSystemVolume(media.Value.Stream, parts[1]);
            if (fileSystemVolumeResult.IsFaulted)
            {
                return new Result<IEntryWriter>(fileSystemVolumeResult.Error);
            }

            return new Result<IEntryWriter>(new AmigaVolumeEntryWriter(media.Value.Stream,
                string.Join("/", parts.Skip(2)), fileSystemVolumeResult.Value));
        }

        return new Result<IEntryWriter>(new Error($"Unsupported partition table '{parts[0]}'"));
    }

    protected Result<MediaResult> ResolveMedia(string path)
    {
        string mediaPath;

        // physical drive
        var physicalDrivePathMatch = Regexs.PhysicalDrivePathRegex.Match(path);
        if (physicalDrivePathMatch.Success)
        {
            mediaPath = physicalDrivePathMatch.Value;
            var firstSeparatorIndex = path.IndexOf("\\", mediaPath.Length, StringComparison.Ordinal);

            return new Result<MediaResult>(new MediaResult
            {
                MediaPath = mediaPath,
                VirtualPath = firstSeparatorIndex >= 0
                    ? path.Substring(firstSeparatorIndex + 1, path.Length - (firstSeparatorIndex + 1))
                    : string.Empty
            });
        }

        // media file
        var next = 0;
        do
        {
            next = path.IndexOf(Path.DirectorySeparatorChar.ToString(), next + 1, StringComparison.OrdinalIgnoreCase);
            mediaPath = path.Substring(0, next == -1 ? path.Length : next);

            if (File.Exists(mediaPath))
            {
                return new Result<MediaResult>(new MediaResult
                {
                    MediaPath = mediaPath,
                    VirtualPath = mediaPath.Length + 1 < path.Length
                        ? path.Substring(mediaPath.Length + 1, path.Length - (mediaPath.Length + 1))
                        : string.Empty
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
        switch (partitionBlock.DosTypeFormatted)
        {
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