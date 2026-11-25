using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Repair Amiga file system. Repair writes to disk and can potentially corrupt the file system further, so it
/// should only be done on a backup.
/// </summary>
public class FsRepairCommand(
    ILogger<FsRepairCommand> logger,
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string path,
    string logPath = null)
    : FileSystemRecoveryCommandBase(logger, commandHelper, physicalDrives, path, logPath)
{
    protected override RecoveryOperation Operation => RecoveryOperation.FileSystemRepair;

    protected override bool Writable => true;

    internal override async Task<Result> Recover(IFileSystemDoctorAdapter adapter, RecoveryReport report,
        CancellationToken token)
    {
        OnInformationMessage($"Repairing file system in '{Path}'");

        await adapter.Check(report, true, token);

        OnInformationMessage(report.IsValid
            ? report.Fixed > 0
                ? $"File system repaired, {report.Fixed} issue(s) fixed"
                : "File system is valid, no repair needed"
            : $"File system has {report.Errors - report.Fixed} error(s) that couldn't be fixed");

        return new Result();
    }
}
