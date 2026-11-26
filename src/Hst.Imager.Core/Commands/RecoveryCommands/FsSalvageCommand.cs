using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems.Salvage;
using Hst.Core;
using Hst.Imager.Core.UaeMetadatas;
using Microsoft.Extensions.Logging;
using SalvageUaeMetadata = Hst.Amiga.FileSystems.Salvage.UaeMetadata;
using UaeMetadata = Hst.Imager.Core.UaeMetadatas.UaeMetadata;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Salvage directories and files from Amiga file system to a directory. Amiga names, protection bits and comments
/// are stored as UAE metadata. The file system is never written to.
/// </summary>
public class FsSalvageCommand(
    ILogger<FsSalvageCommand> logger,
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string path,
    string outputPath,
    UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb,
    bool salvageOrphans = true,
    string logPath = null)
    : FileSystemRecoveryCommandBase(logger, commandHelper, physicalDrives, path, logPath)
{
    protected override RecoveryOperation Operation => RecoveryOperation.FileSystemSalvage;

    protected override IEnumerable<(string Name, object Value)> LogOptions =>
    [
        ("Output directory", outputPath),
        ("UAE metadata", uaeMetadata),
        ("Salvage orphans", salvageOrphans)
    ];

    public override async Task<Result> Execute(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return new Result(new Error("Output directory to write salvaged directories and files is required"));
        }

        if (uaeMetadata == UaeMetadata.None)
        {
            return new Result(new Error(
                "Salvage requires UAE metadata to preserve Amiga names, protection bits and comments. Use UaeFsDb or UaeMetafile"));
        }

        if (File.Exists(outputPath))
        {
            return new Result(new Error($"Output path '{outputPath}' is a file, a directory is required"));
        }

        return await base.Execute(token);
    }

    internal override async Task<Result> Recover(IFileSystemDoctorAdapter adapter, RecoveryReport report,
        CancellationToken token)
    {
        var fullOutputPath = System.IO.Path.GetFullPath(outputPath);

        if (Directory.Exists(fullOutputPath) && Directory.EnumerateFileSystemEntries(fullOutputPath).Any())
        {
            OnWarningMessage($"Output directory '{fullOutputPath}' is not empty, existing files may be overwritten");
        }

        OnInformationMessage($"Salvaging directories and files from '{Path}' to '{fullOutputPath}'");

        var target = new LoggingSalvageTarget(
            new DirectorySalvageTarget(fullOutputPath, uaeMetadata == UaeMetadata.UaeMetafile
                ? SalvageUaeMetadata.UaeMetafile
                : SalvageUaeMetadata.UaeFsDb),
            OnDebugMessage, bytes => ReportProgress(bytes, 0));

        var options = new SalvageOptions
        {
            SalvageOrphans = salvageOrphans
        };

        var result = await adapter.Salvage(target, options, token);
        await target.Flush(token);

        foreach (var message in result.Messages)
        {
            AddMessage(report, RecoveryIssueLevel.Warning, message);
        }

        if (!result.RootBlockFound)
        {
            AddMessage(report, RecoveryIssueLevel.Warning,
                "Root block not found, directories and files were salvaged by scanning blocks");
        }

        if (result.Errors > 0)
        {
            AddMessage(report, RecoveryIssueLevel.Error, $"{result.Errors} error(s) occurred during salvage");
        }

        report.Salvage = new RecoverySalvageSummary
        {
            RootBlockFound = result.RootBlockFound,
            Directories = result.Directories,
            Files = result.Files,
            PartialFiles = result.PartialFiles,
            Bytes = result.Bytes,
            OrphanedDirectories = result.OrphanedDirectories,
            Skipped = result.Skipped,
            Errors = result.Errors
        };

        report.AddStat("Output directory", fullOutputPath);
        report.AddStat("UAE metadata", uaeMetadata);

        OnInformationMessage(
            $"Salvaged {result.Directories} directories and {result.Files} files ({result.Bytes} bytes), {result.PartialFiles} partial files, {result.Skipped} skipped");

        return new Result();
    }

    /// <summary>
    /// Salvage target writing each salvaged directory and file to log and reporting bytes written.
    /// </summary>
    private class LoggingSalvageTarget(ISalvageTarget target, Action<string> log, Action<long> progress)
        : ISalvageTarget
    {
        private long _bytes;

        private static string GetPath(IReadOnlyList<string> parentPath, SalvageEntry entry) =>
            string.Join("/", parentPath.Concat([entry.Name]));

        public async Task CreateDirectory(IReadOnlyList<string> parentPath, SalvageEntry entry,
            CancellationToken cancellationToken)
        {
            log($"Directory '{GetPath(parentPath, entry)}'");
            await target.CreateDirectory(parentPath, entry, cancellationToken);
        }

        public Task SetDirectoryMetadata(IReadOnlyList<string> parentPath, SalvageEntry entry,
            CancellationToken cancellationToken) =>
            target.SetDirectoryMetadata(parentPath, entry, cancellationToken);

        public async Task WriteFile(IReadOnlyList<string> parentPath, SalvageEntry entry, Stream data,
            CancellationToken cancellationToken)
        {
            log($"File '{GetPath(parentPath, entry)}', {entry.Size} bytes, protection bits '{entry.ProtectionBits}', date {entry.Date:yyyy-MM-dd HH:mm:ss}{(string.IsNullOrEmpty(entry.Comment) ? string.Empty : $", comment '{entry.Comment}'")}");
            await target.WriteFile(parentPath, entry, data, cancellationToken);
            _bytes += entry.Size;
            progress(_bytes);
        }

        public Task Flush(CancellationToken cancellationToken) => target.Flush(cancellationToken);
    }
}
