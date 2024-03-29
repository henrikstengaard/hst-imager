﻿namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Core.Extensions;
using Microsoft.Extensions.Logging;

public class RdbRestoreCommand : CommandBase
{
    private readonly ILogger<RdbRestoreCommand> logger;
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string backupPath;
    private readonly string diskPath;

    public RdbRestoreCommand(ILogger<RdbRestoreCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string backupPath, string path)
    {
        this.logger = logger;
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.backupPath = backupPath;
        this.diskPath = path;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnDebugMessage($"Reading Rigid Disk Block backup from '{backupPath}'");

        if (!System.IO.File.Exists(backupPath))
        {
            return new Result(new Error($"Rigid Disk Block backup '{backupPath}' not found"));
        }
        
        await using var backupStream = System.IO.File.OpenRead(backupPath);
        var rdbBytes = await backupStream.ReadBytes((int)backupStream.Length);
        
        backupStream.Position = 0;
        var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(backupStream);

        if (rigidDiskBlock == null)
        {
            return new Result(new Error($"Rigid Disk Block not found in backup '{backupPath}'"));
        }

        OnDebugMessage($"RdbBlockLo '{rigidDiskBlock.RdbBlockLo}'");
        OnDebugMessage($"RdbBlockHi '{rigidDiskBlock.RdbBlockHi}'");
        
        var rdbSize = (int)(rigidDiskBlock.RdbBlockHi * rigidDiskBlock.BlockSize);

        if (rdbSize <= 0)
        {
            return new Result(new Error($"Failed to read rigid disk block from backup: Invalid rdb size {rdbSize}"));
        }
        
        if (rdbBytes.Length != rdbSize)
        {
            return new Result(new Error($"Failed to read rigid disk block from backup: Read {rdbBytes.Length} bytes, but expected {rdbSize}"));
        }

        OnInformationMessage($"Writing backup of Rigid Disk Block to '{diskPath}'");
            
        OnDebugMessage($"Opening '{diskPath}' as writable");

        var mediaResult = await commandHelper.GetWritableMedia(physicalDrives, diskPath);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }

        using var media = mediaResult.Value;
        var stream = media.Stream;

        OnDebugMessage($"Writing Rigid Disk Block backup to '{diskPath}'");
        
        stream.Position = 0;
        await stream.WriteBytes(rdbBytes);

        return new Result();
    }
}