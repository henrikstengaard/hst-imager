using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Commands.GptCommands;
using Hst.Imager.Core.FileSystems;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.PhysicalDrives;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.AvaloniaApp.Services;

public class ImagingService : IImagingService
{
    public const string WritingToCacheStage = "Writing data to cache";
    public const string WritingCachedDataStage = "Writing cache to destination";

    private readonly ILoggerFactory _loggerFactory;
    private readonly AppStateModel _appState;

    public ImagingService(ILoggerFactory loggerFactory, AppStateModel appState)
    {
        _loggerFactory = loggerFactory;
        _appState = appState;
    }

    public async Task ReadAsync(string sourcePath, string destinationPath, long startOffset, long size, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken)
    {
        var physicalDrives = await GetPhysicalDrivesAsync();
        var stage = GetCacheStage(destinationPath, physicalDrives);
        progress.Report(new ProgressModel { Title = "Reading", Stage = stage, PercentComplete = 0 });
        using var cache = TrackCache(progress, cancellationToken);
        using var commandHelper = CreateCommandHelper();

        var readPath = string.Concat(byteswap ? "+bs:" : string.Empty, sourcePath);
        var cmd = new ReadCommand(
            _loggerFactory.CreateLogger<ReadCommand>(), commandHelper, physicalDrives,
            readPath, destinationPath,
            new Size(size, Unit.Bytes), _appState.Settings.Retries,
            _appState.Settings.Verify, _appState.Settings.Force, startOffset,
            _appState.Settings.SparseFiles);

        cmd.DataProcessed += (_, args) => progress.Report(MapProgress("Reading", args, stage));
        var result = await cmd.Execute(cache.Token);
        ThrowIfFaulted(result);
    }

    public async Task WriteAsync(string sourcePath, string destinationPath, long startOffset, long size, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken)
    {
        var physicalDrives = await GetPhysicalDrivesAsync();
        var stage = GetCacheStage(destinationPath, physicalDrives);
        progress.Report(new ProgressModel { Title = "Writing", Stage = stage, PercentComplete = 0 });
        using var cache = TrackCache(progress, cancellationToken);
        using var commandHelper = CreateCommandHelper();

        var writePath = string.Concat(byteswap ? "+bs:" : string.Empty, sourcePath);
        var cmd = new WriteCommand(
            _loggerFactory.CreateLogger<WriteCommand>(), commandHelper, physicalDrives,
            writePath, destinationPath,
            new Size(size, Unit.Bytes), _appState.Settings.Retries,
            _appState.Settings.Verify, _appState.Settings.Force, _appState.Settings.SkipUnusedSectors,
            startOffset);

        cmd.DataProcessed += (_, args) => progress.Report(MapProgress("Writing", args, stage));
        var result = await cmd.Execute(cache.Token);
        ThrowIfFaulted(result);
    }

    public async Task CompareAsync(string sourcePath, long sourceStartOffset, string destinationPath, long destinationStartOffset,
        long size, bool byteswap, IProgress<ProgressModel> progress, CancellationToken cancellationToken)
    {
        // compare only reads, so nothing is written to cache
        progress.Report(new ProgressModel { Title = "Comparing", PercentComplete = 0 });
        using var cache = TrackCache(progress, cancellationToken);

        var physicalDrives = await GetPhysicalDrivesAsync();
        using var commandHelper = CreateCommandHelper();

        var cmpPath = string.Concat(byteswap ? "+bs:" : string.Empty, sourcePath);
        var cmd = new CompareCommand(
            _loggerFactory.CreateLogger<CompareCommand>(), commandHelper, physicalDrives,
            cmpPath, sourceStartOffset, destinationPath, destinationStartOffset,
            new Size(size, Unit.Bytes), _appState.Settings.Retries,
            _appState.Settings.Force, _appState.Settings.SkipUnusedSectors);

        cmd.DataProcessed += (_, args) => progress.Report(MapProgress("Comparing", args, null));
        var result = await cmd.Execute(cache.Token);
        ThrowIfFaulted(result);
    }

