namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amiga.Extensions;
using Amiga.RigidDiskBlocks;
using Extensions;
using Hst.Core;
using Hst.Core.Extensions;
using Hst.Imager.Core.Helpers;
using Microsoft.Extensions.Logging;

public class RdbFsUpdateCommand : CommandBase
{
    private readonly ILogger<RdbFsUpdateCommand> logger;
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string path;
    private readonly int fileSystemNumber;
    private readonly string dosType;
    private readonly string fileSystemName;
    private readonly string fileSystemPath;
    private readonly int? version;
    private readonly int? revision;

    public RdbFsUpdateCommand(ILogger<RdbFsUpdateCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, int fileSystemNumber, string dosType, string fileSystemName, string fileSystemPath,
        int? version = null, int? revision = null)
    {
        this.logger = logger;
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
        this.fileSystemNumber = fileSystemNumber;
        this.dosType = dosType;
        this.fileSystemName = fileSystemName;
        this.fileSystemPath = fileSystemPath;
        this.version = version;
        this.revision = revision;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Updating file system in Rigid Disk Block at '{path}'");
        
        OnDebugMessage($"Opening '{path}' as writable");

        var writableMediaResult = await commandHelper.GetWritableMedia(physicalDrives, path);
        if (writableMediaResult.IsFaulted)
        {
            return new Result(writableMediaResult.Error);
        }

        using var media = MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, writableMediaResult.Value, path);
        var stream = media.Stream;

        OnDebugMessage("Reading Rigid Disk Block");
            
        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(media);

        if (rigidDiskBlock == null)
        {
            return new Result(new Error("Rigid Disk Block not found"));
        }

        var fileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks.ToList();

        OnDebugMessage($"Updating file system number '{fileSystemNumber}'");
            
        if (fileSystemNumber < 1 || fileSystemNumber > fileSystemHeaderBlocks.Count)
        {
            return new Result(new Error($"Invalid file system number '{fileSystemNumber}'"));
        }

        var fileSystemHeaderBlock = fileSystemHeaderBlocks[fileSystemNumber - 1];
        
        if (!string.IsNullOrEmpty(dosType))
        {
            OnDebugMessage($"Updating DOS type '{dosType}'");
            
            var dosTypeBytes = DosTypeHelper.FormatDosType(dosType);
            var partitionBlocks = rigidDiskBlock.PartitionBlocks.Where(x => x.DosType.SequenceEqual(fileSystemHeaderBlock.DosType)).ToList();

            foreach (var partitionBlock in partitionBlocks)
            {
                partitionBlock.DosType = dosTypeBytes; 
            }
            
            fileSystemHeaderBlock.DosType = dosTypeBytes;        
            
            OnInformationMessage($"DOS type '0x{fileSystemHeaderBlock.DosType.FormatHex()}' ({fileSystemHeaderBlock.DosType.FormatDosType()})");
        }

        if (!string.IsNullOrEmpty(fileSystemName))
        {
            OnDebugMessage($"Updating file system name '{fileSystemName}'");
            
            fileSystemHeaderBlock.FileSystemName = fileSystemName;
            
            OnInformationMessage($"File system name '{fileSystemHeaderBlock.FileSystemName}'");
        }

        if (!string.IsNullOrEmpty(fileSystemPath))
        {
            OnDebugMessage($"Updating file system from path '{fileSystemPath}'");

            if (!File.Exists(fileSystemPath))
            {
                return new Result(new Error($"File system path '{fileSystemPath}' not found"));
            }

            var fileSystemBytes = await File.ReadAllBytesAsync(fileSystemPath, cancellationToken: token);

            var maxSize = 512 - (5 * 4);
            var loadSegBlocks = fileSystemBytes.ChunkBy(maxSize)
                .Select(x => BlockHelper.CreateLoadSegBlock(x.ToArray())).ToList();            

            fileSystemHeaderBlock.LoadSegBlocks = loadSegBlocks;
            fileSystemHeaderBlock.FileSystemSize = fileSystemBytes.Length;
            fileSystemHeaderBlock.NextFileSysHeaderBlock = 0;
            
            OnInformationMessage($"Size '{((long)fileSystemHeaderBlock.FileSystemSize).FormatBytes()}' ({fileSystemHeaderBlock.FileSystemSize} bytes)");
        }

        if (version.HasValue || revision.HasValue)
        {
            OnDebugMessage($"Updating file system version '{version?.ToString() ?? "-"}.{revision?.ToString() ?? "-"}'");

            if (version.HasValue)
            {
                fileSystemHeaderBlock.Version = version.Value;
            }

            if (revision.HasValue)
            {
                fileSystemHeaderBlock.Revision = revision.Value;
            }

            OnInformationMessage($"Version '{fileSystemHeaderBlock.VersionFormatted}'");
        }

        OnDebugMessage("Writing Rigid Disk Block");
        await MediaHelper.WriteRigidDiskBlockToMedia(media, rigidDiskBlock);

        return new Result();
    }
}