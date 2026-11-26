using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.MagicBytes;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Base for checking, repairing and salvaging Amiga file systems in rigid disk block partitions and adf files.
/// </summary>
public abstract partial class FileSystemRecoveryCommandBase(
    ILogger logger,
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string path,
    string logPath)
    : RecoveryCommandBase(logger, commandHelper, physicalDrives, path, logPath)
{
    /// <summary>
    /// Run recovery operation using file system doctor adapter.
    /// </summary>
    internal abstract Task<Result> Recover(IFileSystemDoctorAdapter adapter, RecoveryReport report,
        CancellationToken token);

    /// <summary>
    /// Callbacks for file system doctors writing messages to log and adding issues to report.
    /// </summary>
    private FileSystemDoctorCallbacks CreateCallbacks(RecoveryReport report) => new()
    {
        Log = LogEngineMessage,
        Progress = ReportProgress,
        AddIssue = (level, message, block, issuePath, isFixed) =>
            AddMessage(report, level, message, block, issuePath, isFixed)
    };

    protected override async Task<Result> Recover(RecoveryContext context, RecoveryReport report,
        CancellationToken token)
    {
        var adapterResult = await CreateAdapter(context, report);
        if (adapterResult.IsFaulted)
        {
            return new Result(adapterResult.Error);
        }

        var adapter = adapterResult.Value;
        report.TargetType = adapter.Name;
        OnInformationMessage($"File system {adapter.Name}");

        var result = await Recover(adapter, report, token);

        if (report.Written)
        {
            await context.Stream.FlushAsync(token);
        }

        return result;
    }

    private async Task<Result<IFileSystemDoctorAdapter>> CreateAdapter(RecoveryContext context,
        RecoveryReport report)
    {
        var callbacks = CreateCallbacks(report);
        var pathComponents = context.PathComponents;

        if (pathComponents.Length == 0)
        {
            var dataType = await CommandHelper.DetectDataType(context.MediaPath);
            var isAdf = dataType == DataType.Adf ||
                        System.IO.Path.GetExtension(context.MediaPath)
                            .Equals(".adf", StringComparison.OrdinalIgnoreCase);
            if (isAdf)
            {
                OnDebugMessage("Using adf with Fast File System");
                return new Result<IFileSystemDoctorAdapter>(
                    FileSystemDoctorAdapterFactory.CreateAdf(context.Stream, callbacks));
            }

            return new Result<IFileSystemDoctorAdapter>(new Error(
                $"Path '{Path}' doesn't contain a partition, e.g. '{System.IO.Path.Combine(Path, "rdb", "dh0")}'"));
        }

        if (!pathComponents[0].Equals("rdb", StringComparison.OrdinalIgnoreCase))
        {
            return new Result<IFileSystemDoctorAdapter>(new Error(
                $"File system recovery is only supported for Amiga file systems in rigid disk block partitions and adf files, path '{Path}' is not supported"));
        }

        if (pathComponents.Length != 2)
        {
            return new Result<IFileSystemDoctorAdapter>(new Error(
                $"Path '{Path}' must contain a rigid disk block partition number or device name, e.g. 'rdb{System.IO.Path.DirectorySeparatorChar}dh0'"));
        }

        OnDebugMessage("Reading rigid disk block");

        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(context.Media);
        if (rigidDiskBlock == null)
        {
            return new Result<IFileSystemDoctorAdapter>(new Error(
                $"Rigid disk block not found or damaged in '{Path}'. Scan and repair the partition table first with recovery partition table repair"));
        }

        var partition = pathComponents[1];
        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
        var partitionBlock = NumberRegex().IsMatch(partition) &&
                             int.TryParse(partition, out var partitionNumber) &&
                             partitionNumber >= 1 && partitionNumber <= partitionBlocks.Count
            ? partitionBlocks[partitionNumber - 1]
            : partitionBlocks.FirstOrDefault(x => x.DriveName.Equals(partition, StringComparison.OrdinalIgnoreCase));

        if (partitionBlock == null)
        {
            return new Result<IFileSystemDoctorAdapter>(new Error($"Partition '{partition}' not found"));
        }

        OnInformationMessage(
            $"Partition '{partitionBlock.DriveName}' with dos type '{partitionBlock.DosTypeFormatted}', cylinders {partitionBlock.LowCyl}-{partitionBlock.HighCyl}");

        var adapter = FileSystemDoctorAdapterFactory.Create(context.Stream, partitionBlock, callbacks);
        return adapter == null
            ? new Result<IFileSystemDoctorAdapter>(new Error(
                $"File system with dos type '{partitionBlock.DosTypeFormatted}' is not supported. Supported file systems are FFS (DOS\\0-DOS\\7), PFS3 (PFS\\3, PDS\\3) and SFS (SFS\\0)"))
            : new Result<IFileSystemDoctorAdapter>(adapter);
    }

    [GeneratedRegex("^\\d+$")]
    private static partial Regex NumberRegex();
}
