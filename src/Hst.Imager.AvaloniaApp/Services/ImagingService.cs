using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.Core;
using Hst.Imager.Core.Commands;
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
