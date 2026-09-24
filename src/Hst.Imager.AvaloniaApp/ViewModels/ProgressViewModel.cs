using System;
using Humanizer;
using Humanizer.Bytes;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class ProgressViewModel : ViewModelBase
{
    private bool _isRunning;
    private double _percentComplete;
    private string _title = string.Empty;
    private string _speedText = string.Empty;
    private string _etaText = string.Empty;
    private string _bytesText = string.Empty;
    private bool _hasError;
    private string _errorMessage = string.Empty;
    private bool _isComplete;

    public bool IsRunning
    {
        get => _isRunning;
        set => this.RaiseAndSetIfChanged(ref _isRunning, value);
    }

    public bool IsComplete
    {
        get => _isComplete;
        set => this.RaiseAndSetIfChanged(ref _isComplete, value);
    }

    public double PercentComplete
    {
        get => _percentComplete;
        set => this.RaiseAndSetIfChanged(ref _percentComplete, value);
    }

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
        set => this.RaiseAndSetIfChanged(ref _hasError, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public void Update(Models.ProgressModel p)
    {
        Title = p.Title;
        PercentComplete = p.PercentComplete;
        IsComplete = p.IsComplete;
        HasError = p.HasError;
        ErrorMessage = p.ErrorMessage ?? string.Empty;

        if (p.BytesPerSecond.HasValue && p.BytesPerSecond.Value > 0)
            SpeedText = $"{ByteSize.FromBytes(p.BytesPerSecond.Value).Humanize("#.#")}/s";
        else
            SpeedText = string.Empty;

        if (p.MillisecondsRemaining.HasValue && p.MillisecondsRemaining.Value > 0)
            EtaText = $"ETA: {TimeSpan.FromMilliseconds(p.MillisecondsRemaining.Value).Humanize()}";
        else
            EtaText = string.Empty;

        if (p.BytesProcessed.HasValue && p.BytesTotal.HasValue)
            BytesText = $"{ByteSize.FromBytes(p.BytesProcessed.Value).Humanize("#.#")} / {ByteSize.FromBytes(p.BytesTotal.Value).Humanize("#.#")}";
        else
            BytesText = string.Empty;
    }

    public void Reset()
    {
        IsRunning = false;
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
