namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amiga.Extensions;
using Extensions;
using Hst.Core;
using Hst.Core.Extensions;
using Microsoft.Extensions.Logging;

public class RdbFsExportCommand : CommandBase
{
    private readonly ILogger<RdbFsExportCommand> logger;
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string path;
    private readonly int fileSystemNumber;
    private readonly string fileSystemPath;

    public RdbFsExportCommand(ILogger<RdbFsExportCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, int fileSystemNumber, string fileSystemPath)
    {
        this.logger = logger;
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
        this.fileSystemNumber = fileSystemNumber;
        this.fileSystemPath = fileSystemPath;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Exporting file system from Rigid Disk Block at '{path}' to '{fileSystemPath}'");
        
        OnDebugMessage($"Opening '{path}' as readable");

        var mediaResult = commandHelper.GetReadableMedia(physicalDrives, path);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }

        using var media = mediaResult.Value;
        var stream = media.Stream;

        OnDebugMessage("Reading Rigid Disk Block");
            
        var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);

        if (rigidDiskBlock == null)
        {
            return new Result(new Error("Rigid Disk Block not found"));
        }

        var fileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks.ToList();
        
        if (fileSystemNumber < 1 || fileSystemNumber > fileSystemHeaderBlocks.Count)
        {
            return new Result(new Error($"Invalid file system number '{fileSystemNumber}'"));
        }

        var fileSystemHeaderBlock = fileSystemHeaderBlocks[fileSystemNumber - 1];
        var fileSystemBytes = fileSystemHeaderBlock.LoadSegBlocks.SelectMany(x => x.Data).ToArray();
        long fileSystemSize = fileSystemBytes.Length;
            
        OnInformationMessage($"- File system number '{fileSystemNumber}'");
        OnInformationMessage($"- DOS type '0x{fileSystemHeaderBlock.DosType.FormatHex()}' ({fileSystemHeaderBlock.DosType.FormatDosType()})");
        OnInformationMessage($"- Version '{fileSystemHeaderBlock.VersionFormatted}'");
        OnInformationMessage($"- Size '{fileSystemSize.FormatBytes()}' ({fileSystemSize} bytes)");
        OnInformationMessage($"- File system name '{fileSystemHeaderBlock.FileSystemName}'");

        OnDebugMessage($"Writing file system to '{fileSystemPath}'");
        await File.WriteAllBytesAsync(fileSystemPath, fileSystemBytes, token);
            
        return new Result();
    }
}