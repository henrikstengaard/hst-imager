using System;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class TransferViewModel : ViewModelBase
{
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;

    private string _sourcePath = string.Empty;
    private string _destinationPath = string.Empty;
    private long _srcStartOffset;
    private long _destStartOffset;
    private decimal _size;
    private string _sizeUnit = "Bytes";
    private bool _byteswap;
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private CancellationTokenSource? _cts;

    public static readonly string[] SizeUnits = ["GB", "MB", "KB", "Bytes"];

    public TransferViewModel(IImagingService imagingService, IDialogService dialogService)
    {
        _imagingService = imagingService;
        _dialogService = dialogService;

        Progress = new ProgressViewModel();

        BrowseSourceCommand = ReactiveCommand.CreateFromTask(BrowseSourceAsync);
        BrowseDestinationCommand = ReactiveCommand.CreateFromTask(BrowseDestinationAsync);
        StartTransferCommand = ReactiveCommand.CreateFromTask(StartTransferAsync,
            this.WhenAnyValue(x => x.SourcePath, x => x.DestinationPath, x => x.Progress.IsRunning,
                (src, dst, running) => !string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(dst) && !running));
        CancelCommand = ReactiveCommand.Create(Cancel, this.WhenAnyValue(x => x.Progress.IsRunning));
    }

    public ProgressViewModel Progress { get; }

    public string SourcePath
    {
        get => _sourcePath;
        set => this.RaiseAndSetIfChanged(ref _sourcePath, value);
    }

    public string DestinationPath
    {
        get => _destinationPath;
        set => this.RaiseAndSetIfChanged(ref _destinationPath, value);
    }

    public long SrcStartOffset
    {
        get => _srcStartOffset;
        set => this.RaiseAndSetIfChanged(ref _srcStartOffset, value);
    }

    public long DestStartOffset
    {
        get => _destStartOffset;
        set => this.RaiseAndSetIfChanged(ref _destStartOffset, value);
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

    public ReactiveCommand<Unit, Unit> BrowseSourceCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseDestinationCommand { get; }
    public ReactiveCommand<Unit, Unit> StartTransferCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    private long SizeInBytes => _sizeUnit switch
    {
        "GB" => (long)(_size * 1_000_000_000m),
        "MB" => (long)(_size * 1_000_000m),
        "KB" => (long)(_size * 1_000m),
        _ => (long)_size
    };

    private async Task BrowseSourceAsync()
    {
        var path = await _dialogService.ShowOpenFileDialogAsync("Select source image file",
        [
            new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd", "xz", "gz", "zip"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);
        if (path != null) SourcePath = path;
    }

    private async Task BrowseDestinationAsync()
    {
        var path = await _dialogService.ShowSaveFileDialogAsync("Select destination image file",
        [
            new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd", "gz", "zip"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);
        if (path != null) DestinationPath = path;
    }

    private async Task StartTransferAsync()
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
            await _imagingService.TransferAsync(SourcePath, SrcStartOffset,
                DestinationPath, DestStartOffset, SizeInBytes, Byteswap, progress, _cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { _cts?.Dispose(); _cts = null; Progress.IsRunning = false; }
    }

    private void Cancel() => _cts?.Cancel();
}
