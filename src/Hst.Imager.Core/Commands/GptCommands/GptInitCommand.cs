using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Partitions;
using DiscUtils.Raw;
using DiscUtils.Streams;
using Hst.Core;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using DiscUtils;

namespace Hst.Imager.Core.Commands.GptCommands;

public class GptInitCommand : CommandBase
{
    private readonly ILogger<GptInitCommand> logger;
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string path;

    public GptInitCommand(ILogger<GptInitCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path)
    {
        this.logger = logger;
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Initializing Guid Partition Table at '{path}'");

        OnDebugMessage($"Opening '{path}' as writable");

        var physicalDrivesList = physicalDrives.ToList();
        var mediaResult = await commandHelper.GetWritableMedia(physicalDrivesList, path);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }
        using var media = mediaResult.Value;
            
        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new Disk(media.Stream, Ownership.None);
            
        
        var deleteFirstSectors = false;
        var sectorBytes = new byte[disk.SectorSize];
        try
        {
            var offset = 0;

            do
            {
                disk.Content.Position = offset;
                var bytesRead = await disk.Content.ReadAsync(sectorBytes, 0, sectorBytes.Length, token);

                for (var i = 0; i < bytesRead; i++)
                {
                    if (sectorBytes[i] != 0)
                    {
                        deleteFirstSectors = true;
                        break;
                    }
                }

                offset += disk.SectorSize;
            } while (!deleteFirstSectors && offset < 16386);
        }
        catch (Exception)
        {
            // ignored, if reading first sectors fails
        }
        
        if (deleteFirstSectors)
        {
            OnDebugMessage("Deleting sectors 0-64");
                
            Array.Fill<byte>(sectorBytes, 0);
            var offset = 0;
            for (var i = 0; i < 64 && offset < disk.Capacity; i++, offset += disk.SectorSize)
            {
                disk.Content.Position = offset;
                await disk.Content.WriteAsync(sectorBytes, 0, sectorBytes.Length, token);
            }
        }

        OnDebugMessage("Initializing Guid Partition Table");
        
        // initialize gpt
        GuidPartitionTable.Initialize(disk.Content, Geometry.FromCapacity(disk.Capacity));
        
        return new Result();
    }
}