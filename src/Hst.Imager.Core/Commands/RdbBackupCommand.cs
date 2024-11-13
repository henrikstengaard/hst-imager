namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Core.Extensions;
using Hst.Imager.Core.Helpers;
using Microsoft.Extensions.Logging;
using Directory = System.IO.Directory;

public class RdbBackupCommand : CommandBase
{
    private readonly ILogger<RdbBackupCommand> logger;
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string diskPath;
    private readonly string backupPath;

    public RdbBackupCommand(ILogger<RdbBackupCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, string backupPath)
    {
        this.logger = logger;
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.diskPath = path;
        this.backupPath = backupPath;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Reading backup of Rigid Disk Block from '{diskPath}'");
            
        OnDebugMessage($"Opening '{diskPath}' as readable");

        var readableMediaResult = await commandHelper.GetReadableMedia(physicalDrives, diskPath);
        if (readableMediaResult.IsFaulted)
        {
            return new Result(readableMediaResult.Error);
        }

        using var media = await MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, readableMediaResult.Value, diskPath);
        var stream = media.Stream;

        OnDebugMessage("Reading Rigid Disk Block");

        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(media);

        if (rigidDiskBlock == null)
        {
            return new Result(new Error("Rigid Disk Block not found"));
        }

        OnDebugMessage($"RdbBlockLo '{rigidDiskBlock.RdbBlockLo}'");
        OnDebugMessage($"RdbBlockHi '{rigidDiskBlock.RdbBlockHi}'");
        
        var rdbSize = (int)(rigidDiskBlock.RdbBlockHi * rigidDiskBlock.BlockSize);

        if (rdbSize <= 0)
        {
            return new Result(new Error($"Failed to read rigid disk block backup from disk: Invalid rdb size {rdbSize}"));
        }
        
        stream.Position = 0;
        var rdbBytes = await stream.ReadBytes(rdbSize);

        if (rdbBytes.Length != rdbSize)
        {
            return new Result(new Error($"Failed to read rigid disk block backup from disk: Read {rdbBytes.Length} bytes, but expected {rdbSize}"));
        }
        
        var dirPath = Path.GetDirectoryName(backupPath);
        if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }

        OnDebugMessage($"Writing Rigid Disk Block backup to '{backupPath}'");

        await using var backupStream = System.IO.File.OpenWrite(backupPath);
        await backupStream.WriteBytes(rdbBytes);

        return new Result();
    }
}