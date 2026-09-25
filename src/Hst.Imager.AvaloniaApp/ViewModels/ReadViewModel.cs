using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class ReadViewModel : ViewModelBase
{
    private readonly IMediaService _mediaService;
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    private ObservableCollection<MediaItemViewModel> _mediaItems = [];
    private MediaItemViewModel? _selectedMedia;
    private string _destinationPath = string.Empty;
    private decimal _size;
    private string _sizeUnit = "Bytes";
    private bool _byteswap;
    private string _errorMessage = string.Empty;
    private bool _hasError;

    public static readonly string[] SizeUnits = ["GB", "MB", "KB", "Bytes"];

    public ReadViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService,
        INavigationService navigationService, ProgressViewModel progress)
    {
        _mediaService = mediaService;
        _imagingService = imagingService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        Progress = progress;
        PartPath = new PartPathSelection();
        PartPath.SelectionChanged += option =>
        {
            Size = option?.Value == MediaOptions.CustomPartPath ? _selectedMedia?.DiskSize ?? 0 : 0;
            SizeUnit = "Bytes";
        };

        RefreshMediaCommand = ReactiveCommand.CreateFromTask(RefreshMediaAsync);
        BrowseDestinationCommand = ReactiveCommand.CreateFromTask(BrowseDestinationAsync);
        StartReadCommand = ReactiveCommand.CreateFromTask(StartReadAsync,
            this.WhenAnyValue(x => x.SelectedMedia, x => x.DestinationPath, x => x.Progress.IsRunning,
                (media, dest, running) => media != null && !string.IsNullOrWhiteSpace(dest) && !running));
        CancelCommand = ReactiveCommand.Create(Cancel);

        _ = RefreshMediaAsync();
    }

    public ProgressViewModel Progress { get; }
    public PartPathSelection PartPath { get; }

    public ObservableCollection<MediaItemViewModel> MediaItems
    {
        get => _mediaItems;
        set => this.RaiseAndSetIfChanged(ref _mediaItems, value);
    }

    public MediaItemViewModel? SelectedMedia
    {
        get => _selectedMedia;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedMedia, value);
            this.RaisePropertyChanged(nameof(HasSelectedMedia));
            PartPath.Update(null);
            if (value != null)
                _ = LoadMediaInfoAsync(value.Path);
        }
    }

    public bool HasSelectedMedia => _selectedMedia != null;

    public string DestinationPath
    {
        get => _destinationPath;
        set => this.RaiseAndSetIfChanged(ref _destinationPath, value);
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
        set
        {
            this.RaiseAndSetIfChanged(ref _byteswap, value);
            if (_selectedMedia != null)
                _ = LoadMediaInfoAsync(_selectedMedia.Path);
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
    public ReactiveCommand<Unit, Unit> BrowseDestinationCommand { get; }
    public ReactiveCommand<Unit, Unit> StartReadCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    private long SizeInBytes => _sizeUnit switch
    {
        "GB" => (long)(_size * 1_000_000_000m),
        "MB" => (long)(_size * 1_000_000m),
        "KB" => (long)(_size * 1_000m),
        _ => (long)_size
    };

    private string FormattedSize => PartPath.IsCustom ? $" with size {_size} {_sizeUnit}" : string.Empty;

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

    private async Task LoadMediaInfoAsync(string path)
    {
        try
        {
            HasError = false;
            var info = await _mediaService.GetMediaInfoAsync(path, _byteswap);
            if (info != null && _selectedMedia != null && _selectedMedia.Path == path)
            {
                _selectedMedia.MediaInfo = info;
                _selectedMedia.DiskSize = info.DiskSize;
                PartPath.Update(info);
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
    }

    private async Task BrowseDestinationAsync()
    {
        var path = await _dialogService.ShowSaveFileDialogAsync("Select destination image",
        [
            new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd", "gz", "zip"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);
        if (path != null)
            DestinationPath = path;
    }

    private async Task StartReadAsync()
    {
        var media = _selectedMedia!;
        var description = $"source physical disk '{media.Name}{PartPath.Formatted}' to destination image file '{DestinationPath}'{FormattedSize}";
        if (!await _dialogService.ShowConfirmDialogAsync("Read", $"Do you want to read {description}?"))
            return;

        var sourcePath = PartPath.ResolvePath(media.Path);
        var destinationPath = DestinationPath;
        var startOffset = PartPath.StartOffset;
        var size = SizeInBytes;
        var byteswap = Byteswap;
        await Progress.RunAsync($"Reading {description}", (progress, token) =>
            _imagingService.ReadAsync(sourcePath, destinationPath, startOffset, size, byteswap, progress, token));
    }

    private void Cancel() => _navigationService.NavigateTo("Start");
}
