using System;
using System.IO;
using System.Linq;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Default paths for recovery log files.
/// </summary>
public static class RecoveryLogPath
{
    /// <summary>
    /// Get default log file path for a recovery operation. For image files the log file is written next to the
    /// image file and for physical drives the log file is written in the user's home directory.
    /// </summary>
    /// <param name="mediaPath">Path to image file or physical drive (without partition path).</param>
    /// <param name="isPhysicalDrive">Media is a physical drive.</param>
    /// <param name="operation">Recovery operation.</param>
    /// <param name="timestamp">Timestamp used in log file name. Now, if not set.</param>
    public static string GetDefault(string mediaPath, bool isPhysicalDrive, RecoveryOperation operation,
        DateTime? timestamp = null)
    {
        var suffix = $"{GetOperationName(operation)}-{(timestamp ?? DateTime.Now):yyyyMMdd-HHmmss}.log";

        if (isPhysicalDrive || string.IsNullOrWhiteSpace(mediaPath))
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var driveName = GetSafeFileName(mediaPath);
            return Path.Combine(homeDir,
                string.IsNullOrWhiteSpace(driveName)
                    ? $"hst-imager-{suffix}"
                    : $"hst-imager-{driveName}-{suffix}");
        }

        var fullPath = Path.GetFullPath(mediaPath);
        var dir = Path.GetDirectoryName(fullPath) ?? string.Empty;
        return Path.Combine(dir, $"{Path.GetFileName(fullPath)}.{suffix}");
    }

    /// <summary>
    /// Get operation name used in log file names.
    /// </summary>
    public static string GetOperationName(RecoveryOperation operation) => operation switch
    {
        RecoveryOperation.PartitionTableScan => "partitiontable-scan",
        RecoveryOperation.PartitionTableRepair => "partitiontable-repair",
        RecoveryOperation.FileSystemScan => "fs-scan",
        RecoveryOperation.FileSystemRepair => "fs-repair",
        RecoveryOperation.FileSystemSalvage => "fs-salvage",
        _ => "recovery"
    };

    private static string GetSafeFileName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var name = new string(path.Select(c => invalidChars.Contains(c) || c == '.' ? '_' : c).ToArray())
            .Trim('_');
        return name.ToLowerInvariant();
    }
}
