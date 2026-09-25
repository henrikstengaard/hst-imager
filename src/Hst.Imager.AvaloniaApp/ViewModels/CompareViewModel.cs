using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.Core.Commands;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class CompareViewModel : ViewModelBase
{
    private readonly IMediaService _mediaService;
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    private ObservableCollection<MediaItemViewModel> _mediaItems = [];
    private MediaItemViewModel? _sourceDisk;
    private MediaItemViewModel? _destDisk;
    private SelectOption _sourceType;
    private SelectOption _destType;
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

    public CompareViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService,
        INavigationService navigationService, ProgressViewModel progress)
    {
        _mediaService = mediaService;
        _imagingService = imagingService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        _sourceType = SourceTypeOptions[0];
        _destType = SourceTypeOptions[0];

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

        RefreshMediaCommand = ReactiveCommand.CreateFromTask(RefreshMediaAsync);
        BrowseSourceCommand = ReactiveCommand.CreateFromTask(BrowseSourceAsync);
        BrowseDestinationCommand = ReactiveCommand.CreateFromTask(BrowseDestinationAsync);

        var canStart = this.WhenAnyValue(
            x => x.SourceType, x => x.DestType,
            x => x.SourcePath, x => x.SourceDisk,
            x => x.DestinationPath, x => x.DestDisk,
            x => x.Progress.IsRunning,
            (_, _, _, _, _, _, running) =>
                !running && !string.IsNullOrWhiteSpace(EffectiveSourcePath) &&
                !string.IsNullOrWhiteSpace(EffectiveDestinationPath));

        StartCompareCommand = ReactiveCommand.CreateFromTask(StartCompareAsync, canStart);
        CancelCommand = ReactiveCommand.Create(Cancel);

        this.WhenAnyValue(x => x.SourceType, x => x.SourcePath, x => x.SourceDisk)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(_ => Observable.FromAsync(LoadSourceInfoAsync))
            .Concat()
            .Subscribe();
        this.WhenAnyValue(x => x.DestType, x => x.DestinationPath, x => x.DestDisk)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(_ => Observable.FromAsync(LoadDestinationInfoAsync))
            .Concat()
            .Subscribe();

        _ = RefreshMediaAsync();
    }

    public ProgressViewModel Progress { get; }
    public PartPathSelection SrcPartPath { get; }
    public PartPathSelection DestPartPath { get; }
    public List<SelectOption> SourceTypeOptions { get; } = MediaOptions.SourceTypeOptions;

    public ObservableCollection<MediaItemViewModel> MediaItems
    {
        get => _mediaItems;
        set => this.RaiseAndSetIfChanged(ref _mediaItems, value);
    }

    public SelectOption SourceType
    {
        get => _sourceType;
        set
        {
            this.RaiseAndSetIfChanged(ref _sourceType, value);
            this.RaisePropertyChanged(nameof(SourceIsImageFile));
            this.RaisePropertyChanged(nameof(SourceIsPhysicalDisk));
            this.RaisePropertyChanged(nameof(SrcPartPathLabel));
        }
    }

    public SelectOption DestType
    {
        get => _destType;
        set
        {
            this.RaiseAndSetIfChanged(ref _destType, value);
            this.RaisePropertyChanged(nameof(DestIsImageFile));
            this.RaisePropertyChanged(nameof(DestIsPhysicalDisk));
            this.RaisePropertyChanged(nameof(DestPartPathLabel));
        }
    }

    public bool SourceIsImageFile => _sourceType.Value == MediaOptions.ImageFile;
    public bool SourceIsPhysicalDisk => !SourceIsImageFile;
    public bool DestIsImageFile => _destType.Value == MediaOptions.ImageFile;
    public bool DestIsPhysicalDisk => !DestIsImageFile;

    public string SrcPartPathLabel => $"Part of source {SourceTypeFormatted} to compare";
    public string DestPartPathLabel => $"Part of destination {DestTypeFormatted} to compare";

    private string SourceTypeFormatted => SourceIsImageFile ? "image file" : "physical disk";
    private string DestTypeFormatted => DestIsImageFile ? "image file" : "physical disk";

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

    public MediaItemViewModel? SourceDisk
    {
        get => _sourceDisk;
        set => this.RaiseAndSetIfChanged(ref _sourceDisk, value);
    }

    public MediaItemViewModel? DestDisk
    {
        get => _destDisk;
        set => this.RaiseAndSetIfChanged(ref _destDisk, value);
    }

    private string? EffectiveSourcePath => SourceIsImageFile ? _sourcePath : _sourceDisk?.Path;
    private string? EffectiveDestinationPath => DestIsImageFile ? _destinationPath : _destDisk?.Path;

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

    public ReactiveCommand<Unit, Unit> RefreshMediaCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseSourceCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseDestinationCommand { get; }
    public ReactiveCommand<Unit, Unit> StartCompareCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    private long SizeInBytes => _sizeUnit switch
    {
        "GB" => (long)(_size * 1_000_000_000m),
        "MB" => (long)(_size * 1_000_000m),
        "KB" => (long)(_size * 1_000m),
        _ => (long)_size
    };

    private string FormattedSize => SrcPartPath.IsCustom ? $" with size {_size} {_sizeUnit}" : string.Empty;

    private async Task RefreshMediaAsync()
    {
        try
        {
            var medias = await _mediaService.ListMediaAsync();
            MediaItems = new ObservableCollection<MediaItemViewModel>(medias.Select(m => new MediaItemViewModel
                { Path = m.Path, Name = m.Name, DiskSize = m.DiskSize, IsPhysicalDrive = m.IsPhysicalDrive, MediaInfo = m }));
            if (SourceDisk == null && MediaItems.Count > 0)
                SourceDisk = MediaItems[0];
            if (DestDisk == null && MediaItems.Count > 0)
                DestDisk = MediaItems[0];
        }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
    }

    private async Task LoadSourceInfoAsync()
    {
        var path = EffectiveSourcePath;
        var media = string.IsNullOrWhiteSpace(path) ? null : await GetInfoAsync(path, Byteswap);
        if (path != EffectiveSourcePath) return;
        _sourceMedia = media;
        this.RaisePropertyChanged(nameof(HasSourceMedia));
        SrcPartPath.Update(media);
    }

    private async Task LoadDestinationInfoAsync()
    {
        var path = EffectiveDestinationPath;
        var media = string.IsNullOrWhiteSpace(path) ? null : await GetInfoAsync(path, false);
        if (path != EffectiveDestinationPath) return;
        _destinationMedia = media;
        this.RaisePropertyChanged(nameof(HasDestinationMedia));
        DestPartPath.Update(media);
    }

    private async Task<MediaInfo?> GetInfoAsync(string path, bool byteswap)
    {
        try
        {
            HasError = false;
            return await _mediaService.GetMediaInfoAsync(path, byteswap);
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
        var path = await _dialogService.ShowOpenFileDialogAsync("Select source file",
        [
            new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd", "xz", "gz", "zip", "rar"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);
        if (path != null) SourcePath = path;
    }

    private async Task BrowseDestinationAsync()
    {
        var path = await _dialogService.ShowOpenFileDialogAsync("Select destination image file",
        [
            new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd", "xz", "gz", "zip", "rar"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);
        if (path != null) DestinationPath = path;
    }

    private async Task StartCompareAsync()
    {
        var srcPath = EffectiveSourcePath!;
        var dstPath = EffectiveDestinationPath!;
        var srcName = _sourceMedia?.Name ?? srcPath;
        var dstName = _destinationMedia?.Name ?? dstPath;
        var description = $"'{srcName}{SrcPartPath.Formatted}' and '{dstName}{DestPartPath.Formatted}'{FormattedSize}";
        if (!await _dialogService.ShowConfirmDialogAsync("Compare", $"Do you want to compare {description}?"))
            return;

        var sourcePath = SrcPartPath.ResolvePath(srcPath);
        var srcStartOffset = SrcPartPath.StartOffset;
        var destinationPath = DestPartPath.ResolvePath(dstPath);
        var destStartOffset = DestPartPath.StartOffset;
        var size = SizeInBytes;
        var byteswap = Byteswap;
        await Progress.RunAsync($"Comparing {description}", (progress, token) =>
            _imagingService.CompareAsync(sourcePath, srcStartOffset, destinationPath, destStartOffset, size, byteswap,
                progress, token));
    }

    private void Cancel() => _navigationService.NavigateTo("Start");
}
