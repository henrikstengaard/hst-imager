using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Amiga.RigidDiskBlocks.Recovery;
using Hst.Core;
using Hst.Imager.Core.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Full repair of disk repairing partition tables, then reading partitions again and repairing Amiga file systems
/// in rigid disk block partitions. Partition table and file system repairs are written to the same log file and
/// combined in one report. Repair writes to disk and should only be done on a backup.
/// </summary>
public class FullRepairCommand(
    ILogger<FullRepairCommand> logger,
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string path,
    string logPath = null,
    RecoveryMode rdbMode = RecoveryMode.Auto)
    : CommandBase, IRecoveryCommand
{
    public event EventHandler<RecoveryReportReadEventArgs> RecoveryReportRead;
    public event EventHandler<string> LogLineWritten;

    public override async Task<Result> Execute(CancellationToken token)
    {
        var mediaResult = commandHelper.ResolveMedia(path);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }

        var resolvedMedia = mediaResult.Value;
        var separator = resolvedMedia.DirectorySeparatorChar;
        var pathComponents = (resolvedMedia.FileSystemPath ?? string.Empty)
            .Split(separator, StringSplitOptions.RemoveEmptyEntries);

        if (!IsValidPath(pathComponents))
        {
            return new Result(new Error(
                $"Invalid path '{path}' for full repair, expected disk or disk with partition table, e.g. 'disk.img' or 'disk.img{separator}rdb'"));
        }

        var drives = physicalDrives.ToList();
        var isPhysicalDrive = drives.Any(x =>
            x.Path.Equals(resolvedMedia.MediaPath, StringComparison.OrdinalIgnoreCase));
        var logFile = string.IsNullOrWhiteSpace(logPath)
            ? RecoveryLogPath.GetDefault(resolvedMedia.MediaPath, isPhysicalDrive, RecoveryOperation.FullRepair)
            : logPath;

        // create log file with header for full repair. partition table and file system repairs are appended
        string fullLogPath;
        try
        {
            using var log = CreateLog(logFile, false);
            log.WriteHeader(RecoveryOperation.FullRepair, path, ("Rigid disk block mode", rdbMode));
            fullLogPath = log.Path;
        }
        catch (Exception e)
        {
            return new Result(new Error($"Failed to create log file '{logFile}': {e.Message}"));
        }

        var report = new RecoveryReport
        {
            Operation = RecoveryOperation.FullRepair,
            Target = path,
            LogPath = fullLogPath,
            FileSystemsChecked = 0
        };

        OnInformationMessage($"Writing log to '{fullLogPath}'");

        await RepairPartitionTables(drives, fullLogPath, report, token);

        if (!token.IsCancellationRequested)
        {
            await RepairFileSystems(drives, fullLogPath, separator, pathComponents, report, token);
        }

        report.Aborted |= token.IsCancellationRequested;

        try
        {
            using var log = CreateLog(fullLogPath, true);
            log.WriteRaw(string.Empty);
            log.WriteRaw("Full repair");
            log.WriteRaw(new string('=', 11));
            log.WriteReport(report);
        }
        catch (Exception e)
        {
            logger?.LogError(e, "Failed to write full repair summary to log file");
        }

        RecoveryReportRead?.Invoke(this, new RecoveryReportReadEventArgs(report));

        return report.Aborted
            ? new Result(new Error("Full repair was cancelled"))
            : new Result();
    }

    private RecoveryLogWriter CreateLog(string file, bool append)
    {
        var log = new RecoveryLogWriter(file, append);
        log.LineWritten += (_, line) => LogLineWritten?.Invoke(this, line);
        return log;
    }

    /// <summary>
    /// Path is disk, partition table or PiStorm rigid disk block, e.g. "disk.img", "disk.img\rdb" or
    /// "disk.img\mbr\2\rdb".
    /// </summary>
    private static bool IsValidPath(string[] pathComponents) => pathComponents.Length switch
    {
        0 => true,
        1 => pathComponents[0].ToLowerInvariant() is "rdb" or "mbr" or "gpt",
        3 => pathComponents[0].ToLowerInvariant() is "mbr" or "gpt" &&
             int.TryParse(pathComponents[1], out _) &&
             pathComponents[2].Equals("rdb", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    private async Task RepairPartitionTables(List<IPhysicalDrive> drives, string fullLogPath,
        RecoveryReport report, CancellationToken token)
    {
        OnInformationMessage($"Repairing partition tables in '{path}'");

        var command = new PartitionTableRepairCommand(NullLogger<PartitionTableRepairCommand>.Instance,
            commandHelper, drives, path, fullLogPath, null, rdbMode)
        {
            AppendLog = true
        };

        var (result, partitionTableReport) = await Run(command, token);

        if (partitionTableReport != null)
        {
            report.TargetType = partitionTableReport.TargetType;
            report.PartitionTablesFound = partitionTableReport.PartitionTablesFound;
            report.PartitionsFound = partitionTableReport.PartitionsFound;
            report.FileSystemsFound = partitionTableReport.FileSystemsFound;
            report.Written |= partitionTableReport.Written;
            report.Aborted |= partitionTableReport.Aborted;
            report.Issues.AddRange(partitionTableReport.Issues);
            report.Stats.AddRange(partitionTableReport.Stats);
        }

        if (result.IsFaulted)
        {
            report.AddIssue(RecoveryIssueLevel.Error, $"Partition table repair failed: {result.Error.Message}");
        }
    }

    private async Task RepairFileSystems(List<IPhysicalDrive> drives, string fullLogPath, string separator,
        string[] pathComponents, RecoveryReport report, CancellationToken token)
    {
        // file systems are repaired in rigid disk block partitions
        var rigidDiskBlockPath = pathComponents.Length == 0
            ? string.Concat(path.TrimEnd('\\', '/'), separator, "rdb")
            : pathComponents[^1].Equals("rdb", StringComparison.OrdinalIgnoreCase)
                ? path
                : null;

        if (rigidDiskBlockPath == null)
        {
            report.AddIssue(RecoveryIssueLevel.Info,
                "No rigid disk block in path, file systems in partitions are not repaired");
            return;
        }

        OnInformationMessage("Reading partitions in rigid disk block");

        var partitionBlocks = await ReadPartitionBlocks(drives, rigidDiskBlockPath);
        if (partitionBlocks == null)
        {
            // disk without rigid disk block, e.g. master boot record or guid partition table disk
            var rigidDiskBlockExpected = pathComponents.Length > 0 ||
                                         (report.TargetType ?? string.Empty).Contains("RDB");
            report.AddIssue(rigidDiskBlockExpected ? RecoveryIssueLevel.Error : RecoveryIssueLevel.Info,
                rigidDiskBlockExpected
                    ? "Rigid disk block can't be read after partition table repair, file systems in partitions can't be repaired"
                    : "No rigid disk block found, no file systems in partitions to repair");
            return;
        }

        for (var i = 0; i < partitionBlocks.Count; i++)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            var partitionBlock = partitionBlocks[i];
            var driveName = string.IsNullOrWhiteSpace(partitionBlock.DriveName)
                ? $"Partition #{i + 1}"
                : partitionBlock.DriveName;

            if (!RecoveryFileSystems.IsSupported(partitionBlock.DosTypeFormatted))
            {
                report.AddIssue(RecoveryIssueLevel.Info,
                    $"{driveName}: File system with dos type '{partitionBlock.DosTypeFormatted}' is not supported, skipped");
                continue;
            }

            OnInformationMessage($"Repairing file system in partition '{driveName}'");

            var command = new FsRepairCommand(NullLogger<FsRepairCommand>.Instance, commandHelper, drives,
                string.Concat(rigidDiskBlockPath, separator, i + 1), fullLogPath)
            {
                AppendLog = true
            };

            var (result, fileSystemReport) = await Run(command, token);

            report.FileSystemsChecked++;

            if (fileSystemReport != null)
            {
                report.DirectoriesFound = (report.DirectoriesFound ?? 0) + (fileSystemReport.DirectoriesFound ?? 0);
                report.FilesFound = (report.FilesFound ?? 0) + (fileSystemReport.FilesFound ?? 0);
                report.Written |= fileSystemReport.Written;
                report.Aborted |= fileSystemReport.Aborted;
                report.AddStat($"{driveName} file system", fileSystemReport.TargetType);
                report.AddStat($"{driveName} result", fileSystemReport.IsValid
                    ? fileSystemReport.Written ? "Repaired" : "Valid"
                    : "Errors not repaired");
                foreach (var issue in fileSystemReport.Issues)
                {
                    report.AddIssue(issue.Level, $"{driveName}: {issue.Message}", issue.Block, issue.Path,
                        issue.Fixed);
                }
            }

            if (result.IsFaulted)
            {
                report.AddIssue(RecoveryIssueLevel.Error,
                    $"{driveName}: File system repair failed: {result.Error.Message}");
            }
        }
    }

    /// <summary>
    /// Read partition blocks from rigid disk block at path. Returns null, if rigid disk block can't be read.
    /// </summary>
    private async Task<IList<PartitionBlock>> ReadPartitionBlocks(List<IPhysicalDrive> drives,
        string rigidDiskBlockPath)
    {
        var mediaResult = commandHelper.ResolveMedia(rigidDiskBlockPath);
        if (mediaResult.IsFaulted)
        {
            return null;
        }

        var resolvedMedia = mediaResult.Value;
        var openResult = await commandHelper.GetReadableMedia(drives, resolvedMedia.MediaPath,
            resolvedMedia.Modifiers);
        if (openResult.IsFaulted)
        {
            return null;
        }

        using var media = openResult.Value;
        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(media,
            resolvedMedia.FileSystemPath ?? string.Empty, resolvedMedia.DirectorySeparatorChar);

        try
        {
            var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(piStormRdbMediaResult.Media);
            return rigidDiskBlock?.PartitionBlocks.ToList();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Run recovery command forwarding messages, progress and log lines and return result and report.
    /// </summary>
    private async Task<(Result, RecoveryReport)> Run(RecoveryCommandBase command, CancellationToken token)
    {
        RecoveryReport report = null;
        command.DebugMessage += (_, message) => OnDebugMessage(message);
        command.WarningMessage += (_, message) => OnWarningMessage(message);
        command.InformationMessage += (_, message) => OnInformationMessage(message);
        command.DataProcessed += (_, args) => OnDataProcessed(args.Indeterminate, args.PercentComplete,
            args.BytesProcessed, args.BytesRemaining, args.BytesTotal, args.TimeElapsed, args.TimeRemaining,
            args.TimeTotal, args.BytesPerSecond);
        command.LogLineWritten += (_, line) => LogLineWritten?.Invoke(this, line);
        command.RecoveryReportRead += (_, args) => report = args.Report;

        Result result;
        try
        {
            result = await command.Execute(token);
        }
        catch (OperationCanceledException)
        {
            result = new Result(new Error("Cancelled"));
        }

        return (result, report);
    }
}
