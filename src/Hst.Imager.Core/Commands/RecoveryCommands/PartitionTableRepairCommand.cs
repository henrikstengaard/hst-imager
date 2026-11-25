using System.Collections.Generic;
using Hst.Amiga.RigidDiskBlocks.Recovery;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Repair partition tables. Rigid disk block is repaired or rebuilt from file systems found on disk and guid
/// partition table is restored from valid primary or backup copy. Repair writes to disk and should only be done
/// on a backup.
/// </summary>
public class PartitionTableRepairCommand(
    ILogger<PartitionTableRepairCommand> logger,
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string path,
    string logPath = null,
    PartitionTableType? partitionTableType = null,
    RecoveryMode rdbMode = RecoveryMode.Auto)
    : PartitionTableRecoveryCommandBase(logger, commandHelper, physicalDrives, path, logPath, partitionTableType)
{
    protected override RecoveryOperation Operation => RecoveryOperation.PartitionTableRepair;

    protected override bool Repair => true;

    protected override RecoveryMode RdbMode => rdbMode;
}
