namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Core;
using Hst.Core.Extensions;
using Hst.Imager.Core.Helpers;
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
        var rigidDiskBlock = await RigidDiskBlockReader.Read(backupStream);

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

        var writableMediaResult = await commandHelper.GetWritableMedia(physicalDrives, diskPath);
        if (writableMediaResult.IsFaulted)
        {
            return new Result(writableMediaResult.Error);
        }

        using var media = await MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, writableMediaResult.Value, diskPath);
        var stream = media.Stream;

        OnDebugMessage($"Writing Rigid Disk Block backup to '{diskPath}'");
        
        stream.Position = 0;
        await stream.WriteBytes(rdbBytes);

        return new Result();
    }
}