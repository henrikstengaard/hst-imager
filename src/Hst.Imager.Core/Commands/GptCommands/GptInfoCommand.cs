using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands.GptCommands;

public class GptInfoCommand : CommandBase
{
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string path;

    public GptInfoCommand(ILogger<GptInfoCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path)
    {
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
    }

    public event EventHandler<InfoReadEventArgs> GptInfoRead;
        
    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Reading Guid Partition Table information from '{path}'");
            
        OnDebugMessage($"Opening '{path}' as readable");

        var mediaResult = await commandHelper.GetReadableMedia(physicalDrives, path);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }
        using var media = mediaResult.Value;
            
        OnDebugMessage($"Reading Guid Partition Table from path '{path}'");

        var diskInfo = await commandHelper.ReadDiskInfo(media);
            
        OnGptInfoRead(new MediaInfo
        {
            Path = path,
            Name = media.Model,
            IsPhysicalDrive = media.IsPhysicalDrive,
            Type = media.Type,
            DiskSize = diskInfo.Size,
            DiskInfo = diskInfo,
            Byteswap = media.Byteswap
        });

        return new Result();
    }

    private void OnGptInfoRead(MediaInfo mediaInfo)
    {
        GptInfoRead?.Invoke(this, new InfoReadEventArgs(mediaInfo));
    }        
}