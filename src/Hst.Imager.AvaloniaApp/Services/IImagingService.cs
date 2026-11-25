using System;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Commands.RecoveryCommands;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.UaeMetadatas;
using Hst.Amiga.RigidDiskBlocks.Recovery;

namespace Hst.Imager.AvaloniaApp.Services;

public interface IImagingService
{
    Task ReadAsync(string sourcePath, string destinationPath, long startOffset, long size, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    Task WriteAsync(string sourcePath, string destinationPath, long startOffset, long size, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    Task CompareAsync(string sourcePath, long sourceStartOffset, string destinationPath, long destinationStartOffset,
        long size, bool byteswap, IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    Task TransferAsync(string sourcePath, long srcStartOffset, string destinationPath, long destStartOffset,
        long size, bool byteswap, IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    Task BlankAsync(string path, long size, bool compatibleSize,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    Task<OptimizeResult> OptimizeAsync(string path, long size, bool compact, bool examineFileSystems, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    /// <summary>
    /// Format disk at path. Cluster size in bytes is used for FAT32 and exFAT file systems with master boot record
    /// and guid partition table format types, 0 calculates optimal cluster size.
    /// </summary>
    Task FormatAsync(string path, FormatType formatType, string fileSystem, string? fileSystemPath,
        long size, long maxPartitionSize, bool useExperimental, bool kickstart31, int clusterSize, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    Task PartitionAsync(PartitionPlan plan, IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    /// <summary>
    /// Export file system with number from rigid disk block at path to output path.
    /// </summary>
    Task ExportRdbFileSystemAsync(string path, bool byteswap, int fileSystemNumber, string outputPath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Find file system with name in media like lha, adf or iso. Returns null, if file system is not found.
    /// </summary>
    Task<RdbFileSystemInfo?> FindRdbFileSystemAsync(string mediaPath, string fileSystemName);

    /// <summary>
    /// Scan partition table at path for errors without writing any changes. Partition table type is auto detected,
    /// if null.
    /// </summary>
    Task<RecoveryReport> ScanPartitionTableAsync(string path, PartitionTableType? partitionTableType, bool byteswap,
        string logPath, IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    /// <summary>
    /// Repair partition table at path. Repair writes to disk and should be done on a backup.
    /// </summary>
    Task<RecoveryReport> RepairPartitionTableAsync(string path, PartitionTableType? partitionTableType,
        RecoveryMode rdbMode, bool byteswap, string logPath, IProgress<ProgressModel> progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Scan file system in partition at path for errors without writing any changes.
    /// </summary>
    Task<RecoveryReport> ScanFileSystemAsync(string path, bool byteswap, string logPath,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    /// <summary>
    /// Repair file system in partition at path. Repair writes to disk and should be done on a backup.
    /// </summary>
    Task<RecoveryReport> RepairFileSystemAsync(string path, bool byteswap, string logPath,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    /// <summary>
    /// Salvage directories and files from file system in partition at path to output path with uae metadata.
    /// </summary>
    Task<RecoveryReport> SalvageFileSystemAsync(string path, string outputPath, UaeMetadata uaeMetadata,
        bool salvageOrphans, bool byteswap, string logPath, IProgress<ProgressModel> progress,
        CancellationToken cancellationToken);
}
