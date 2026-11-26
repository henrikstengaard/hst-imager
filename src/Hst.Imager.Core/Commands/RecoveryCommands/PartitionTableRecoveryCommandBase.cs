using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.RigidDiskBlocks.Recovery;
using Hst.Core;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Base for scanning and repairing partition tables: Rigid Disk Block, Master Boot Record and Guid Partition Table.
/// </summary>
public abstract class PartitionTableRecoveryCommandBase : RecoveryCommandBase
{
    private const int RdbSearchBlocks = 16;
    private readonly PartitionTableType? _partitionTableType;

    protected PartitionTableRecoveryCommandBase(ILogger logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, string logPath,
        PartitionTableType? partitionTableType)
        : base(logger, commandHelper, physicalDrives, path, logPath)
    {
        _partitionTableType = partitionTableType is PartitionTableType.None ? null : partitionTableType;
    }

    protected abstract bool Repair { get; }

    protected virtual RecoveryMode RdbMode => RecoveryMode.Auto;

    protected override bool Writable => Repair;

    protected override IEnumerable<(string Name, object Value)> LogOptions =>
    [
        ("Partition table", _partitionTableType?.ToString() ?? "Auto detect"),
        ("Rigid disk block mode", RdbMode)
    ];

    protected override async Task<Result> Recover(RecoveryContext context, RecoveryReport report,
        CancellationToken token)
    {
        var typeResult = GetPartitionTableType(context.PathComponents);
        if (typeResult.IsFaulted)
        {
            return new Result(typeResult.Error);
        }

        var stream = context.Stream;
        var diskSize = context.Media.Size > 0 ? context.Media.Size : stream.Length;
        if (diskSize <= 0)
        {
            return new Result(new Error($"Unable to determine size of '{Path}'"));
        }

        var types = typeResult.Value.HasValue
            ? [typeResult.Value.Value]
            : await DetectPartitionTables(stream, diskSize, token);

        if (types.Count == 0)
        {
            AddMessage(report, RecoveryIssueLevel.Error,
                "No partition table found. Specify partition table type to scan or repair, e.g. rigid disk block");
            report.TargetType = "None";
            report.PartitionTablesFound = 0;
            report.PartitionsFound = 0;
            return new Result();
        }

        report.TargetType = string.Join(", ", types.Select(GetName));
        report.PartitionTablesFound = 0;
        report.PartitionsFound = 0;
        OnInformationMessage($"{(Repair ? "Repairing" : "Scanning")} {report.TargetType} in '{Path}'");

        foreach (var type in types)
        {
            token.ThrowIfCancellationRequested();

            switch (type)
            {
                case PartitionTableType.RigidDiskBlock:
                    await RecoverRigidDiskBlock(stream, diskSize, report, token);
                    break;
                case PartitionTableType.GuidPartitionTable:
                    await RecoverGuidPartitionTable(stream, diskSize, report, token);
                    break;
                case PartitionTableType.MasterBootRecord:
                    await RecoverMasterBootRecord(stream, diskSize, report, token);
                    break;
            }
        }

        if (Repair && report.Written)
        {
            await stream.FlushAsync(token);
        }

        OnInformationMessage(report.IsValid
            ? $"{report.TargetType} is valid"
            : $"{report.TargetType} has {report.Errors} error(s)");

        return new Result();
    }

    private Result<PartitionTableType?> GetPartitionTableType(string[] pathComponents)
    {
        PartitionTableType? pathType = null;
        if (pathComponents.Length > 0)
        {
            pathType = pathComponents[0].ToLowerInvariant() switch
            {
                "rdb" => PartitionTableType.RigidDiskBlock,
                "mbr" => PartitionTableType.MasterBootRecord,
                "gpt" => PartitionTableType.GuidPartitionTable,
                _ => null
            };

            if (pathType == null || pathComponents.Length > 1)
            {
                return new Result<PartitionTableType?>(new Error(
                    $"Invalid partition table path '{Path}', expected disk or disk with 'rdb', 'mbr' or 'gpt'"));
            }
        }

        if (pathType.HasValue && _partitionTableType.HasValue && pathType != _partitionTableType)
        {
            return new Result<PartitionTableType?>(new Error(
                $"Partition table type '{_partitionTableType}' doesn't match path '{Path}'"));
        }

        return new Result<PartitionTableType?>(pathType ?? _partitionTableType);
    }

