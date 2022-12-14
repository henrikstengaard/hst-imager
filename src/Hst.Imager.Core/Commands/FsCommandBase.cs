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
using EntryType = Models.FileSystems.EntryType;
using File = System.IO.File;

public abstract class FsCommandBase : CommandBase
{
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;

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

        if (string.IsNullOrEmpty(mediaResult.Value.VirtualPath))
        {
            return new Result<IEntryIterator>(new Error($"Partition table not found in path '{mediaResult.Value.VirtualPath}'"));
        }
        
        var media = commandHelper.GetReadableMedia(physicalDrives, mediaResult.Value.MediaPath);
        if (media.IsFaulted)
        {
            return new Result<IEntryIterator>(media.Error);
        }

        var parts = mediaResult.Value.VirtualPath.Split('\\', '/');

        if (parts[0].Equals("rdb", StringComparison.OrdinalIgnoreCase))
        {
            var fileSystemParts = parts.Skip(1).ToArray();
            var fileSystemVolumeResult = await MountRdbFileSystemVolume(media.Value.Stream, fileSystemParts);
            if (fileSystemVolumeResult.IsFaulted)
            {
                return new Result<IEntryIterator>(fileSystemVolumeResult.Error);
            }

            return new Result<IEntryIterator>(new MediaEntryIterator(media.Value, string.Join("/", fileSystemParts.Skip(1)), fileSystemVolumeResult.Value, recursive));
        }

        return new Result<IEntryIterator>(new Error($"Unsupported partition table '{parts[0]}'"));
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
        
        return new Result<MediaResult>(new PathNotFoundError("Media not found", path));
    }

    protected async Task<Result<IFileSystemVolume>> MountRdbFileSystemVolume(Stream stream, string[] parts)
    {
        OnDebugMessage("Reading Rigid Disk Block");

        var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);

        if (rigidDiskBlock == null)
        {
            return new Result<IFileSystemVolume>(new Error("No rigid disk block"));
        }

        if (!parts.Any() || string.IsNullOrWhiteSpace(parts[0]))
        {
            return new Result<IFileSystemVolume>(new Error("Path does not contain any partition"));
        }

        var partitionName = parts[0];

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

        // var fileSystemPath = string.Join("/", parts.Skip(1));
        //
        // if (!string.IsNullOrWhiteSpace(fileSystemPath))
        // {
        //     OnDebugMessage($"Change directory to path '{fileSystemPath}'");
        //
        //     await fileSystemVolume.ChangeDirectory(fileSystemPath);
        // }

        return new Result<IFileSystemVolume>(fileSystemVolume);

        // var entries = await fileSystemVolume.ListEntries();
        //
        // return new Result<IEnumerable<Entry>>(entries.Select(x => new Entry
        // {
        //     Name = x.Name,
        //     Type = GetEntryType(x.Type),
        //     Size = x.Size,
        //     Date = x.Date,
        //     Attributes = EntryFormatter.FormatProtectionBits(x.ProtectionBits)
        // }).ToList());
    }

    protected async Task<Result<IFileSystemVolume>> MountPartitionFileSystemVolume(Stream stream, PartitionBlock partitionBlock)
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

    protected EntryType GetEntryType(Hst.Amiga.FileSystems.EntryType type)
    {
        return type switch
        {
            Amiga.FileSystems.EntryType.Dir => EntryType.Dir,
            Amiga.FileSystems.EntryType.DirLink => EntryType.Dir,
            Amiga.FileSystems.EntryType.File => EntryType.File,
            Amiga.FileSystems.EntryType.FileLink => EntryType.File,
            Amiga.FileSystems.EntryType.SoftLink => EntryType.File,
            _ => throw new IOException($"Invalid entry type '{type}'")
        };
    }
}