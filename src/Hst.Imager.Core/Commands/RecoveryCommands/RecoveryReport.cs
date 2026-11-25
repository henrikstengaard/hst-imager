using System.Collections.Generic;
using System.Linq;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Recovery operation.
/// </summary>
public enum RecoveryOperation
{
    PartitionTableScan,
    PartitionTableRepair,
    FileSystemScan,
    FileSystemRepair,
    FileSystemSalvage,
    FullRepair
}

/// <summary>
/// Level of recovery issue.
/// </summary>
public enum RecoveryIssueLevel
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Issue found by a recovery operation.
/// </summary>
public class RecoveryIssue
{
    public RecoveryIssueLevel Level { get; init; }
    public string Message { get; init; }

    /// <summary>
    /// Block number issue was found in, if known.
    /// </summary>
    public long? Block { get; init; }

    /// <summary>
    /// Path to directory or file issue was found in, if known.
    /// </summary>
    public string Path { get; init; }

    /// <summary>
    /// Issue was fixed by repair.
    /// </summary>
    public bool Fixed { get; init; }

    public override string ToString()
    {
        var parts = new List<string> { Level.ToString().ToUpperInvariant() };
        if (Block.HasValue)
        {
            parts.Add($"block {Block.Value}");
        }

        if (!string.IsNullOrWhiteSpace(Path))
        {
            parts.Add($"'{Path}'");
        }

        return $"[{string.Join(", ", parts)}] {Message}{(Fixed ? " (fixed)" : string.Empty)}";
    }
}

/// <summary>
/// Summary of salvaged directories and files.
/// </summary>
public class RecoverySalvageSummary
{
    public bool RootBlockFound { get; init; }
    public int Directories { get; init; }
    public int Files { get; init; }
    public int PartialFiles { get; init; }
    public long Bytes { get; init; }
    public int OrphanedDirectories { get; init; }
    public int Skipped { get; init; }
    public int Errors { get; init; }
}

/// <summary>
/// Report from a recovery operation with issues found, statistics and salvage summary.
/// </summary>
public class RecoveryReport
{
    public RecoveryOperation Operation { get; init; }

    /// <summary>
    /// Path to disk, partition table or partition the operation was done on.
    /// </summary>
    public string Target { get; init; }

    /// <summary>
    /// Type of partition table or file system, e.g. "RDB", "GPT", "PFS3" or "FFS".
    /// </summary>
    public string TargetType { get; set; }

    /// <summary>
    /// Path to log file written by the operation.
    /// </summary>
    public string LogPath { get; set; }

    /// <summary>
    /// Operation was aborted.
    /// </summary>
    public bool Aborted { get; set; }

    /// <summary>
    /// Changes were written to disk by repair.
    /// </summary>
    public bool Written { get; set; }

    public List<RecoveryIssue> Issues { get; } = [];

    /// <summary>
    /// Statistics in the order they were added, e.g. files, directories and blocks checked.
    /// </summary>
    public List<KeyValuePair<string, string>> Stats { get; } = [];

    /// <summary>
    /// Summary of salvaged directories and files. Null, if operation is not salvage.
    /// </summary>
    public RecoverySalvageSummary Salvage { get; set; }

    /// <summary>
    /// Number of partition tables found. Null, if operation is not a partition table operation.
    /// </summary>
    public int? PartitionTablesFound { get; set; }

    /// <summary>
    /// Number of partitions found in partition tables. Null, if operation is not a partition table operation.
    /// </summary>
    public int? PartitionsFound { get; set; }

    /// <summary>
    /// Number of file systems found on disk while scanning for rigid disk block. Null, if not scanned.
    /// </summary>
    public int? FileSystemsFound { get; set; }

    /// <summary>
    /// Number of directories found in file system. Null, if operation is not a file system scan or repair.
    /// </summary>
    public int? DirectoriesFound { get; set; }

    /// <summary>
    /// Number of files found in file system. Null, if operation is not a file system scan or repair.
    /// </summary>
    public int? FilesFound { get; set; }

    /// <summary>
    /// Number of file systems checked or repaired in partitions. Null, if operation is not a full repair.
    /// </summary>
    public int? FileSystemsChecked { get; set; }

    public int Errors => Issues.Count(x => x.Level == RecoveryIssueLevel.Error);
    public int Warnings => Issues.Count(x => x.Level == RecoveryIssueLevel.Warning);
    public int Fixed => Issues.Count(x => x.Fixed);

    /// <summary>
    /// Number of errors and warnings found.
    /// </summary>
    public int IssuesFound => Issues.Count(x => x.Level != RecoveryIssueLevel.Info);

    /// <summary>
    /// Number of errors and warnings, which were not fixed.
    /// </summary>
    public int IssuesNotFixed => Issues.Count(x => x.Level != RecoveryIssueLevel.Info && !x.Fixed);

    /// <summary>
    /// Partition table or file system is valid, if not aborted and no errors are left unfixed.
    /// </summary>
    public bool IsValid => !Aborted && Issues.All(x => x.Level != RecoveryIssueLevel.Error || x.Fixed);

    public void AddIssue(RecoveryIssueLevel level, string message, long? block = null, string path = null,
        bool isFixed = false) =>
        Issues.Add(new RecoveryIssue
        {
            Level = level,
            Message = message,
            Block = block,
            Path = path,
            Fixed = isFixed
        });

    public void AddStat(string name, object value) =>
        Stats.Add(new KeyValuePair<string, string>(name, value?.ToString() ?? string.Empty));
}