    private async Task<IList<PartitionTableType>> DetectPartitionTables(Stream stream, long diskSize,
        CancellationToken token)
    {
        var types = new List<PartitionTableType>();

        if (await GptValidator.IsPresent(stream, diskSize, token))
        {
            OnDebugMessage("Guid partition table detected");
            types.Add(PartitionTableType.GuidPartitionTable);
        }
        else if (await MbrValidator.IsPresent(stream, token))
        {
            OnDebugMessage("Master boot record detected");
            types.Add(PartitionTableType.MasterBootRecord);
        }

        if (await HasRigidDiskBlockSignature(stream, token) || types.Count == 0)
        {
            OnDebugMessage(types.Count == 0
                ? "No master boot record or guid partition table detected, scanning for rigid disk block"
                : "Rigid disk block detected");
            types.Add(PartitionTableType.RigidDiskBlock);
        }

        return types;
    }

    private static async Task<bool> HasRigidDiskBlockSignature(Stream stream, CancellationToken token)
    {
        var buffer = new byte[512];
        for (var block = 0; block < RdbSearchBlocks; block++)
        {
            stream.Position = block * 512L;
            var read = await stream.ReadAsync(buffer, token);
            if (read < 4)
            {
                return false;
            }

            if (buffer[0] == 'R' && buffer[1] == 'D' && buffer[2] == 'S' && buffer[3] == 'K')
            {
                return true;
            }
        }

        return false;
    }

    private async Task RecoverRigidDiskBlock(Stream stream, long diskSize, RecoveryReport report,
        CancellationToken token)
    {
        OnInformationMessage(Repair
            ? $"Repairing rigid disk block using mode '{RdbMode}'"
            : "Analyzing rigid disk block and scanning disk for file systems");

        // strict read of rigid disk block fails, if checksums or block pointers are invalid
        var strictReadError = await ReadRigidDiskBlockStrict(stream);
        if (strictReadError != null)
        {
            OnDebugMessage($"Strict read of rigid disk block failed: {strictReadError}");
        }

        var options = new RecoveryOptions
        {
            Mode = RdbMode,
            DiskSize = diskSize,
            Progress = new ActionProgress<long>(bytes => ReportProgress(bytes, diskSize))
        };

        var result = Repair
            ? await RigidDiskBlockRecovery.Repair(stream, options, token)
            : await RigidDiskBlockRecovery.Analyze(stream, options, token);

        ReportProgress(diskSize, diskSize);

        foreach (var message in result.Messages)
        {
            AddMessage(report, message.Level switch
            {
                RecoveryMessageLevel.Error => RecoveryIssueLevel.Error,
                RecoveryMessageLevel.Warning => RecoveryIssueLevel.Warning,
                _ => RecoveryIssueLevel.Info
            }, $"RDB: {message.Text}", isFixed: Repair && result.Written && message.Level == RecoveryMessageLevel.Warning);
        }

        foreach (var fileSystem in result.FileSystems)
        {
            AddMessage(report, RecoveryIssueLevel.Info, $"RDB: File system found: {fileSystem}",
                fileSystem.StartSector);
        }

        report.FileSystemsFound = (report.FileSystemsFound ?? 0) + result.FileSystems.Count;
        if (strictReadError == null || result.RigidDiskBlock != null)
        {
            report.PartitionTablesFound++;
            report.PartitionsFound += result.RigidDiskBlock?.PartitionBlocks.Count() ?? 0;
        }

        report.AddStat("RDB mode", result.Mode);
        report.AddStat("RDB file systems found", result.FileSystems.Count);
        if (result.RigidDiskBlock != null)
        {
            report.AddStat("RDB partitions", result.RigidDiskBlock.PartitionBlocks.Count());
        }

        if (result.RigidDiskBlock == null || !result.IsValid)
        {
            AddMessage(report, RecoveryIssueLevel.Error, "RDB: Rigid disk block could not be recovered without errors");
        }

        if (!Repair)
        {
            if (strictReadError != null)
            {
                AddMessage(report, RecoveryIssueLevel.Error,
                    $"RDB: Rigid disk block on disk is missing or damaged: {strictReadError}");
            }

            if (result.Modified)
            {
                AddMessage(report, RecoveryIssueLevel.Warning,
                    "RDB: Recovered rigid disk block differs from rigid disk block on disk, repair can update it");
            }

            return;
        }

        if (result.Written)
        {
            report.Written = true;
            AddMessage(report, RecoveryIssueLevel.Info, "RDB: Repaired rigid disk block was written to disk");
        }
        else if (result.Modified)
        {
            AddMessage(report, RecoveryIssueLevel.Error, "RDB: Repaired rigid disk block was not written to disk");
        }
        else if (strictReadError == null)
        {
            AddMessage(report, RecoveryIssueLevel.Info, "RDB: Rigid disk block is valid, no repair needed");
        }

        if (strictReadError != null)
        {
            var repairedReadError = await ReadRigidDiskBlockStrict(stream);
            AddMessage(report, RecoveryIssueLevel.Error,
                $"RDB: Rigid disk block on disk is missing or damaged: {strictReadError}",
                isFixed: repairedReadError == null);
            if (repairedReadError != null)
            {
                AddMessage(report, RecoveryIssueLevel.Error,
                    $"RDB: Rigid disk block is still damaged after repair: {repairedReadError}");
            }
        }
    }

