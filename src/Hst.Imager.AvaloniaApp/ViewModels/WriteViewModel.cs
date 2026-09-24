using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class WriteViewModel : ViewModelBase
{
    private readonly IMediaService _mediaService;
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;

    private ObservableCollection<MediaItemViewModel> _mediaItems = [];
    private MediaItemViewModel? _selectedMedia;
    private string _sourcePath = string.Empty;
    private long _startOffset;
    private decimal _size;
    private string _sizeUnit = "Bytes";
    private bool _byteswap;
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private CancellationTokenSource? _cts;

    public static readonly string[] SizeUnits = ["GB", "MB", "KB", "Bytes"];

    public WriteViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService)
    {
        _mediaService = mediaService;
        _imagingService = imagingService;
        _dialogService = dialogService;

        Progress = new ProgressViewModel();

        RefreshMediaCommand = ReactiveCommand.CreateFromTask(RefreshMediaAsync);
        BrowseSourceCommand = ReactiveCommand.CreateFromTask(BrowseSourceAsync);
        StartWriteCommand = ReactiveCommand.CreateFromTask(StartWriteAsync,
            this.WhenAnyValue(x => x.SelectedMedia, x => x.SourcePath, x => x.Progress.IsRunning,
                (media, src, running) => media != null && !string.IsNullOrEmpty(src) && !running));
        CancelCommand = ReactiveCommand.Create(Cancel,
            this.WhenAnyValue(x => x.Progress.IsRunning));

        _ = RefreshMediaAsync();
    }

    public ProgressViewModel Progress { get; }

    public ObservableCollection<MediaItemViewModel> MediaItems
    {
        get => _mediaItems;
        set => this.RaiseAndSetIfChanged(ref _mediaItems, value);
    }

    public MediaItemViewModel? SelectedMedia
    {
        get => _selectedMedia;
        set => this.RaiseAndSetIfChanged(ref _selectedMedia, value);
    }

    public string SourcePath
    {
        get => _sourcePath;
        set => this.RaiseAndSetIfChanged(ref _sourcePath, value);
    }

    public long StartOffset
    {
        get => _startOffset;
        set => this.RaiseAndSetIfChanged(ref _startOffset, value);
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
    public ReactiveCommand<Unit, Unit> StartWriteCommand { get; }
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
            HasError = false;
            var medias = await _mediaService.ListMediaAsync();
            var items = medias.Select(m => new MediaItemViewModel
            {
                Path = m.Path,
                Name = m.Name,
                DiskSize = m.DiskSize,
                IsPhysicalDrive = m.IsPhysicalDrive,
                MediaInfo = m
            }).ToList();

            MediaItems = new ObservableCollection<MediaItemViewModel>(items);
            if (SelectedMedia == null && items.Count > 0)
                SelectedMedia = items[0];
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
    }

    private async Task BrowseSourceAsync()
    {
        var path = await _dialogService.ShowOpenFileDialogAsync("Select source image file",
        [
            new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd", "gz", "zip"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);
        if (path != null)
            SourcePath = path;
    }

    private async Task StartWriteAsync()
    {
        HasError = false;
        _cts = new CancellationTokenSource();
        Progress.Reset();
        Progress.IsRunning = true;

        try
        {
            var destPath = _selectedMedia!.Path;
            var progress = new Progress<Models.ProgressModel>(p =>
            {
                Progress.Update(p);
                if (p.IsComplete && !p.HasError)
                    Progress.IsRunning = false;
            });

            await _imagingService.WriteAsync(SourcePath, destPath, StartOffset, SizeInBytes, Byteswap,
                progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            Progress.IsRunning = false;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            Progress.IsRunning = false;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            Progress.IsRunning = false;
        }
    }

    private void Cancel() => _cts?.Cancel();
}
