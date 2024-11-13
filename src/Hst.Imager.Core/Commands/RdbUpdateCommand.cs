namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.Helpers;
using Microsoft.Extensions.Logging;

public class RdbUpdateCommand : CommandBase
{
    private readonly ILogger<RdbUpdateCommand> logger;
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string path;
    private readonly uint? flags;
    private readonly uint? hostId;
    private readonly string diskProduct;
    private readonly string diskRevision;
    private readonly string diskVendor;

    public RdbUpdateCommand(ILogger<RdbUpdateCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, uint? flags, uint? hostId, string diskProduct,
        string diskRevision, string diskVendor)
    {
        this.logger = logger;
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
        this.flags = flags;
        this.hostId = hostId;
        this.diskProduct = diskProduct;
        this.diskRevision = diskRevision;
        this.diskVendor = diskVendor;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Updating Rigid Disk Block at '{path}'");
        
        OnDebugMessage($"Opening '{path}' as writable");

        var writableMediaResult = await commandHelper.GetWritableMedia(physicalDrives, path);
        if (writableMediaResult.IsFaulted)
        {
            return new Result(writableMediaResult.Error);
        }

        using var media = await MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, writableMediaResult.Value, path);
        var stream = media.Stream;

        OnDebugMessage("Reading Rigid Disk Block");
            
        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(media);

        if (rigidDiskBlock == null)
        {
            return new Result(new Error("Rigid Disk Block not found"));
        }

        if (flags.HasValue)
        {
            OnDebugMessage($"Updating flags '{flags.Value}'");

            rigidDiskBlock.Flags = flags.Value;
            
            OnInformationMessage($"Flags '{flags.Value}'");
        }
            
        if (hostId.HasValue)
        {
            OnDebugMessage($"Updating host id '{hostId.Value}'");

            rigidDiskBlock.HostId = hostId.Value;
            
            OnInformationMessage($"Host id '{hostId.Value}'");
        }

        if (!string.IsNullOrWhiteSpace(diskProduct))
        {
            OnDebugMessage($"Updating disk product '{diskProduct}'");

            rigidDiskBlock.DiskProduct = diskProduct;
            
            OnInformationMessage($"Disk product '{diskProduct}'");
        }
            
        if (!string.IsNullOrWhiteSpace(diskRevision))
        {
            OnDebugMessage($"Updating disk revision '{diskRevision}'");

            rigidDiskBlock.DiskRevision = diskRevision;
            
            OnInformationMessage($"Disk revision '{diskRevision}'");
        }

        if (!string.IsNullOrWhiteSpace(diskVendor))
        {
            OnDebugMessage($"Updating disk vendor '{diskVendor}'");

            rigidDiskBlock.DiskVendor = diskVendor;
            
            OnInformationMessage($"Disk vendor '{diskVendor}'");
        }

        OnDebugMessage("Writing Rigid Disk Block");
        await MediaHelper.WriteRigidDiskBlockToMedia(media, rigidDiskBlock);

        return new Result();
    }
}