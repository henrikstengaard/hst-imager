using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Streams;
using Hst.Core;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.Models.FileSystems;
using Hst.Imager.Core.PathComponents;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands.FsCommands;

public class FsMkDirCommand(ILogger<FsMkDirCommand> logger, ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives, string path)
    : FsCommandBase(commandHelper, physicalDrives)
{
    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Creating directory path: '{path}'");
        
        OnDebugMessage($"Opening '{path}' as readable");
        
        var pathResult = commandHelper.ResolveMedia(path, true);
        if (pathResult.IsFaulted)
        {
            return new Result(pathResult.Error);
        }

        OnDebugMessage($"Media Path: '{pathResult.Value.MediaPath}'");
        OnDebugMessage($"File System Path: '{pathResult.Value.FileSystemPath}'");

        if (!pathResult.Value.Exists)
        {
            return CreateLocalDirectory(path);
        }
        
        if (await IsAdfMedia(pathResult.Value))
        {
            return await CreateAdfMediaDirectory(pathResult.Value, false);
        }

        return await CreateDiskMediaDirectory(pathResult.Value);
    }
    
    private static Result CreateLocalDirectory(string localDirectoryPath)
    {
        if (Directory.Exists(localDirectoryPath))
        {
            return new Result();
        }
        
        Directory.CreateDirectory(localDirectoryPath);
        
        return new Result();
    }
    
    private async Task<Result> CreateAdfMediaDirectory(MediaResult resolvedMedia, bool recursive)
    {
        var mediaResult = await commandHelper.GetWritableFileMedia(resolvedMedia.MediaPath);
        if (mediaResult.IsFaulted)
        {
            return new Result<IEntryIterator>(mediaResult.Error);
        }
        
        using var media = mediaResult.Value;
        
        var fileSystemVolumeResult = await MountAdfFileSystemVolume(media.Stream);
        if (fileSystemVolumeResult.IsFaulted)
        {
            return new Result<IEntryIterator>(fileSystemVolumeResult.Error);
        }

        var dirPathComponents = resolvedMedia.FileSystemPath.Split(resolvedMedia.DirectorySeparatorChar);

        await using var fileSystemVolume = fileSystemVolumeResult.Value;
        using var amigaVolumeEntryWriter = new AmigaVolumeEntryWriter(mediaResult.Value, PartitionTableType.RigidDiskBlock,
            0, string.Empty, [], recursive, fileSystemVolume,
            false, false);

        var initializeResult = await amigaVolumeEntryWriter.Initialize();
        if (initializeResult.IsFaulted)
        {
            return new Result<IEntryIterator>(initializeResult.Error);
        }

        for (var i = 0; i < dirPathComponents.Length; i++)
        {
            var currentDirPathComponents = dirPathComponents.Take(i).ToArray();
            var entryPathComponents = dirPathComponents.Take(i + 1).ToArray();
            
            var entry = new Entry
            {
                Type = EntryType.Dir,
                Attributes = string.Empty,
                Date = DateTime.Now,
                Name = dirPathComponents[i],
                RawPath = MediaPath.AmigaOsPath.Join(entryPathComponents),
                FullPathComponents = currentDirPathComponents,
                RelativePathComponents = currentDirPathComponents,
                Size = 0
            };
            
            var createDirectoryResult = await amigaVolumeEntryWriter.CreateDirectory(entry,
                entryPathComponents, true, false);
            if (createDirectoryResult.IsFaulted)
            {
                return createDirectoryResult;
            }
        }
        
        return  new Result();
    }

    private async Task<Result> CreateDiskMediaDirectory(MediaResult resolvedMedia)
    {
        var readableMediaResult = await commandHelper.GetWritableMedia(physicalDrives, resolvedMedia.MediaPath);
        if (readableMediaResult.IsFaulted)
        {
            return new Result(readableMediaResult.Error);
        }

        var fileSystemPath = resolvedMedia.FileSystemPath ?? string.Empty;
        var directorySeparatorChar = resolvedMedia.DirectorySeparatorChar;

        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(
            readableMediaResult.Value, fileSystemPath, directorySeparatorChar);

        using var media = piStormRdbMediaResult.Media;
        fileSystemPath = piStormRdbMediaResult.FileSystemPath;

        var parts = fileSystemPath.Split(new []{ directorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 1 || !(parts[0].Equals("mbr", StringComparison.OrdinalIgnoreCase) || 
                                      parts[0].Equals("gpt", StringComparison.OrdinalIgnoreCase) || 
                                      parts[0].Equals("rdb", StringComparison.OrdinalIgnoreCase)))
        {
            return new Result(new Error($"Path '{path}' does not contain partition table (mbr, gpt, rdb)"));
        }

        if (parts.Length < 2)
        {
            return new Result(new Error($"Path '{path}' does not contain partition number or partition name"));
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "mbr":
                return await CreateMbrDirectory(media, parts.Skip(1).ToArray());
            case "gpt":
                return await CreateGptDirectory(media, parts.Skip(1).ToArray());
            case "rdb":
                return await CreateRdbDirectory(media, parts.Skip(1).ToArray());
            default:
                return new Result(new Error($"Unsupported media path '{path}'"));
        }
    }

    private async Task<Result> CreateGptDirectory(Media media, string[] parts)
    {
        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
            
        var partitionPart = parts[0];
        var gptFileSystemResult = await MountGptFileSystem(disk, partitionPart);
        if (gptFileSystemResult.IsFaulted)
        {
            return new Result(gptFileSystemResult.Error);
        }

        var (_, fileSystem) = gptFileSystemResult.Value;
        var fileSystemPath = string.Join("/", parts.Skip(1));

        try
        {
            fileSystem.CreateDirectory(fileSystemPath);
        }
        catch (Exception e)
        {
            return new Result(new Error(e.Message));
        }

        return new Result();
    }

    private async Task<Result> CreateMbrDirectory(Media media, string[] parts)
    {
        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
            
        var partitionPart = parts[0];
        var mbrFileSystemResult = await MountMbrFileSystem(disk, partitionPart);
        if (mbrFileSystemResult.IsFaulted)
        {
            return new Result(mbrFileSystemResult.Error);
        }

        var (_, fileSystem) = mbrFileSystemResult.Value;
        var fileSystemPath = string.Join("/", parts.Skip(1));

        try
        {
            fileSystem.CreateDirectory(fileSystemPath);
        }
        catch (Exception e)
        {
            return new Result(new Error(e.Message));
        }

        return new Result();
    }

    private async Task<Result> CreateRdbDirectory(Media media, string[] parts)
    {
        var partitionPart = parts[0];
        var fileSystemResult = await MountRdbFileSystemVolume(media, partitionPart);
        if (fileSystemResult.IsFaulted)
        {
            return new Result(fileSystemResult.Error);
        }

        await using var fileSystem = fileSystemResult.Value.Item2;
        var dirPaths = parts.Skip(1).ToArray();

        foreach (var dirPathComponent in dirPaths)
        {
            var entries = await fileSystem.ListEntries();
            var dirEntry = entries.FirstOrDefault(x => x.Name.Equals(dirPathComponent, StringComparison.OrdinalIgnoreCase));

            switch (dirEntry)
            {
                case { Type: Amiga.FileSystems.EntryType.File }:
                    return new Result(new Error($"Directory '{dirPathComponent}' not found"));
                case null:
                    await fileSystem.CreateDirectory(dirPathComponent);
                    break;
            }

            await fileSystem.ChangeDirectory(dirPathComponent);
        }

        return new Result();
    }
}