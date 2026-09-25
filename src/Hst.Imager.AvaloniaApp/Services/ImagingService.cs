using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.PhysicalDrives;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.AvaloniaApp.Services;

public class ImagingService : IImagingService
{
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
        progress.Report(new ProgressModel { Title = "Reading", PercentComplete = 0 });

        var physicalDrives = await GetPhysicalDrivesAsync();
        using var commandHelper = CreateCommandHelper();

        var readPath = string.Concat(byteswap ? "+bs:" : string.Empty, sourcePath);
        var cmd = new ReadCommand(
            _loggerFactory.CreateLogger<ReadCommand>(), commandHelper, physicalDrives,
            readPath, destinationPath,
            new Size(size, Unit.Bytes), _appState.Settings.Retries,
            _appState.Settings.Verify, _appState.Settings.Force, startOffset,
            _appState.Settings.SparseFiles);

        cmd.DataProcessed += (_, args) => progress.Report(MapProgress("Reading", args));
        var result = await cmd.Execute(cancellationToken);
        ThrowIfFaulted(result);
    }

    public async Task WriteAsync(string sourcePath, string destinationPath, long startOffset, long size, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken)
    {
        progress.Report(new ProgressModel { Title = "Writing", PercentComplete = 0 });

        var physicalDrives = await GetPhysicalDrivesAsync();
        using var commandHelper = CreateCommandHelper();

        var writePath = string.Concat(byteswap ? "+bs:" : string.Empty, sourcePath);
        var cmd = new WriteCommand(
            _loggerFactory.CreateLogger<WriteCommand>(), commandHelper, physicalDrives,
            writePath, destinationPath,
            new Size(size, Unit.Bytes), _appState.Settings.Retries,
            _appState.Settings.Verify, _appState.Settings.Force, _appState.Settings.SkipUnusedSectors,
            startOffset);

        cmd.DataProcessed += (_, args) => progress.Report(MapProgress("Writing", args));
        var result = await cmd.Execute(cancellationToken);
        ThrowIfFaulted(result);
    }

    public async Task CompareAsync(string sourcePath, long sourceStartOffset, string destinationPath, long destinationStartOffset,
        long size, bool byteswap, IProgress<ProgressModel> progress, CancellationToken cancellationToken)
    {
        progress.Report(new ProgressModel { Title = "Comparing", PercentComplete = 0 });

        var physicalDrives = await GetPhysicalDrivesAsync();
        using var commandHelper = CreateCommandHelper();

        var cmpPath = string.Concat(byteswap ? "+bs:" : string.Empty, sourcePath);
        var cmd = new CompareCommand(
            _loggerFactory.CreateLogger<CompareCommand>(), commandHelper, physicalDrives,
            cmpPath, sourceStartOffset, destinationPath, destinationStartOffset,
            new Size(size, Unit.Bytes), _appState.Settings.Retries,
            _appState.Settings.Force, _appState.Settings.SkipUnusedSectors);

        cmd.DataProcessed += (_, args) => progress.Report(MapProgress("Comparing", args));
        var result = await cmd.Execute(cancellationToken);
        ThrowIfFaulted(result);
    }

    public async Task TransferAsync(string sourcePath, long srcStartOffset, string destinationPath, long destStartOffset,
        long size, bool byteswap, IProgress<ProgressModel> progress, CancellationToken cancellationToken)
    {
        progress.Report(new ProgressModel { Title = "Transferring", PercentComplete = 0 });
        using var commandHelper = CreateCommandHelper();

        var transferPath = string.Concat(byteswap ? "+bs:" : string.Empty, sourcePath);
        var cmd = new TransferCommand(commandHelper, transferPath, destinationPath,
            new Size(size, Unit.Bytes), false, srcStartOffset, destStartOffset,
            _appState.Settings.SparseFiles);

        cmd.DataProcessed += (_, args) => progress.Report(MapProgress("Transferring", args));
        var result = await cmd.Execute(cancellationToken);
        ThrowIfFaulted(result);
    }

    public async Task BlankAsync(string path, long size, bool compatibleSize,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken)
    {
        progress.Report(new ProgressModel { Title = "Creating blank image", PercentComplete = 50 });
        using var commandHelper = CreateCommandHelper();

        var cmd = new BlankCommand(_loggerFactory.CreateLogger<BlankCommand>(), commandHelper,
            path, new Size(size, Unit.Bytes), compatibleSize);

        var result = await cmd.Execute(cancellationToken);
        ThrowIfFaulted(result);
    }

    public async Task OptimizeAsync(string path, long size, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken)
    {
        progress.Report(new ProgressModel { Title = "Optimizing", PercentComplete = 50 });
        using var commandHelper = CreateCommandHelper();

        var optimizePath = string.Concat(byteswap ? "+bs:" : string.Empty, path);
        var cmd = new OptimizeCommand(commandHelper, optimizePath, new Size(size, Unit.Bytes), PartitionTable.None);

        var result = await cmd.Execute(cancellationToken);
        ThrowIfFaulted(result);
    }

    public async Task FormatAsync(string path, FormatType formatType, string fileSystem, string? fileSystemPath,
        long size, long maxPartitionSize, bool useExperimental, bool kickstart31, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken)
    {
        progress.Report(new ProgressModel { Title = "Formatting", PercentComplete = 0 });

        var physicalDrives = await GetPhysicalDrivesAsync();
        using var commandHelper = CreateCommandHelper();

        var formatPath = string.Concat(byteswap ? "+bs:" : string.Empty, path);
        var cmd = new FormatCommand(
            _loggerFactory.CreateLogger<FormatCommand>(), _loggerFactory, commandHelper, physicalDrives,
            formatPath, formatType, fileSystem, fileSystemPath, _appState.AppDataPath,
            new Size(size, Unit.Bytes), new Size(maxPartitionSize, Unit.Bytes),
            useExperimental, kickstart31);

        cmd.DataProcessed += (_, args) => progress.Report(MapProgress("Formatting", args));
        var result = await cmd.Execute(cancellationToken);
        ThrowIfFaulted(result);
    }

    private async System.Threading.Tasks.Task<System.Collections.Generic.List<IPhysicalDrive>> GetPhysicalDrivesAsync()
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

    private static ProgressModel MapProgress(string title, DataProcessedEventArgs args) => new()
    {
        Title = title,
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

    private static void ThrowIfFaulted(Hst.Core.Result result)
    {
        if (result.IsFaulted)
            throw new ImagingException(result.Error?.Message ?? "Command failed without an error message");
    }
}
