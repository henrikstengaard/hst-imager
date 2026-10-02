using System.Reactive;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class BlankViewModel : ViewModelBase
{
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    private decimal _size = 16m;
    private string _sizeUnit = "GB";
    private bool _compatibleSize = true;
    private string _errorMessage = string.Empty;
    private bool _hasError;

    public static readonly string[] SizeUnits = ["GB", "MB", "KB", "Bytes"];

    public BlankViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService,
        INavigationService navigationService, ProgressViewModel progress)
    {
        _imagingService = imagingService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        Progress = progress;

        Destination = new MediaSelectionViewModel(mediaService, dialogService, new MediaSelectionOptions
        {
            Title = "Disk",
            SaveImageFile = true,
            BrowseTitle = "Select image file to create",
            FileFilters =
            [
                new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd"] },
                new FileFilterItem { Name = "All files", Extensions = ["*"] }
            ],
            LoadMedia = false
        });

        ResetCommand = ReactiveCommand.Create(() => _navigationService.NavigateTo("Blank"),
            this.WhenAnyValue(x => x.Progress.IsRunning, running => !running));
        StartBlankCommand = ReactiveCommand.CreateFromTask(StartBlankAsync,
            this.WhenAnyValue(x => x.Destination.IsSelected, x => x.Size, x => x.Progress.IsRunning,
                (selected, size, running) => selected && size > 0 && !running));
    }

    public ProgressViewModel Progress { get; }

    public MediaSelectionViewModel Destination { get; }

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

    public ReactiveCommand<Unit, Unit> StartBlankCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }

    private long SizeInBytes => _sizeUnit switch
    {
        "GB" => (long)(_size * 1_000_000_000m),
        "MB" => (long)(_size * 1_000_000m),
        "KB" => (long)(_size * 1_000m),
        _ => (long)_size
    };


    private async Task StartBlankAsync()
    {
        if (!await _dialogService.ShowConfirmDialogAsync("Blank",
                $"Do you want to create blank image file '{Destination.Path}' with size '{Size} {SizeUnit.ToUpperInvariant()}'?"))
            return;

        var path = Destination.Path!;
        var size = SizeInBytes;
        var compatibleSize = CompatibleSize;
        await Progress.RunAsync($"Creating {Size} {SizeUnit} blank image '{path}'", (progress, token) =>
            _imagingService.BlankAsync(path, size, compatibleSize, progress, token));
    }
}
