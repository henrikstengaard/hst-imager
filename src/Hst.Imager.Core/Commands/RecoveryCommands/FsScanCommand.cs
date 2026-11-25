using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Scan Amiga file system for errors without writing any changes.
/// </summary>
public class FsScanCommand(
    ILogger<FsScanCommand> logger,
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string path,
    string logPath = null)
    : FileSystemRecoveryCommandBase(logger, commandHelper, physicalDrives, path, logPath)
{
    protected override RecoveryOperation Operation => RecoveryOperation.FileSystemScan;

    internal override async Task<Result> Recover(IFileSystemDoctorAdapter adapter, RecoveryReport report,
        CancellationToken token)
    {
        OnInformationMessage($"Scanning file system in '{Path}'");

        await adapter.Check(report, false, token);

        OnInformationMessage(report.IsValid
            ? "File system is valid"
            : $"File system has {report.Errors} error(s) and {report.Warnings} warning(s)");

        return new Result();
    }
}
