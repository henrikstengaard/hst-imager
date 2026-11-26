using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Opened media for a recovery operation.
/// </summary>
public class RecoveryContext
{
    /// <summary>
    /// Media opened, PiStorm RDB media if path contains a PiStorm RDB partition.
    /// </summary>
    public Media Media { get; init; }

    /// <summary>
    /// Stream with disk data.
    /// </summary>
    public Stream Stream { get; init; }

    /// <summary>
    /// Path to media file or physical drive.
    /// </summary>
    public string MediaPath { get; init; }

    /// <summary>
    /// Path components in media after media path, e.g. "rdb", "dh0".
    /// </summary>
    public string[] PathComponents { get; init; }
}

/// <summary>
/// Base for recovery commands opening media, writing log file and reporting results.
/// </summary>
public abstract class RecoveryCommandBase : CommandBase, IRecoveryCommand
{
    protected readonly ILogger Logger;
    protected readonly ICommandHelper CommandHelper;
    protected readonly IEnumerable<IPhysicalDrive> PhysicalDrives;
    protected readonly string Path;
    private readonly string _logPath;
    private RecoveryLogWriter _log;
    private readonly Stopwatch _stopwatch = new();
    private DateTime _lastProgress = DateTime.MinValue;

    protected RecoveryCommandBase(ILogger logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, string logPath)
    {
        Logger = logger;
        CommandHelper = commandHelper;
        PhysicalDrives = physicalDrives;
        Path = path;
        _logPath = logPath;
    }

    public event EventHandler<RecoveryReportReadEventArgs> RecoveryReportRead;

    /// <summary>
    /// Append to log file instead of creating a new log file, e.g. when run as part of full repair.
    /// </summary>
    internal bool AppendLog { get; set; }

    /// <summary>
    /// Raised for each line written to log file.
    /// </summary>
    public event EventHandler<string> LogLineWritten;

    /// <summary>
    /// Recovery operation done by command.
    /// </summary>
    protected abstract RecoveryOperation Operation { get; }

    /// <summary>
    /// Open media writable.
    /// </summary>
    protected virtual bool Writable => false;

    /// <summary>
    /// Options written to log header.
    /// </summary>
    protected virtual IEnumerable<(string Name, object Value)> LogOptions => [];

    /// <summary>
    /// Run recovery operation on opened media and add issues found to report.
    /// </summary>
    protected abstract Task<Result> Recover(RecoveryContext context, RecoveryReport report,
        CancellationToken token);

    public override async Task<Result> Execute(CancellationToken token)
    {
        var mediaResult = CommandHelper.ResolveMedia(Path);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }

        var resolvedMedia = mediaResult.Value;

        OnDebugMessage($"Opening '{resolvedMedia.MediaPath}' as {(Writable ? "writable" : "readable")}");

        var openResult = Writable
            ? await CommandHelper.GetWritableMedia(PhysicalDrives, resolvedMedia.MediaPath, resolvedMedia.Modifiers)
            : await CommandHelper.GetReadableMedia(PhysicalDrives, resolvedMedia.MediaPath, resolvedMedia.Modifiers);
        if (openResult.IsFaulted)
        {
            return new Result(openResult.Error);
        }

        using var openedMedia = openResult.Value;

        var logPath = string.IsNullOrWhiteSpace(_logPath)
            ? RecoveryLogPath.GetDefault(resolvedMedia.MediaPath, openedMedia.IsPhysicalDrive, Operation)
            : _logPath;

        try
        {
            _log = new RecoveryLogWriter(logPath, AppendLog);
            _log.LineWritten += (_, line) => LogLineWritten?.Invoke(this, line);
        }
        catch (Exception e)
        {
            return new Result(new Error($"Failed to create log file '{logPath}': {e.Message}"));
        }

        var report = new RecoveryReport
        {
            Operation = Operation,
            Target = Path,
            LogPath = _log.Path
        };