    public async Task TransferAsync(string sourcePath, long srcStartOffset, string destinationPath, long destStartOffset,
        long size, bool byteswap, IProgress<ProgressModel> progress, CancellationToken cancellationToken)
    {
        var stage = GetCacheStage(destinationPath, []);
        progress.Report(new ProgressModel { Title = "Transferring", Stage = stage, PercentComplete = 0 });
        using var cache = TrackCache(progress, cancellationToken);
        using var commandHelper = CreateCommandHelper();

        var transferPath = string.Concat(byteswap ? "+bs:" : string.Empty, sourcePath);
        var cmd = new TransferCommand(commandHelper, transferPath, destinationPath,
            new Size(size, Unit.Bytes), false, srcStartOffset, destStartOffset,
            _appState.Settings.SparseFiles);

        cmd.DataProcessed += (_, args) => progress.Report(MapProgress("Transferring", args, stage));
        var result = await cmd.Execute(cache.Token);
        ThrowIfFaulted(result);
    }

    public async Task BlankAsync(string path, long size, bool compatibleSize,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken)
    {
        var stage = GetCacheStage(path, []);
        progress.Report(new ProgressModel { Title = "Creating blank image", Stage = stage, PercentComplete = 50 });
        using var cache = TrackCache(progress, cancellationToken);
        using var commandHelper = CreateCommandHelper();

        var cmd = new BlankCommand(_loggerFactory.CreateLogger<BlankCommand>(), commandHelper,
            path, new Size(size, Unit.Bytes), compatibleSize);

        var result = await cmd.Execute(cache.Token);
        ThrowIfFaulted(result);
    }

    public async Task OptimizeAsync(string path, long size, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken)
    {
        var stage = GetCacheStage(path, []);
        progress.Report(new ProgressModel { Title = "Optimizing", Stage = stage, PercentComplete = 50 });
        using var cache = TrackCache(progress, cancellationToken);
        using var commandHelper = CreateCommandHelper();

        var optimizePath = string.Concat(byteswap ? "+bs:" : string.Empty, path);
        var cmd = new OptimizeCommand(commandHelper, optimizePath, new Size(size, Unit.Bytes), PartitionTable.None);

        var result = await cmd.Execute(cache.Token);
        ThrowIfFaulted(result);
    }

    public async Task FormatAsync(string path, FormatType formatType, string fileSystem, string? fileSystemPath,
        long size, long maxPartitionSize, bool useExperimental, bool kickstart31, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken)
    {
        var physicalDrives = await GetPhysicalDrivesAsync();
        var stage = GetCacheStage(path, physicalDrives);
        progress.Report(new ProgressModel { Title = "Formatting", Stage = stage, PercentComplete = 0 });
        using var cache = TrackCache(progress, cancellationToken);
        using var commandHelper = CreateCommandHelper();

        var formatPath = string.Concat(byteswap ? "+bs:" : string.Empty, path);
        var cmd = new FormatCommand(
            _loggerFactory.CreateLogger<FormatCommand>(), _loggerFactory, commandHelper, physicalDrives,
            formatPath, formatType, fileSystem, fileSystemPath, _appState.AppDataPath,
            new Size(size, Unit.Bytes), new Size(maxPartitionSize, Unit.Bytes),
            useExperimental, kickstart31);

        cmd.DataProcessed += (_, args) => progress.Report(MapProgress("Formatting", args, stage));
        var result = await cmd.Execute(cache.Token);
        ThrowIfFaulted(result);
    }

