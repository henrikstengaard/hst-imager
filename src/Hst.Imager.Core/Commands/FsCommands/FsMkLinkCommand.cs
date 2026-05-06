using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Core;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.MagicBytes;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands.FsCommands;

public class FsMkLinkCommand(ILogger<FsMkLinkCommand> logger, ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives, string fromPath, string toPath)
    : FsCommandBase(commandHelper, physicalDrives)
{
    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Creating link from path '{fromPath}' to path '{toPath}'");
        
        OnDebugMessage($"Opening '{fromPath}' as readable");
        
        var fromPathResult = commandHelper.ResolveMedia(fromPath, true);
        if (fromPathResult.IsFaulted)
        {
            return new Result(fromPathResult.Error);
        }

        OnDebugMessage($"Media Path: '{fromPathResult.Value.MediaPath}'");
        OnDebugMessage($"File System Path: '{fromPathResult.Value.FileSystemPath}'");

        if (await commandHelper.DetectDataType(fromPathResult.Value.MediaPath) == DataType.Adf)
        {
            return await CreateAdfMediaLink(fromPathResult.Value);
        }

        return await CreateDiskMediaLink(fromPathResult.Value);
    }
    
    private async Task<Result> CreateAdfMediaLink(MediaResult resolvedMedia)
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

        var fileSystemVolume = fileSystemVolumeResult.Value;
        var fromPathComponents = resolvedMedia.FileSystemPath.Split(resolvedMedia.DirectorySeparatorChar);

        await CreateLink(fileSystemVolume, fromPathComponents);
        
        return  new Result();
    }

    private async Task<Result> CreateDiskMediaLink(MediaResult resolvedMedia)
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

        var parts = fileSystemPath.Split(directorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 1 || !(parts[0].Equals("mbr", StringComparison.OrdinalIgnoreCase) || 
                                      parts[0].Equals("gpt", StringComparison.OrdinalIgnoreCase) || 
                                      parts[0].Equals("rdb", StringComparison.OrdinalIgnoreCase)))
        {
            return new Result(new Error($"From path '{fromPath}' does not contain partition table (mbr, gpt, rdb)"));
        }

        if (parts.Length < 2)
        {
            return new Result(new Error($"From path '{fromPath}' does not contain partition number or partition name"));
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "mbr":
                return new Result(new Error($"Creating link on MBR partition is not supported"));
            case "gpt":
                return new Result(new Error($"Creating link on GPT partition is not supported"));
            case "rdb":
                return await CreateRdbLink(media, parts.Skip(1).ToArray());
            default:
                return new Result(new Error($"Unsupported media path '{fromPath}'"));
        }
    }
    
    private async Task<Result> CreateRdbLink(Media media, string[] fromParts)
    {
        var partitionPart = fromParts[0];
        var fileSystemResult = await MountRdbFileSystemVolume(media, partitionPart);
        if (fileSystemResult.IsFaulted)
        {
            return new Result(fileSystemResult.Error);
        }
        
        await using var fileSystem = fileSystemResult.Value.Item2;
        
        // first from part contains partition number, therefore its skipped
        await CreateLink(fileSystem, fromParts.Skip(1).ToArray());

        return new Result();
    }
    
    private async Task CreateLink(IFileSystemVolume fileSystemVolume, string[] fromPartComponents)
    {
        for (var i = 0; i < fromPartComponents.Length; i++)
        {
            var pathComponent = fromPartComponents[i];

            if (i < fromPartComponents.Length - 1)
            {
                await fileSystemVolume.ChangeDirectory(pathComponent);
                continue;                
            }

            await fileSystemVolume.CreateLink(pathComponent, toPath);
        }
    }
}