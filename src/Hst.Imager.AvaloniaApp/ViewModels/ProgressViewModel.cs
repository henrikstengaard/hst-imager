using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Avalonia.Threading;
using System.Threading;
using System.Threading.Tasks;
using Humanizer;
using Hst.Imager.AvaloniaApp.Models;
using ReactiveUI;
using ReactiveUI.Reactive;

namespace Hst.Imager.AvaloniaApp.ViewModels;

/// <summary>
/// App wide progress for running tasks shown as a modal overlay in main window.
/// </summary>
public class ProgressViewModel : ViewModelBase
{
    private bool _isVisible;
    private bool _isRunning;
    private bool _isComplete;
    private double _percentComplete;
    private bool _isIndeterminate;
    private string _title = string.Empty;
    private string _speedText = string.Empty;
    private string _etaText = string.Empty;
    private string _bytesText = string.Empty;
    private bool _hasError;
    private string _errorMessage = string.Empty;
    private string _resultMessage = string.Empty;
    private CancellationTokenSource? _cts;

    public ProgressViewModel()
    {
        CancelCommand = ReactiveCommand.Create(Cancel,
            this.WhenAnyValue(x => x.IsRunning, x => x.IsCancelling, (running, cancelling) => running && !cancelling));
        OkCommand = ReactiveCommand.Create(() => { IsVisible = false; }, this.WhenAnyValue(x => x.IsComplete));
    }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> OkCommand { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            this.RaiseAndSetIfChanged(ref _isRunning, value);
            this.RaisePropertyChanged(nameof(IsFinishing));
            this.RaisePropertyChanged(nameof(IsProgressBarIndeterminate));
            this.RaisePropertyChanged(nameof(IsPercentVisible));
        }
    }

    private bool _isCancelling;

    /// <summary>
    /// Cancel is requested and task is stopping.
    /// </summary>
    public bool IsCancelling
    {
        get => _isCancelling;
        private set => this.RaiseAndSetIfChanged(ref _isCancelling, value);
    }

    /// <summary>
    /// All data is processed, but task is still finishing like flushing and closing destination.
    /// </summary>
    public bool IsFinishing => _isRunning && _percentComplete >= 100;

    public bool IsComplete
    {
        get => _isComplete;
        set
        {
            this.RaiseAndSetIfChanged(ref _isComplete, value);
            this.RaisePropertyChanged(nameof(IsSuccess));
            this.RaisePropertyChanged(nameof(IsResultOk));
        }
    }

    public bool IsSuccess => _isComplete && !_hasError;

    public double PercentComplete
    {
        get => _percentComplete;
        set
        {
            this.RaiseAndSetIfChanged(ref _percentComplete, value);
            this.RaisePropertyChanged(nameof(PercentText));
            this.RaisePropertyChanged(nameof(IsFinishing));
            this.RaisePropertyChanged(nameof(IsProgressBarIndeterminate));
            this.RaisePropertyChanged(nameof(IsPercentVisible));
        }
    }

    public string PercentText => $"{_percentComplete:0.0} %";

    /// <summary>
    /// Total size is unknown, so only bytes processed and time elapsed are shown, e.g. writing gzip compressed image.
    /// </summary>
    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            this.RaiseAndSetIfChanged(ref _isIndeterminate, value);
            this.RaisePropertyChanged(nameof(IsProgressBarIndeterminate));
            this.RaisePropertyChanged(nameof(IsPercentVisible));
        }
    }

    /// <summary>
    /// Progress bar is indeterminate, when task is finishing or total size is unknown.
    /// </summary>
    public bool IsProgressBarIndeterminate => IsFinishing || _isIndeterminate;

    /// <summary>
    /// Percent is shown, when task is not finishing and total size is known.
    /// </summary>
    public bool IsPercentVisible => !IsFinishing && !_isIndeterminate;

    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    private string _stage = string.Empty;
    private bool _isCacheFlush;

    /// <summary>
    /// Current stage of task, e.g. writing cached data to destination. Empty for main stage.
    /// </summary>
    public string Stage
    {
        get => _stage;
        set
        {
            this.RaiseAndSetIfChanged(ref _stage, value);
            this.RaisePropertyChanged(nameof(HasStage));
        }
    }

    public bool HasStage => !string.IsNullOrEmpty(_stage);

    public string SpeedText
    {
        get => _speedText;
        set => this.RaiseAndSetIfChanged(ref _speedText, value);
    }

    public string EtaText
    {
        get => _etaText;
        set => this.RaiseAndSetIfChanged(ref _etaText, value);
    }

    public string BytesText
    {
        get => _bytesText;
        set => this.RaiseAndSetIfChanged(ref _bytesText, value);
    }

    public bool HasError
    {
        get => _hasError;
        set
        {
            this.RaiseAndSetIfChanged(ref _hasError, value);
            this.RaisePropertyChanged(nameof(IsSuccess));
            this.RaisePropertyChanged(nameof(IsResultOk));
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    /// <summary>
    /// Message shown when task completed successfully, e.g. summary of result. Empty for no message.
    /// </summary>
    public string ResultMessage
    {
        get => _resultMessage;
        set
        {
            this.RaiseAndSetIfChanged(ref _resultMessage, value);
            this.RaisePropertyChanged(nameof(HasResultMessage));
        }
    }

    public bool HasResultMessage => !string.IsNullOrEmpty(_resultMessage);

    private string _resultStatus = string.Empty;
    private bool _isResultWarning;

    /// <summary>
    /// Total status shown when task completed successfully, e.g. "Partition table valid".
    /// Completed successfully is shown, if empty.
    /// </summary>
    public string ResultStatus
    {
        get => _resultStatus;
        private set
        {
            this.RaiseAndSetIfChanged(ref _resultStatus, value);
            this.RaisePropertyChanged(nameof(CompletedText));
        }
    }

    /// <summary>
    /// Text shown when task completed successfully, result status or completed successfully.
    /// </summary>
    public string CompletedText => string.IsNullOrEmpty(_resultStatus) ? "Completed successfully" : _resultStatus;

    /// <summary>
    /// Result status is a warning, e.g. errors found or not repaired.
    /// </summary>
    public bool IsResultWarning
    {
        get => _isResultWarning;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isResultWarning, value);
            this.RaisePropertyChanged(nameof(IsResultOk));
        }
    }

    public bool IsResultOk => IsSuccess && !_isResultWarning;

    /// <summary>
    /// Maximum number of detail lines kept. Oldest lines are removed, when exceeded.
    /// </summary>
    public const int MaxDetailLines = 10000;

    private const int DetailsFlushIntervalMilliseconds = 200;

    private bool _isDetailsExpanded;
    private bool _isDetailsTruncated;

    /// <summary>
    /// Detail lines reported by running task, e.g. same lines as written to recovery log file.
    /// </summary>
    public ObservableCollection<string> DetailLines { get; } = [];

    public bool HasDetails => DetailLines.Count > 0;

    public bool IsDetailsExpanded
    {
        get => _isDetailsExpanded;
        set => this.RaiseAndSetIfChanged(ref _isDetailsExpanded, value);
    }

    /// <summary>
    /// Oldest detail lines were removed, as maximum number of detail lines was exceeded.
    /// </summary>
    public bool IsDetailsTruncated
    {
        get => _isDetailsTruncated;
        private set => this.RaiseAndSetIfChanged(ref _isDetailsTruncated, value);
    }

    public string DetailsTruncatedText => $"Showing last {MaxDetailLines} lines, see log file for all details.";

    private void AddDetailLines(IList<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var hadDetails = HasDetails;

        // only add lines fitting within max detail lines
        foreach (var line in lines.Skip(Math.Max(0, lines.Count - MaxDetailLines)))
        {
            DetailLines.Add(line);
        }

        var removeCount = DetailLines.Count - MaxDetailLines;
        if (removeCount > 0 || lines.Count > MaxDetailLines)
        {
            for (var i = 0; i < removeCount; i++)
            {
                DetailLines.RemoveAt(0);
            }

            IsDetailsTruncated = true;
        }

        if (!hadDetails)
        {
            this.RaisePropertyChanged(nameof(HasDetails));
        }
    }

    private void ClearDetails()
    {
        DetailLines.Clear();
        IsDetailsTruncated = false;
        this.RaisePropertyChanged(nameof(HasDetails));
    }

    /// <summary>
    /// Run task showing progress until completed, failed or cancelled.
    /// </summary>
    public Task RunAsync(string title, Func<IProgress<ProgressModel>, CancellationToken, Task> action) =>
        RunAsync(title, async (progress, token) =>
        {
            await action(progress, token);
            return null;
        });

    /// <summary>
    /// Run task showing progress until completed, failed or cancelled.
    /// Message returned by task is shown, when task completed successfully.
    /// </summary>
    public Task RunAsync(string title, Func<IProgress<ProgressModel>, CancellationToken, Task<string?>> action) =>
        RunAsync(title, async (progress, _, token) =>
        {
            var message = await action(progress, token);
            return new ProgressResult(message);
        });

    /// <summary>
    /// Run task showing progress and details reported by task until completed, failed or cancelled.
    /// Details are shown in a collapsable details panel while task is running and after it completes.
    /// Result summary and status returned by task is shown, when task completed successfully.
    /// </summary>
    public async Task RunAsync(string title,
        Func<IProgress<ProgressModel>, IProgress<string>, CancellationToken, Task<ProgressResult?>> action)
    {
        if (IsRunning) return;

        Reset();
        Title = title;
        IsRunning = true;
        IsVisible = true;
        _cts = new CancellationTokenSource();

        var progress = new Progress<ProgressModel>(p =>
        {
            if (IsRunning)
                Update(p);
        });

        // details are queued by task and added to details lines in batches to avoid flooding ui thread
        var details = new DetailsLog();
        var detailsTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(DetailsFlushIntervalMilliseconds),
            DispatcherPriority.Background, (_, _) => AddDetailLines(details.Drain()));
        detailsTimer.Start();

        string? errorMessage = null;
        ProgressResult? result = null;
        try
        {
            // run task on thread pool to keep ui responsive, as commands has synchronous parts
            // like flushing and closing destination when finishing. progress is reported to ui thread
            var token = _cts.Token;
            result = await Task.Run(() => action(progress, details, token), CancellationToken.None);
        }
        catch (Exception e)
        {
            errorMessage = e.Message;
        }

        detailsTimer.Stop();
        AddDetailLines(details.Drain());

        var cancelled = _cts.IsCancellationRequested;
        _cts.Dispose();
        _cts = null;
        IsRunning = false;

        if (cancelled)
        {
            // cancelled tasks closes progress like gui app
            IsVisible = false;
            return;
        }

        PercentComplete = 100;
        HasError = errorMessage != null;
        ErrorMessage = errorMessage ?? string.Empty;
        ResultMessage = errorMessage == null ? result?.Message ?? string.Empty : string.Empty;
        ResultStatus = errorMessage == null ? result?.Status ?? string.Empty : string.Empty;
        IsResultWarning = errorMessage == null && (result?.IsWarning ?? false);
        SpeedText = string.Empty;
        EtaText = string.Empty;
        BytesText = string.Empty;
        IsComplete = true;
    }

    private void Cancel()
    {
        if (_cts == null || IsCancelling) return;

        IsCancelling = true;
        Stage = "Cancelling, please wait...";
        _cts.Cancel();
    }

    private void Update(ProgressModel p)
    {
        // keep showing cancelling, when cancel is requested
        if (IsCancelling)
            return;

        // cache flush stage is kept once entered, as late progress from main stage can arrive after
        if (p.IsCacheFlush)
            _isCacheFlush = true;
        else if (_isCacheFlush)
            return;

        Stage = p.Stage ?? string.Empty;

        PercentComplete = p.PercentComplete;
        IsIndeterminate = p.IsIndeterminate;

        SpeedText = p.BytesPerSecond is > 0
            ? $"{ByteSize.FromBytes(p.BytesPerSecond.Value).Humanize("0.0")}/s"
            : string.Empty;

        if (p.IsIndeterminate)
        {
            // total size is unknown, show bytes processed and time elapsed so far
            EtaText = p.MillisecondsElapsed is > 0
                ? $"Elapsed: {TimeSpan.FromMilliseconds(p.MillisecondsElapsed.Value).Humanize()}"
                : string.Empty;

            BytesText = p.BytesProcessed is > 0
                ? ByteSize.FromBytes(p.BytesProcessed.Value).Humanize("0.0")
                : string.Empty;
            return;
        }

        EtaText = p.MillisecondsRemaining is > 0
            ? $"ETA: {TimeSpan.FromMilliseconds(p.MillisecondsRemaining.Value).Humanize()}"
            : string.Empty;

        BytesText = p.BytesProcessed.HasValue && p.BytesTotal.HasValue &&
                    (p.BytesProcessed is not 0 || p.BytesRemaining is not (null or 0))
            ?$"{ByteSize.FromBytes(p.BytesProcessed.Value).Humanize("0.0")} / {ByteSize.FromBytes(p.BytesTotal.Value).Humanize("0.0")}"
            : string.Empty;
    }

    private void Reset()
    {
        IsComplete = false;
        PercentComplete = 0;
        IsIndeterminate = false;
        Title = string.Empty;
        Stage = string.Empty;
        _isCacheFlush = false;
        IsCancelling = false;
        ClearDetails();
        ResultStatus = string.Empty;
        IsResultWarning = false;
        SpeedText = string.Empty;
        EtaText = string.Empty;
        BytesText = string.Empty;
        HasError = false;
        ErrorMessage = string.Empty;
        ResultMessage = string.Empty;
    }
}
