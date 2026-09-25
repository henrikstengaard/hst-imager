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
    private readonly INavigationService _navigationService;

    private string _outputPath = string.Empty;
    private decimal _size = 16m;
    private string _sizeUnit = "GB";
    private bool _compatibleSize = true;
    private string _errorMessage = string.Empty;
    private bool _hasError;

    public static readonly string[] SizeUnits = ["GB", "MB", "KB", "Bytes"];

    public BlankViewModel(IImagingService imagingService, IDialogService dialogService,
        INavigationService navigationService, ProgressViewModel progress)
    {
        _imagingService = imagingService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        Progress = progress;

        BrowseOutputCommand = ReactiveCommand.CreateFromTask(BrowseOutputAsync);
        StartBlankCommand = ReactiveCommand.CreateFromTask(StartBlankAsync,
            this.WhenAnyValue(x => x.OutputPath, x => x.Size, x => x.Progress.IsRunning,
                (path, size, running) => !string.IsNullOrEmpty(path) && size > 0 && !running));
        CancelCommand = ReactiveCommand.Create(Cancel);
    }

    public ProgressViewModel Progress { get; }

    public string OutputPath
    {
        get => _outputPath;
        set => this.RaiseAndSetIfChanged(ref _outputPath, value);
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

    public bool CompatibleSize
    {
        get => _compatibleSize;
        set => this.RaiseAndSetIfChanged(ref _compatibleSize, value);
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

    public ReactiveCommand<Unit, Unit> BrowseOutputCommand { get; }
    public ReactiveCommand<Unit, Unit> StartBlankCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    private long SizeInBytes => _sizeUnit switch
    {
        "GB" => (long)(_size * 1_000_000_000m),
        "MB" => (long)(_size * 1_000_000m),
        "KB" => (long)(_size * 1_000m),
        _ => (long)_size
    };

    private async Task BrowseOutputAsync()
    {
        var path = await _dialogService.ShowSaveFileDialogAsync("Select image file to create",
        [
            new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);
        if (path != null) OutputPath = path;
    }

    private async Task StartBlankAsync()
    {
        if (!await _dialogService.ShowConfirmDialogAsync("Blank",
                $"Do you want to create blank image file '{OutputPath}' with size '{Size} {SizeUnit.ToUpperInvariant()}'?"))
            return;

        var path = OutputPath;
        var size = SizeInBytes;
        var compatibleSize = CompatibleSize;
        await Progress.RunAsync($"Creating {Size} {SizeUnit} blank image '{path}'", (progress, token) =>
            _imagingService.BlankAsync(path, size, compatibleSize, progress, token));
    }

    private void Cancel() => _navigationService.NavigateTo("Start");
}
