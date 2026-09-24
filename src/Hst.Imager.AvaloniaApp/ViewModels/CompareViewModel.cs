using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class CompareViewModel : ViewModelBase
{
    private readonly IMediaService _mediaService;
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;

    private ObservableCollection<MediaItemViewModel> _mediaItems = [];
    private MediaItemViewModel? _sourceMedia;
    private MediaItemViewModel? _destMedia;
    private string _sourceType = "Image file";
    private string _destType = "Physical disk";
    private string _sourcePath = string.Empty;
    private string _destinationPath = string.Empty;
    private long _sourceStartOffset;
    private long _destinationStartOffset;
    private decimal _size;
    private string _sizeUnit = "Bytes";
    private bool _byteswap;
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private CancellationTokenSource? _cts;

    public static readonly string[] MediaTypes = ["Image file", "Physical disk"];
    public static readonly string[] SizeUnits = ["GB", "MB", "KB", "Bytes"];

    public CompareViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService)
    {
        _mediaService = mediaService;
        _imagingService = imagingService;
        _dialogService = dialogService;

        Progress = new ProgressViewModel();

        RefreshMediaCommand = ReactiveCommand.CreateFromTask(RefreshMediaAsync);
        BrowseSourceCommand = ReactiveCommand.CreateFromTask(BrowseSourceAsync);
        BrowseDestinationCommand = ReactiveCommand.CreateFromTask(BrowseDestinationAsync);

        var canStart = this.WhenAnyValue(
            x => x.SourceType, x => x.DestType,
            x => x.SourcePath, x => x.SourceMedia,
            x => x.DestinationPath, x => x.DestMedia,
            x => x.Progress.IsRunning,
            (srcType, dstType, srcPath, srcMedia, dstPath, dstMedia, running) =>
            {
                if (running) return false;
                var srcOk = srcType == "Image file" ? !string.IsNullOrEmpty(srcPath) : srcMedia != null;
                var dstOk = dstType == "Image file" ? !string.IsNullOrEmpty(dstPath) : dstMedia != null;
                return srcOk && dstOk;
            });

        StartCompareCommand = ReactiveCommand.CreateFromTask(StartCompareAsync, canStart);
        CancelCommand = ReactiveCommand.Create(Cancel, this.WhenAnyValue(x => x.Progress.IsRunning));

        _ = RefreshMediaAsync();
    }

    public ProgressViewModel Progress { get; }

    public ObservableCollection<MediaItemViewModel> MediaItems
    {
        get => _mediaItems;
        set => this.RaiseAndSetIfChanged(ref _mediaItems, value);
    }

    public string SourceType
    {
        get => _sourceType;
        set
        {
            this.RaiseAndSetIfChanged(ref _sourceType, value);
            this.RaisePropertyChanged(nameof(SourceIsImageFile));
        }
    }

    public string DestType
    {
        get => _destType;
        set
        {
            this.RaiseAndSetIfChanged(ref _destType, value);
            this.RaisePropertyChanged(nameof(DestIsImageFile));
        }
    }

    public bool SourceIsImageFile => _sourceType == "Image file";
    public bool DestIsImageFile => _destType == "Image file";

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

    public MediaItemViewModel? SourceMedia
    {
        get => _sourceMedia;
        set => this.RaiseAndSetIfChanged(ref _sourceMedia, value);
    }

    public MediaItemViewModel? DestMedia
    {
        get => _destMedia;
        set => this.RaiseAndSetIfChanged(ref _destMedia, value);
    }

    public long SourceStartOffset
    {
        get => _sourceStartOffset;
        set => this.RaiseAndSetIfChanged(ref _sourceStartOffset, value);
    }

    public long DestinationStartOffset
    {
        get => _destinationStartOffset;
        set => this.RaiseAndSetIfChanged(ref _destinationStartOffset, value);
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

    private async Task RefreshMediaAsync()
    {
        try
        {
            var medias = await _mediaService.ListMediaAsync();
            MediaItems = new ObservableCollection<MediaItemViewModel>(medias.Select(m => new MediaItemViewModel
                { Path = m.Path, Name = m.Name, DiskSize = m.DiskSize, IsPhysicalDrive = m.IsPhysicalDrive, MediaInfo = m }));
            if (SourceMedia == null && !SourceIsImageFile && MediaItems.Count > 0)
                SourceMedia = MediaItems[0];
            if (DestMedia == null && !DestIsImageFile && MediaItems.Count > 0)
                DestMedia = MediaItems[0];
        }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
    }

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
        var path = await _dialogService.ShowOpenFileDialogAsync("Select destination image file",
        [
            new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd", "xz", "gz", "zip"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);
        if (path != null) DestinationPath = path;
    }

    private async Task StartCompareAsync()
    {
        HasError = false;
        _cts = new CancellationTokenSource();
        Progress.Reset();
        Progress.IsRunning = true;
        try
        {
            var srcPath = SourceIsImageFile ? SourcePath : _sourceMedia!.Path;
            var dstPath = DestIsImageFile ? DestinationPath : _destMedia!.Path;
            var progress = new Progress<Models.ProgressModel>(p =>
            {
                Progress.Update(p);
                if (p.IsComplete && !p.HasError) Progress.IsRunning = false;
            });
            await _imagingService.CompareAsync(srcPath, SourceStartOffset, dstPath,
                DestinationStartOffset, SizeInBytes, Byteswap, progress, _cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { _cts?.Dispose(); _cts = null; Progress.IsRunning = false; }
    }

    private void Cancel() => _cts?.Cancel();
}