    /// <summary>
    /// Partition disk by initializing partition table or deleting partitions, then adding and formatting partitions.
    /// Partitions are formatted after all partitions are added, as partition numbers can change when adding.
    /// </summary>
    public async Task PartitionAsync(PartitionPlan plan, IProgress<ProgressModel> progress,
        CancellationToken cancellationToken)
    {
        var physicalDrives = await GetPhysicalDrivesAsync();
        var stage = GetCacheStage(plan.Path, physicalDrives);
        using var cache = TrackCache(progress, cancellationToken);
        using var commandHelper = CreateCommandHelper();
        var token = cache.Token;

        var path = string.Concat(plan.Byteswap ? "+bs:" : string.Empty, plan.Path);

        // PiStorm disk in master boot record partition added by preceding plan is partitioned using partition number
        // of master boot record partition read from disk
        if (plan.ContainerStartOffset.HasValue)
        {
            var mbrPartitionNumbers = await ReadPartitionNumbersAsync(commandHelper, physicalDrives, path,
                PartitionTableType.MasterBootRecord, token);
            if (!mbrPartitionNumbers.TryGetValue(plan.ContainerStartOffset.Value, out var mbrPartitionNumber))
            {
                throw new ImagingException(
                    $"PiStorm partition at offset {plan.ContainerStartOffset.Value} not found in Master Boot Record");
            }

            var separator = plan.Path.StartsWith('/') ? "/" : "\\";
            path = string.Concat(path, separator, "mbr", separator, mbrPartitionNumber);
        }

        var isRdb = plan.TableType == PartitionTableType.RigidDiskBlock;
        var rdbDosTypes = isRdb
            ? plan.AddPartitions.Select(x => GetDosType(x.FileSystem)).Distinct().ToList()
            : [];

        var steps = (plan.Initialize ? 2 : 0) + plan.DeletePartitionNumbers.Count + plan.UpdateFileSystems.Count +
                    plan.DeleteFileSystemNumbers.Count + plan.AddFileSystems.Count * 2 + rdbDosTypes.Count +
                    plan.AddPartitions.Count * 2 + plan.FormatPartitions.Count + 1;
        var stepsExecuted = 0;

        void ReportStep() => progress.Report(new ProgressModel
        {
            Title = "Partitioning",
            Stage = stage,
            PercentComplete = Math.Min(100, Math.Round(100d / steps * stepsExecuted))
        });

        async Task Run(CommandBase command)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfFaulted(await command.Execute(token));
            stepsExecuted++;
            ReportStep();
        }

        ReportStep();

        // initialize partition table
        if (plan.Initialize)
        {
            // erase partition tables for whole disk, a part of it like a PiStorm disk only has rigid disk block.
            // keeping master boot record for a hybrid disk only erases sectors rigid disk block can be written to
            if (plan.KeepMasterBootRecord)
                await EraseRigidDiskBlockSectorsAsync(commandHelper, physicalDrives, path, token);
            else if (plan.IsDiskPath)
                await ErasePartitionTablesAsync(commandHelper, physicalDrives, path, token);
            stepsExecuted++;
            ReportStep();

            await Run(plan.TableType switch
            {
                PartitionTableType.MasterBootRecord => new MbrInitCommand(
                    _loggerFactory.CreateLogger<MbrInitCommand>(), commandHelper, physicalDrives, path),
                PartitionTableType.GuidPartitionTable => new GptInitCommand(
                    _loggerFactory.CreateLogger<GptInitCommand>(), commandHelper, physicalDrives, path),
                PartitionTableType.RigidDiskBlock => new RdbInitCommand(
                    _loggerFactory.CreateLogger<RdbInitCommand>(), commandHelper, physicalDrives, path,
                    "HstImager", new Size(plan.RdbSize, Unit.Bytes), string.Empty, plan.RdbBlockLo),
                _ => throw new ImagingException($"Unsupported partition table '{plan.TableType}'")
            });
        }

        // delete partitions from highest number, so partition numbers to delete are not changed by deleting
        foreach (var partitionNumber in plan.DeletePartitionNumbers.OrderByDescending(x => x))
        {
            await Run(plan.TableType switch
            {
                PartitionTableType.MasterBootRecord => new MbrPartDelCommand(
                    _loggerFactory.CreateLogger<MbrPartDelCommand>(), commandHelper, physicalDrives, path,
                    partitionNumber),
                PartitionTableType.GuidPartitionTable => new GptPartDelCommand(
                    _loggerFactory.CreateLogger<GptPartDelCommand>(), commandHelper, physicalDrives, path,
                    partitionNumber),
                PartitionTableType.RigidDiskBlock => new RdbPartDelCommand(
                    _loggerFactory.CreateLogger<RdbInitCommand>(), commandHelper, physicalDrives, path,
                    partitionNumber),
                _ => throw new ImagingException($"Unsupported partition table '{plan.TableType}'")
            });
        }

        // update existing file systems by number before deleting file systems changes their numbers
        foreach (var update in plan.UpdateFileSystems.OrderBy(x => x.Number))
        {
            await Run(new RdbFsUpdateCommand(_loggerFactory.CreateLogger<RdbFsUpdateCommand>(), commandHelper,
                physicalDrives, path, update.Number, update.DosType ?? string.Empty, update.Name ?? string.Empty,
                update.Path ?? string.Empty));
        }

