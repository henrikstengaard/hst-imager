using System;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class BlankViewModel : ViewModelBase
{
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;

    private string _outputPath = string.Empty;
    private long _size = 1_000_000_000; // 1 GB default
    private bool _compatibleSize;
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private CancellationTokenSource? _cts;

    public BlankViewModel(IImagingService imagingService, IDialogService dialogService)
    {
        _imagingService = imagingService;
        _dialogService = dialogService;

        Progress = new ProgressViewModel();

        BrowseOutputCommand = ReactiveCommand.CreateFromTask(BrowseOutputAsync);
        StartBlankCommand = ReactiveCommand.CreateFromTask(StartBlankAsync,
            this.WhenAnyValue(x => x.OutputPath, x => x.Progress.IsRunning,
                (path, running) => !string.IsNullOrEmpty(path) && !running));
        CancelCommand = ReactiveCommand.Create(Cancel, this.WhenAnyValue(x => x.Progress.IsRunning));
    }

    public ProgressViewModel Progress { get; }
    public string OutputPath { get => _outputPath; set => this.RaiseAndSetIfChanged(ref _outputPath, value); }
    public long Size { get => _size; set => this.RaiseAndSetIfChanged(ref _size, value); }
    public bool CompatibleSize { get => _compatibleSize; set => this.RaiseAndSetIfChanged(ref _compatibleSize, value); }
    public string ErrorMessage { get => _errorMessage; set => this.RaiseAndSetIfChanged(ref _errorMessage, value); }
    public bool HasError { get => _hasError; set => this.RaiseAndSetIfChanged(ref _hasError, value); }

    public ReactiveCommand<Unit, Unit> BrowseOutputCommand { get; }
    public ReactiveCommand<Unit, Unit> StartBlankCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    private async Task BrowseOutputAsync()
    {
        var path = await _dialogService.ShowSaveFileDialogAsync("Save blank image as",
        [
            new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);
        if (path != null) OutputPath = path;
    }

    private async Task StartBlankAsync()
    {
        HasError = false;
        _cts = new CancellationTokenSource();
        Progress.Reset();
        Progress.IsRunning = true;
        try
        {
            var progress = new Progress<Models.ProgressModel>(p =>
            {
                Progress.Update(p);
                if (p.IsComplete && !p.HasError) Progress.IsRunning = false;
            });
            await _imagingService.BlankAsync(OutputPath, Size, CompatibleSize, progress, _cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { _cts?.Dispose(); _cts = null; Progress.IsRunning = false; }
    }

    private void Cancel() => _cts?.Cancel();
}
