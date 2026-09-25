using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.Core.Commands;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class OptimizeViewModel : ViewModelBase
{
    private readonly IMediaService _mediaService;
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    private string _imagePath = string.Empty;
    private MediaInfo? _media;
    private ObservableCollection<SelectOption> _sizeOptions = [];
    private SelectOption? _selectedSizeOption;
    private decimal _size;
    private string _sizeUnit = "Bytes";
    private bool _byteswap;
    private string _errorMessage = string.Empty;
    private bool _hasError;

    public static readonly string[] SizeUnits = ["GB", "MB", "KB", "Bytes"];

    public OptimizeViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService,
        INavigationService navigationService, ProgressViewModel progress)
    {
        _mediaService = mediaService;
        _imagingService = imagingService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        Progress = progress;

        BrowseImageCommand = ReactiveCommand.CreateFromTask(BrowseImageAsync);
        StartOptimizeCommand = ReactiveCommand.CreateFromTask(StartOptimizeAsync,
            this.WhenAnyValue(x => x.ImagePath, x => x.HasMedia, x => x.Progress.IsRunning,
                (path, hasMedia, running) => !string.IsNullOrWhiteSpace(path) && hasMedia && !running));
        CancelCommand = ReactiveCommand.Create(Cancel);

        this.WhenAnyValue(x => x.ImagePath)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(_ => Observable.FromAsync(LoadInfoAsync))
            .Concat()
            .Subscribe();
    }

    public ProgressViewModel Progress { get; }

    public string ImagePath
    {
        get => _imagePath;
        set => this.RaiseAndSetIfChanged(ref _imagePath, value);
    }

    public bool HasMedia => _media != null;

    public ObservableCollection<SelectOption> SizeOptions
    {
        get => _sizeOptions;
        private set => this.RaiseAndSetIfChanged(ref _sizeOptions, value);
    }

    public SelectOption? SelectedSizeOption
    {
        get => _selectedSizeOption;
        set
        {
            if (ReferenceEquals(_selectedSizeOption, value)) return;
            this.RaiseAndSetIfChanged(ref _selectedSizeOption, value);
            if (value == null) return;
            Size = long.Parse(value.Value, CultureInfo.InvariantCulture);
            SizeUnit = "Bytes";
        }
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
            if (_media != null)
                _ = LoadInfoAsync();
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

    private async Task LoadInfoAsync()
    {
        var path = ImagePath;
        MediaInfo? media = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                HasError = false;
                media = await _mediaService.GetMediaInfoAsync(path, Byteswap);
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
            }
        }

        if (path != ImagePath) return;

        _media = media;
        this.RaisePropertyChanged(nameof(HasMedia));

        var options = new ObservableCollection<SelectOption>();
        if (media != null)
        {
            options.Add(SizeOption("Disk", media.DiskSize));
            if (media.DiskInfo?.GptPartitionTablePart != null)
                options.Add(SizeOption("Guid Partition Table", media.DiskInfo.GptPartitionTablePart.Size));
            if (media.DiskInfo?.MbrPartitionTablePart != null)
                options.Add(SizeOption("Master Boot Record", media.DiskInfo.MbrPartitionTablePart.Size));
            if (media.DiskInfo?.RdbPartitionTablePart != null)
                options.Add(SizeOption("Rigid Disk Block", media.DiskInfo.RdbPartitionTablePart.Size));
        }

        // default size and unit
        _selectedSizeOption = null;
        SizeOptions = options;
        _selectedSizeOption = options.Count > 0 ? options[0] : null;
        this.RaisePropertyChanged(nameof(SelectedSizeOption));
        Size = 0;
        SizeUnit = "Bytes";
    }

    private static SelectOption SizeOption(string title, long size) => new()
    {
        Title = $"{title} ({MediaOptions.FormatBytes(size)})",
        Value = size.ToString(CultureInfo.InvariantCulture)
    };

    private async Task BrowseImageAsync()
    {
        var path = await _dialogService.ShowOpenFileDialogAsync("Select image file",
        [
            new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);
        if (path != null) ImagePath = path;
    }

    private async Task StartOptimizeAsync()
    {
        var sizeFormatted = Size > 0 ? $" to size {Size} {SizeUnit}" : string.Empty;
        if (!await _dialogService.ShowConfirmDialogAsync("Optimize",
                $"Do you want to optimize image file '{ImagePath}'{sizeFormatted}?"))
            return;

        var path = ImagePath;
        var size = SizeInBytes;
        var byteswap = Byteswap;
        await Progress.RunAsync($"Optimizing image file '{path}'", (progress, token) =>
            _imagingService.OptimizeAsync(path, size, byteswap, progress, token));
    }

    private void Cancel() => _navigationService.NavigateTo("Start");
}
