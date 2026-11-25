using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Scan partition tables for errors without writing any changes.
/// </summary>
public class PartitionTableScanCommand(
    ILogger<PartitionTableScanCommand> logger,
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string path,
    string logPath = null,
    PartitionTableType? partitionTableType = null)
    : PartitionTableRecoveryCommandBase(logger, commandHelper, physicalDrives, path, logPath, partitionTableType)
{
    protected override RecoveryOperation Operation => RecoveryOperation.PartitionTableScan;

    protected override bool Repair => false;
}