        // delete file systems from highest number, so file system numbers to delete are not changed by deleting
        foreach (var fileSystemNumber in plan.DeleteFileSystemNumbers.OrderByDescending(x => x))
        {
            await Run(new RdbFsDelCommand(_loggerFactory.CreateLogger<RdbFsDelCommand>(), commandHelper,
                physicalDrives, path, fileSystemNumber));
        }

        // add and import file systems, which replace existing file systems with same dos type. name is set after
        // adding, as file system name is set to name of file or name found in media when added
        foreach (var fileSystem in plan.AddFileSystems)
        {
            if (fileSystem.IsImport)
            {
                await ThrowIfFileSystemNotInMediaAsync(commandHelper, fileSystem.Path, fileSystem.Name);
                await Run(new RdbFsImportCommand(_loggerFactory.CreateLogger<RdbFsImportCommand>(), commandHelper,
                    physicalDrives, path, fileSystem.Path, fileSystem.DosType, fileSystem.Name,
                    _appState.AppDataPath));
            }
            else
            {
                await Run(new RdbFsAddCommand(_loggerFactory.CreateLogger<RdbFsAddCommand>(), commandHelper,
                    physicalDrives, path, fileSystem.Path, fileSystem.DosType, fileSystem.Name, fileSystem.Version,
                    fileSystem.Revision));
            }

            var fileSystemNumber = await ReadRdbFileSystemNumberAsync(commandHelper, physicalDrives, path,
                fileSystem.DosType, token);
            await Run(new RdbFsUpdateCommand(_loggerFactory.CreateLogger<RdbFsUpdateCommand>(), commandHelper,
                physicalDrives, path, fileSystemNumber, string.Empty, fileSystem.Name, string.Empty));
        }

        // add file systems used by new partitions, which are not already in rigid disk block
        if (isRdb)
        {
            var existingDosTypes = await ReadRdbDosTypesAsync(commandHelper, physicalDrives, path, token);
            foreach (var dosType in rdbDosTypes)
            {
                if (!existingDosTypes.Contains(dosType))
                {
                    // pfs3aio and fast file system are added from media, other file systems must be added or
                    // imported to rigid disk block by plan
                    var isPfs3 = dosType is "PFS3" or "PDS3";
                    if (!isPfs3 && !ViewModels.PartitionFileSystems.IsFastFileSystem(dosType))
                    {
                        throw new ImagingException(
                            $"File system with DOS type '{dosType}' not found in Rigid Disk Block. Import a file system with DOS type '{dosType}'");
                    }

                    var fileSystemPath = await PrepareRdbFileSystemAsync(commandHelper,
                        isPfs3 ? plan.Pfs3FileSystemPath : plan.FastFileSystemPath,
                        isPfs3 ? "pfs3aio" : "FastFileSystem", token);
                    await Run(new RdbFsAddCommand(_loggerFactory.CreateLogger<RdbFsAddCommand>(), commandHelper,
                        physicalDrives, path, fileSystemPath, dosType, string.Empty, null, null));
                }
                else
                {
                    stepsExecuted++;
                    ReportStep();
                }
            }
        }

        // add partitions ordered by start offset
        foreach (var partition in plan.AddPartitions.OrderBy(x => x.StartOffset))
        {
            var startSector = partition.StartOffset / 512;
            var endSector = (partition.StartOffset + partition.Size) / 512 - 1;
            await Run(plan.TableType switch
            {
                PartitionTableType.MasterBootRecord => new MbrPartAddCommand(
                    _loggerFactory.CreateLogger<MbrPartAddCommand>(), commandHelper, physicalDrives, path,
                    partition.IsPiStorm
                        ? nameof(MbrPartType.PiStormRdb)
                        : string.IsNullOrWhiteSpace(partition.PartitionType)
                            ? GetMbrPartType(partition.FileSystem)
                            : partition.PartitionType,
                    new Size(partition.Size, Unit.Bytes), startSector,
                    endSector, partition.Bootable),
                PartitionTableType.GuidPartitionTable => new GptPartAddCommand(
                    _loggerFactory.CreateLogger<GptPartAddCommand>(), commandHelper, physicalDrives, path,
                    string.IsNullOrWhiteSpace(partition.PartitionType)
                        ? GetGptPartType(partition.FileSystem).ToString()
                        : partition.PartitionType, partition.Label,
                    new Size(partition.Size, Unit.Bytes), startSector, endSector),
                PartitionTableType.RigidDiskBlock => new RdbPartAddCommand(
                    _loggerFactory.CreateLogger<RdbPartAddCommand>(), commandHelper, physicalDrives, path,
                    partition.DeviceName, GetDosType(partition.FileSystem), new Size(partition.Size, Unit.Bytes),
                    null, null, null, 0x1fe00, null, false, partition.Bootable, null, 512, plan.UseExperimental,
                    (uint)(partition.StartOffset / plan.CylinderSize)),
                _ => throw new ImagingException($"Unsupported partition table '{plan.TableType}'")
            });
        }

