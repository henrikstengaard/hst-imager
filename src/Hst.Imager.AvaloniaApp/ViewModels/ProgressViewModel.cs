using System;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Humanizer;
using Humanizer.Bytes;
using Hst.Imager.AvaloniaApp.Models;
using ReactiveUI;

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
    private string _title = string.Empty;
    private string _speedText = string.Empty;
    private string _etaText = string.Empty;
    private string _bytesText = string.Empty;
    private bool _hasError;
    private string _errorMessage = string.Empty;
    private CancellationTokenSource? _cts;

    public ProgressViewModel()
    {
        CancelCommand = ReactiveCommand.Create(Cancel, this.WhenAnyValue(x => x.IsRunning));
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
        set => this.RaiseAndSetIfChanged(ref _isRunning, value);
    }

    public bool IsComplete
    {
        get => _isComplete;
        set
        {
            this.RaiseAndSetIfChanged(ref _isComplete, value);
            this.RaisePropertyChanged(nameof(IsSuccess));
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
        }
    }

    public string PercentText => $"{_percentComplete:0.0} %";

    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

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
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    /// <summary>
    /// Run task showing progress until completed, failed or cancelled.
    /// </summary>
    public async Task RunAsync(string title, Func<IProgress<ProgressModel>, CancellationToken, Task> action)
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

        string? errorMessage = null;
        try
        {
            await action(progress, _cts.Token);
        }
        catch (Exception e)
        {
            errorMessage = e.Message;
        }

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
        SpeedText = string.Empty;
        EtaText = string.Empty;
        BytesText = string.Empty;
        IsComplete = true;
    }

    private void Cancel() => _cts?.Cancel();

    private void Update(ProgressModel p)
    {
        PercentComplete = p.PercentComplete;

        SpeedText = p.BytesPerSecond is > 0
            ? $"{ByteSize.FromBytes(p.BytesPerSecond.Value).Humanize("#.#")}/s"
            : string.Empty;

        EtaText = p.MillisecondsRemaining is > 0
            ? $"ETA: {TimeSpan.FromMilliseconds(p.MillisecondsRemaining.Value).Humanize()}"
            : string.Empty;

        BytesText = p.BytesProcessed.HasValue && p.BytesTotal.HasValue
            ? $"{ByteSize.FromBytes(p.BytesProcessed.Value).Humanize("#.#")} / {ByteSize.FromBytes(p.BytesTotal.Value).Humanize("#.#")}"
            : string.Empty;
    }

    private void Reset()
    {
        IsComplete = false;
        PercentComplete = 0;
        Title = string.Empty;
        SpeedText = string.Empty;
        EtaText = string.Empty;
        BytesText = string.Empty;
        HasError = false;
        ErrorMessage = string.Empty;
    }
}
