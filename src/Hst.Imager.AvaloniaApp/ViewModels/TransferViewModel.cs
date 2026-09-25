using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.Core.Commands;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class TransferViewModel : ViewModelBase
{
    private readonly IMediaService _mediaService;
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    private string _sourcePath = string.Empty;
    private string _destinationPath = string.Empty;
    private MediaInfo? _sourceMedia;
    private MediaInfo? _destinationMedia;
    private decimal _size;
    private string _sizeUnit = "Bytes";
    private bool _byteswap;
    private string _errorMessage = string.Empty;
    private bool _hasError;

    public static readonly string[] SizeUnits = ["GB", "MB", "KB", "Bytes"];

    public TransferViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService,
        INavigationService navigationService, ProgressViewModel progress)
    {
        _mediaService = mediaService;
        _imagingService = imagingService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        Progress = progress;
        SrcPartPath = new PartPathSelection();
        SrcPartPath.SelectionChanged += option =>
        {
            Size = option?.Value == MediaOptions.CustomPartPath ? _sourceMedia?.DiskSize ?? 0 : 0;
            SizeUnit = "Bytes";
            this.RaisePropertyChanged(nameof(IsSizeEnabled));
        };
        DestPartPath = new PartPathSelection();
        DestPartPath.SelectionChanged += _ => this.RaisePropertyChanged(nameof(IsSizeEnabled));

        BrowseSourceCommand = ReactiveCommand.CreateFromTask(BrowseSourceAsync);
        BrowseDestinationCommand = ReactiveCommand.CreateFromTask(BrowseDestinationAsync);
        StartTransferCommand = ReactiveCommand.CreateFromTask(StartTransferAsync,
            this.WhenAnyValue(x => x.SourcePath, x => x.DestinationPath, x => x.Progress.IsRunning,
                (src, dst, running) => !string.IsNullOrWhiteSpace(src) && !string.IsNullOrWhiteSpace(dst) && !running));
        CancelCommand = ReactiveCommand.Create(Cancel);

        this.WhenAnyValue(x => x.SourcePath)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(_ => Observable.FromAsync(LoadSourceInfoAsync))
            .Concat()
            .Subscribe();
        this.WhenAnyValue(x => x.DestinationPath)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(_ => Observable.FromAsync(LoadDestinationInfoAsync))
            .Concat()
            .Subscribe();
    }

    public ProgressViewModel Progress { get; }
    public PartPathSelection SrcPartPath { get; }
    public PartPathSelection DestPartPath { get; }

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

    public bool HasSourceMedia => _sourceMedia != null;
    public bool HasDestinationMedia => _destinationMedia != null;
    public bool IsSizeEnabled => SrcPartPath.IsCustom || DestPartPath.IsCustom;

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
        set
        {
            this.RaiseAndSetIfChanged(ref _byteswap, value);
            _ = LoadSourceInfoAsync();
        }
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

    private string FormattedSize => IsSizeEnabled ? $" with size {_size} {_sizeUnit}" : string.Empty;

    private async Task LoadSourceInfoAsync()
    {
        var path = SourcePath;
        var media = string.IsNullOrWhiteSpace(path) ? null : await GetInfoAsync(path, Byteswap, false);
        if (path != SourcePath) return;
        _sourceMedia = media;
        this.RaisePropertyChanged(nameof(HasSourceMedia));
        SrcPartPath.Update(_sourceMedia);
    }

    private async Task LoadDestinationInfoAsync()
    {
        var path = DestinationPath;
        var media = string.IsNullOrWhiteSpace(path) ? null : await GetInfoAsync(path, false, true);
        if (path != DestinationPath) return;
        _destinationMedia = media;
        this.RaisePropertyChanged(nameof(HasDestinationMedia));
        DestPartPath.Update(_destinationMedia);
    }

    private async Task<MediaInfo?> GetInfoAsync(string path, bool byteswap, bool allowNonExisting)
    {
        try
        {
            HasError = false;
            return await _mediaService.GetMediaInfoAsync(path, byteswap, allowNonExisting);
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            return null;
        }
    }

    private async Task BrowseSourceAsync()
    {
        var path = await _dialogService.ShowOpenFileDialogAsync("Select source image file",
        [
            new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd", "xz", "gz", "zip", "rar"] },
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
        var srcMediaName = _sourceMedia?.Name ?? Path.GetFileName(SourcePath);
        var destMediaName = _destinationMedia?.Name ?? Path.GetFileName(DestinationPath);
        var description = $"source image file '{srcMediaName}{SrcPartPath.Formatted}' to destination image file '{destMediaName}{DestPartPath.Formatted}'{FormattedSize}";
        if (!await _dialogService.ShowConfirmDialogAsync("Transfer", $"Do you want to transfer {description}?"))
            return;

        var srcPath = SrcPartPath.ResolvePath(SourcePath);
        var srcStartOffset = SrcPartPath.StartOffset;
        var destPath = DestPartPath.ResolvePath(DestinationPath);
        var destStartOffset = DestPartPath.StartOffset;
        var size = SizeInBytes;
        var byteswap = Byteswap;
        await Progress.RunAsync($"Transferring {description}", (progress, token) =>
            _imagingService.TransferAsync(srcPath, srcStartOffset, destPath, destStartOffset, size, byteswap,
                progress, token));
    }

    private void Cancel() => _navigationService.NavigateTo("Start");
}