        // format new and existing partitions using partition numbers read from partition table after changes
        var partitionNumbers = await ReadPartitionNumbersAsync(commandHelper, physicalDrives, path,
            plan.TableType, token);
        foreach (var partition in plan.AddPartitions.Concat(plan.FormatPartitions).OrderBy(x => x.StartOffset))
        {
            // PiStorm partitions contain a rigid disk block partitioned by another plan and are not formatted.
            // partitions with partition types without a supported file system are only added
            if (partition.IsPiStorm || !partition.Format)
            {
                stepsExecuted++;
                ReportStep();
                continue;
            }

            if (!partitionNumbers.TryGetValue(partition.StartOffset, out var partitionNumber))
            {
                throw new ImagingException(
                    $"Partition at offset {partition.StartOffset} not found in partition table after partitioning");
            }

            await Run(plan.TableType switch
            {
                PartitionTableType.MasterBootRecord => new MbrPartFormatCommand(
                    _loggerFactory.CreateLogger<MbrPartFormatCommand>(), commandHelper, physicalDrives, path,
                    partitionNumber, partition.Label, GetMbrPartType(partition.FileSystem)),
                PartitionTableType.GuidPartitionTable => new GptPartFormatCommand(
                    _loggerFactory.CreateLogger<GptPartFormatCommand>(), commandHelper, physicalDrives, path,
                    partitionNumber, GetGptPartType(partition.FileSystem), partition.Label),
                PartitionTableType.RigidDiskBlock => new RdbPartFormatCommand(
                    _loggerFactory.CreateLogger<RdbPartFormatCommand>(), commandHelper, physicalDrives, path,
                    partitionNumber, partition.Label, false, string.Empty, string.Empty),
                _ => throw new ImagingException($"Unsupported partition table '{plan.TableType}'")
            });
        }

        if (physicalDrives.Any(x => plan.Path.StartsWith(x.Path, StringComparison.OrdinalIgnoreCase)))
            await commandHelper.RescanPhysicalDrives();