    /// <summary>
    /// Read rigid disk block with checksum and block validation.
    /// </summary>
    /// <returns>Null, if rigid disk block is valid. Otherwise error message.</returns>
    private static async Task<string> ReadRigidDiskBlockStrict(Stream stream)
    {
        try
        {
            stream.Position = 0;
            var rigidDiskBlock = await Hst.Amiga.RigidDiskBlocks.RigidDiskBlockReader.Read(stream);
            return rigidDiskBlock == null ? "Rigid disk block not found" : null;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    private async Task RecoverGuidPartitionTable(Stream stream, long diskSize, RecoveryReport report,
        CancellationToken token)
    {
        OnInformationMessage("Validating guid partition table");

        // when repairing, issues found before repair are logged, but only issues left after repair are reported
        var scanReport = Repair ? new RecoveryReport() : report;
        var result = await GptValidator.Validate(stream, diskSize, (level, message, block) =>
            AddMessage(scanReport, level, $"GPT: {message}", block), token);

        if (result.Primary.SignatureValid || result.Backup.SignatureValid)
        {
            report.PartitionTablesFound++;
            report.PartitionsFound += result.Partitions;
        }

        report.AddStat("GPT sector size", result.SectorSize);
        report.AddStat("GPT partitions", result.Partitions);

        if (!Repair)
        {
            return;
        }

        if (scanReport.IsValid)
        {
            AddMessage(report, RecoveryIssueLevel.Info, "GPT: Guid partition table is valid, no repair needed");
            return;
        }

        OnInformationMessage("Repairing guid partition table");

        var written = await GptValidator.Repair(stream, result, (level, message, block) =>
            AddMessage(report, level, $"GPT: {message}", block), token);
        report.Written |= written;

        if (!written)
        {
            return;
        }

        // validate repaired guid partition table
        var verifyReport = new RecoveryReport();
        await GptValidator.Validate(stream, diskSize, (level, message, block) =>
            verifyReport.AddIssue(level, $"GPT: {message}", block), token);

        foreach (var issue in scanReport.Issues.Where(x => x.Level == RecoveryIssueLevel.Error))
        {
            var stillPresent = verifyReport.Issues.Any(x => x.Message == issue.Message);
            report.AddIssue(issue.Level, issue.Message, issue.Block, issue.Path, !stillPresent);
        }

        foreach (var issue in verifyReport.Issues.Where(x =>
                     x.Level == RecoveryIssueLevel.Error && scanReport.Issues.All(s => s.Message != x.Message)))
        {
            AddMessage(report, issue.Level, issue.Message, issue.Block);
        }
    }

    private async Task RecoverMasterBootRecord(Stream stream, long diskSize, RecoveryReport report,
        CancellationToken token)
    {
        OnInformationMessage("Validating master boot record");

        var partitions = await MbrValidator.Validate(stream, diskSize, (level, message, block) =>
            AddMessage(report, level, $"MBR: {message}", block), token);

        if (await MbrValidator.IsPresent(stream, token))
        {
            report.PartitionTablesFound++;
            report.PartitionsFound += partitions;
        }

        report.AddStat("MBR partitions", partitions);

        if (Repair && !report.IsValid)
        {
            AddMessage(report, RecoveryIssueLevel.Warning,
                "MBR: Repair of master boot record is not supported, errors must be fixed manually");
        }
    }

    private static string GetName(PartitionTableType type) => type switch
    {
        PartitionTableType.RigidDiskBlock => "RDB",
        PartitionTableType.MasterBootRecord => "MBR",
        PartitionTableType.GuidPartitionTable => "GPT",
        _ => type.ToString()
    };
}

/// <summary>
/// Progress calling action synchronously.
/// </summary>
internal class ActionProgress<T>(Action<T> action) : IProgress<T>
{
    public void Report(T value) => action(value);
}