        try
        {
            _log.WriteHeader(Operation, Path, LogOptions.ToArray());

            OnInformationMessage($"Writing log to '{_log.Path}'");

            var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(openedMedia,
                resolvedMedia.FileSystemPath ?? string.Empty, resolvedMedia.DirectorySeparatorChar);
            if (piStormRdbMediaResult.HasPiStormRdb)
            {
                OnDebugMessage($"Using PiStorm RDB partition '{piStormRdbMediaResult.Media.Path}'");
            }

            var media = piStormRdbMediaResult.Media;
            var pathComponents = (piStormRdbMediaResult.FileSystemPath ?? string.Empty)
                .Split(resolvedMedia.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

            var context = new RecoveryContext
            {
                Media = media,
                Stream = MediaHelper.GetStreamFromMedia(media),
                MediaPath = resolvedMedia.MediaPath,
                PathComponents = pathComponents
            };

            _stopwatch.Start();

            Result result;
            try
            {
                result = await Recover(context, report, token);
            }
            catch (OperationCanceledException)
            {
                report.Aborted = true;
                result = new Result(new Error("Recovery operation was cancelled"));
            }
            catch (Exception e)
            {
                Logger?.LogError(e, "Recovery operation failed");
                _log.Error(e.ToString());
                result = new Result(new Error($"Recovery operation failed: {e.Message}"));
            }

            _stopwatch.Stop();

            if (result.IsFaulted)
            {
                _log.Error(result.Error.Message);
            }

            _log.WriteReport(report);

            OnRecoveryReportRead(report);

            return result;
        }
        finally
        {
            _log.Dispose();
            _log = null;
        }
    }

    protected override void OnDebugMessage(string message)
    {
        _log?.Debug(message);
        base.OnDebugMessage(message);
    }

    protected override void OnWarningMessage(string message)
    {
        _log?.Warning(message);
        base.OnWarningMessage(message);
    }

    public override void OnInformationMessage(string message)
    {
        _log?.Information(message);
        base.OnInformationMessage(message);
    }

    /// <summary>
    /// Write message from recovery engine to log file only.
    /// </summary>
    protected void LogEngineMessage(string message)
    {
        _log?.Write("ENG", message);
    }

    /// <summary>
    /// Report progress. Progress is throttled to avoid flooding front-ends.
    /// </summary>
    protected void ReportProgress(long processed, long total)
    {
        var now = DateTime.UtcNow;
        if (processed < total && (now - _lastProgress).TotalMilliseconds < 200)
        {
            return;
        }

        _lastProgress = now;

        var elapsed = _stopwatch.Elapsed;
        if (total <= 0)
        {
            OnDataProcessed(true, 0, processed, 0, 0, elapsed, TimeSpan.Zero, elapsed, 0);
            return;
        }

        processed = Math.Min(processed, total);
        var percent = (double)100 / total * processed;
        var bytesPerSecond = elapsed.TotalSeconds > 0 ? (long)(processed / elapsed.TotalSeconds) : 0;
        var remaining = bytesPerSecond > 0
            ? TimeSpan.FromSeconds((double)(total - processed) / bytesPerSecond)
            : TimeSpan.Zero;
        OnDataProcessed(false, percent, processed, total - processed, total, elapsed, remaining,
            elapsed + remaining, bytesPerSecond);
    }

    /// <summary>
    /// Report indeterminate progress.
    /// </summary>
    protected void ReportIndeterminate() => ReportProgress(0, 0);

    protected virtual void OnRecoveryReportRead(RecoveryReport report)
    {
        RecoveryReportRead?.Invoke(this, new RecoveryReportReadEventArgs(report));
    }

    /// <summary>
    /// Add recovery engine message to log and report.
    /// </summary>
    protected void AddMessage(RecoveryReport report, RecoveryIssueLevel level, string message, long? block = null,
        string path = null, bool isFixed = false)
    {
        report.AddIssue(level, message, block, path, isFixed);
        switch (level)
        {
            case RecoveryIssueLevel.Error:
                _log?.Error(message);
                break;
            case RecoveryIssueLevel.Warning:
                _log?.Warning(message);
                break;
            default:
                _log?.Information(message);
                break;
        }
    }
}