        stepsExecuted = steps;
        ReportStep();
    }

    public async Task ExportRdbFileSystemAsync(string path, bool byteswap, int fileSystemNumber, string outputPath,
        CancellationToken cancellationToken)
    {
        var physicalDrives = await GetPhysicalDrivesAsync();
        using var commandHelper = CreateCommandHelper();
        ThrowIfFaulted(await new RdbFsExportCommand(_loggerFactory.CreateLogger<RdbFsExportCommand>(), commandHelper,
                physicalDrives, string.Concat(byteswap ? "+bs:" : string.Empty, path), fileSystemNumber, outputPath)
            .Execute(cancellationToken));
    }

    public async Task<RdbFileSystemInfo?> FindRdbFileSystemAsync(string mediaPath, string fileSystemName)
    {
        using var commandHelper = CreateCommandHelper();
        var findResult = await AmigaFileSystemHelper.FindFileSystemInMedia(commandHelper, mediaPath, fileSystemName);
        if (findResult.IsFaulted)
            throw new ImagingException(findResult.Error?.Message ?? $"Failed to read '{mediaPath}'");

        var (name, data) = findResult.Value;
        if (data.Length == 0)
            return null;

        var version = Hst.Amiga.VersionStrings.VersionStringReader.Read(data);
        return new RdbFileSystemInfo(name, data.Length, ViewModels.RdbFileSystemEntry.FormatVersion(version));
    }

    /// <summary>
    /// Throw, if file system is not found in media. Import command adds an empty file system, if it's not found.
    /// Media from urls are downloaded by import command and not checked.
    /// </summary>
    private static async Task ThrowIfFileSystemNotInMediaAsync(ICommandHelper commandHelper, string mediaPath,
        string fileSystemName)
    {
        if (mediaPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return;

        var findResult = await AmigaFileSystemHelper.FindFileSystemInMedia(commandHelper, mediaPath, fileSystemName);
        if (findResult.IsFaulted)
            throw new ImagingException(findResult.Error?.Message ?? $"Failed to read '{mediaPath}'");
        if (findResult.Value.Item2.Length == 0)
            throw new ImagingException($"File system '{fileSystemName}' not found in '{mediaPath}'");
    }

    /// <summary>
    /// Read number of file system with dos type in rigid disk block.
    /// </summary>
    private async Task<int> ReadRdbFileSystemNumberAsync(ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, string dosType, CancellationToken token)
    {
        var diskInfo = await ReadDiskInfoAsync(commandHelper, physicalDrives, path, token);
        var fileSystems = (diskInfo?.RigidDiskBlock?.FileSystemHeaderBlocks ?? []).ToList();
        var index = fileSystems.FindIndex(x => string.Equals(x.DosTypeFormatted.Replace("\\", string.Empty),
            dosType, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            throw new ImagingException($"File system with DOS type '{dosType}' not found in Rigid Disk Block after adding it");
        return index + 1;
    }

    private static string GetMbrPartType(string fileSystem) => fileSystem switch
    {
        "fat32" => MbrPartType.Fat32Lba.ToString(),
        "exfat" => MbrPartType.ExFat.ToString(),
        "ntfs" => MbrPartType.Ntfs.ToString(),
        _ => throw new ImagingException($"Unsupported Master Boot Record file system '{fileSystem}'")
    };

    private static GptPartType GetGptPartType(string fileSystem) => fileSystem switch
    {
        "fat32" => GptPartType.Fat32,
        "exfat" => GptPartType.ExFat,
        "ntfs" => GptPartType.Ntfs,
        _ => throw new ImagingException($"Unsupported Guid Partition Table file system '{fileSystem}'")
    };

    private static string GetDosType(string fileSystem) => fileSystem.ToUpperInvariant();

    /// <summary>
    /// Erase existing partition tables by writing zeros to first 10MB of disk, same as format.
    /// </summary>
    private static async Task ErasePartitionTablesAsync(ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, CancellationToken token)
    {
        var mediaResult = await commandHelper.GetWritableMedia(physicalDrives, path);
        if (mediaResult.IsFaulted)
            throw new ImagingException(mediaResult.Error?.Message ?? $"Failed to open '{path}'");

        using var media = mediaResult.Value;
        await using var stream = media.Stream;
        var eraseSize = Math.Min(media.Size, 10L * 1024 * 1024) / 512 * 512;
        using var streamCopier = new StreamCopier();
        await streamCopier.Copy(token, new System.IO.MemoryStream(new byte[eraseSize]), stream, eraseSize, 0, 0);
    }

    /// <summary>
    /// Erase sectors 1 to 15, which rigid disk block can be written to, keeping master boot record in sector 0.
    /// Prevents an existing rigid disk block in another sector from being found before the new rigid disk block.
    /// First 16 sectors are read and written as a whole to keep writes aligned for physical disks.
    /// </summary>
    private static async Task EraseRigidDiskBlockSectorsAsync(ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, CancellationToken token)
    {
        var mediaResult = await commandHelper.GetWritableMedia(physicalDrives, path);
        if (mediaResult.IsFaulted)
            throw new ImagingException(mediaResult.Error?.Message ?? $"Failed to open '{path}'");

        using var media = mediaResult.Value;
        await using var stream = media.Stream;
        var sectors = new byte[16 * 512];
        stream.Position = 0;
        await stream.ReadExactlyAsync(sectors, token);
        Array.Clear(sectors, 512, sectors.Length - 512);
        stream.Position = 0;
        await stream.WriteAsync(sectors, token);
        await stream.FlushAsync(token);
    }

    private async Task<DiskInfo?> ReadDiskInfoAsync(ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, CancellationToken token)
    {
        var infoCommand = new InfoCommand(_loggerFactory.CreateLogger<InfoCommand>(), commandHelper, physicalDrives,
            path, false);
        DiskInfo? diskInfo = null;
        infoCommand.DiskInfoRead += (_, args) => diskInfo = args.MediaInfo?.DiskInfo;
        ThrowIfFaulted(await infoCommand.Execute(token));
        return diskInfo;
    }

    private async Task<HashSet<string>> ReadRdbDosTypesAsync(ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, CancellationToken token)
    {
        var diskInfo = await ReadDiskInfoAsync(commandHelper, physicalDrives, path, token);
        return (diskInfo?.RigidDiskBlock?.FileSystemHeaderBlocks ?? [])
            .Select(x => x.DosTypeFormatted.Replace("\\", string.Empty))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Read partition numbers by start offset from partition table.
    /// </summary>
    private async Task<Dictionary<long, int>> ReadPartitionNumbersAsync(ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, PartitionTableType tableType,
        CancellationToken token)
    {
        var diskInfo = await ReadDiskInfoAsync(commandHelper, physicalDrives, path, token);
        var partitionTablePart = tableType switch
        {
            PartitionTableType.MasterBootRecord => diskInfo?.MbrPartitionTablePart,
            PartitionTableType.GuidPartitionTable => diskInfo?.GptPartitionTablePart,
            PartitionTableType.RigidDiskBlock => diskInfo?.RdbPartitionTablePart,
            _ => null
        };

        var partitionNumbers = new Dictionary<long, int>();
        foreach (var part in (partitionTablePart?.Parts ?? [])
                 .Where(x => x.PartType == PartType.Partition && x.PartitionNumber.HasValue))
        {
            partitionNumbers.TryAdd(part.StartOffset, part.PartitionNumber!.Value);
        }

        return partitionNumbers;
    }

    /// <summary>
    /// Prepare file system file for rigid disk block by downloading it, if path is an url, and finding file system
    /// in media like lha, adf or iso.
    /// </summary>
    private async Task<string> PrepareRdbFileSystemAsync(ICommandHelper commandHelper, string? mediaPath,
        string fileSystemName, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(mediaPath))
            throw new ImagingException($"Path to media with file system '{fileSystemName}' is required");

        if (mediaPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            mediaPath = await FileSystemHelper.DownloadFile(mediaPath, _appState.AppDataPath,
                System.IO.Path.GetFileName(new Uri(mediaPath).LocalPath));

        var findResult = await AmigaFileSystemHelper.FindFileSystemInMedia(commandHelper, mediaPath, fileSystemName);
        if (findResult.IsFaulted)
            throw new ImagingException(findResult.Error?.Message ?? $"Failed to read '{mediaPath}'");

        var (name, data) = findResult.Value;
        if (data.Length == 0)
            throw new ImagingException($"File system '{fileSystemName}' not found in '{mediaPath}'");

        var outputPath = System.IO.Path.Combine(_appState.AppDataPath, "filesystems");
        System.IO.Directory.CreateDirectory(outputPath);
        var fileSystemPath = System.IO.Path.Combine(outputPath,
            string.IsNullOrWhiteSpace(name) ? fileSystemName : System.IO.Path.GetFileName(name));
        await System.IO.File.WriteAllBytesAsync(fileSystemPath, data, token);

        return fileSystemPath;
    }

    private async Task<List<IPhysicalDrive>> GetPhysicalDrivesAsync()
    {
        var physicalDriveManager = CreatePhysicalDriveManager();
        return (await physicalDriveManager.GetPhysicalDrives(_appState.Settings.AllPhysicalDrives)).ToList();
    }

    private IPhysicalDriveManager CreatePhysicalDriveManager()
    {
        if (Hst.Core.OperatingSystem.IsWindows())
            return new WindowsPhysicalDriveManager(_loggerFactory.CreateLogger<WindowsPhysicalDriveManager>());
        if (Hst.Core.OperatingSystem.IsMacOs())
            return new MacOsPhysicalDriveManager(_loggerFactory.CreateLogger<MacOsPhysicalDriveManager>());
        if (Hst.Core.OperatingSystem.IsLinux())
            return new LinuxPhysicalDriveManager(_loggerFactory.CreateLogger<LinuxPhysicalDriveManager>());
        return new StaticPhysicalDriveManager([]);
    }

    private CommandHelper CreateCommandHelper() =>
        new(_loggerFactory.CreateLogger<ICommandHelper>(),
            _appState.IsAdministrator,
            _appState.Settings.SparseFiles,
            _appState.Settings.UseCache,
            _appState.Settings.CacheType);

    /// <summary>
    /// Get stage for writing data to cache, if use cache is enabled and destination is cached.
    /// Command helper only caches physical drives and vhd image files, other image files are written directly.
    /// </summary>
    private string? GetCacheStage(string destinationPath, IEnumerable<IPhysicalDrive> physicalDrives) =>
        _appState.Settings.UseCache && IsCachedMedia(destinationPath, physicalDrives)
            ? WritingToCacheStage
            : null;

    private static bool IsCachedMedia(string path, IEnumerable<IPhysicalDrive> physicalDrives)
    {
        // vhd image file or part of it, e.g. "disk.vhd" or "disk.vhd\mbr\1"
        if (path.Split('\\', '/').Any(x => x.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // physical drive or part of it, e.g. "\\.\PhysicalDrive2" or "\\.\PhysicalDrive2\mbr\1"
        return physicalDrives.Any(x =>
            path.Equals(x.Path, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(x.Path + "\\", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(x.Path + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static ProgressModel MapProgress(string title, DataProcessedEventArgs args, string? stage) => new()
    {
        Title = title,
        Stage = stage,
        IsComplete = false,
        PercentComplete = args.PercentComplete,
        BytesPerSecond = args.BytesPerSecond,
        BytesProcessed = args.BytesProcessed,
        BytesRemaining = args.BytesRemaining,
        BytesTotal = args.BytesTotal,
        MillisecondsElapsed = args.PercentComplete > 0 ? (long)args.TimeElapsed.TotalMilliseconds : null,
        MillisecondsRemaining = args.PercentComplete > 0 ? (long)args.TimeRemaining.TotalMilliseconds : null,
        MillisecondsTotal = args.PercentComplete > 0 ? (long)args.TimeTotal.TotalMilliseconds : null
    };

    /// <summary>
    /// Track cache used by task. Writes to physical disks and vhd image files are cached, when use cache is enabled,
    /// and flushed to the destination when it's closed after data is processed.
    /// Reports progress of flushing cache to destination, as that is when data is actually written.
    /// Discards cached writes when cancelled, so cancel doesn't flush cached data to the destination, which can
    /// take minutes for physical disks.
    /// </summary>
    private static CacheTracker TrackCache(IProgress<ProgressModel> progress, CancellationToken cancellationToken)
    {
        // command gets its own token cancelled after cached writes are discarded. cancelling a token runs callbacks
        // in reverse order, so discarding on the same token could run after the command has seen the cancel and
        // flushed the cache when closing the destination
        var commandCancellationTokenSource = new CancellationTokenSource();
        var discardOnCancel = cancellationToken.Register(() =>
        {
            CacheHelper.DiscardCachedWrites();
            commandCancellationTokenSource.Cancel();
        });
        CommandLogger.Instance.DataProcessed += OnDataProcessed;

        return new CacheTracker(commandCancellationTokenSource.Token, Disposable.Create(() =>
        {
            CommandLogger.Instance.DataProcessed -= OnDataProcessed;
            discardOnCancel.Dispose();
            commandCancellationTokenSource.Dispose();
        }));

        void OnDataProcessed(object? sender, DataProcessedEventArgs args)
        {
            var progressModel = MapProgress("Flushing cache", args, WritingCachedDataStage);
            progressModel.IsCacheFlush = true;
            progress.Report(progressModel);
        }
    }

    /// <summary>
    /// Cache tracked for a task with cancellation token to use for commands.
    /// </summary>
    private sealed class CacheTracker(CancellationToken token, IDisposable disposable) : IDisposable
    {
        public CancellationToken Token { get; } = token;

        public void Dispose() => disposable.Dispose();
    }

    private static void ThrowIfFaulted(Hst.Core.Result result)
    {
        if (result.IsFaulted)
            throw new ImagingException(result.Error?.Message ?? "Command failed without an error message");
    }
}
