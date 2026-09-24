using System;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class OptimizeViewModel : ViewModelBase
{
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;

    private string _imagePath = string.Empty;
    private decimal _size;
    private string _sizeUnit = "Bytes";
    private bool _byteswap;
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private CancellationTokenSource? _cts;

    public static readonly string[] SizeUnits = ["GB", "MB", "KB", "Bytes"];

    public OptimizeViewModel(IImagingService imagingService, IDialogService dialogService)
    {
        _imagingService = imagingService;
        _dialogService = dialogService;

        Progress = new ProgressViewModel();

        BrowseImageCommand = ReactiveCommand.CreateFromTask(BrowseImageAsync);
        StartOptimizeCommand = ReactiveCommand.CreateFromTask(StartOptimizeAsync,
            this.WhenAnyValue(x => x.ImagePath, x => x.Progress.IsRunning,
                (path, running) => !string.IsNullOrEmpty(path) && !running));
        CancelCommand = ReactiveCommand.Create(Cancel, this.WhenAnyValue(x => x.Progress.IsRunning));
    }

    public ProgressViewModel Progress { get; }

    public string ImagePath
    {
        get => _imagePath;
        set => this.RaiseAndSetIfChanged(ref _imagePath, value);
    }

    public decimal Size
    {
        get => _size;
        set => this.RaiseAndSetIfChanged(ref _size, value);
    }

    public string SizeUnit
    {
        get => _sizeUnit;
        set => this.RaiseAndSetIfChanged(ref _sizeUnit, value);
    }

    public bool Byteswap
    {
        get => _byteswap;
        set => this.RaiseAndSetIfChanged(ref _byteswap, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => this.RaiseAndSetIfChanged(ref _hasError, value);
    }

    public ReactiveCommand<Unit, Unit> BrowseImageCommand { get; }
    public ReactiveCommand<Unit, Unit> StartOptimizeCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    private long SizeInBytes => _sizeUnit switch
    {
        "GB" => (long)(_size * 1_000_000_000m),
        "MB" => (long)(_size * 1_000_000m),
        "KB" => (long)(_size * 1_000m),
        _ => (long)_size
    };

    private async Task BrowseImageAsync()
    {
        var path = await _dialogService.ShowOpenFileDialogAsync("Select image file to optimize",
        [
            new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);
        if (path != null) ImagePath = path;
    }

    private async Task StartOptimizeAsync()
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
            await _imagingService.OptimizeAsync(ImagePath, SizeInBytes, Byteswap, progress, _cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { _cts?.Dispose(); _cts = null; Progress.IsRunning = false; }
    }

    private void Cancel() => _cts?.Cancel();
}
