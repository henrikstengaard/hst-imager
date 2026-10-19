using System.Reactive;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;
using ReactiveUI.Reactive;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class ReadViewModel : ViewModelBase
{
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    public ReadViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService,
        INavigationService navigationService, ProgressViewModel progress)
    {
        _imagingService = imagingService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        Progress = progress;
        Source = new MediaSelectionViewModel(mediaService, dialogService, new MediaSelectionOptions
        {
            Title = "Source",
            AllowImageFile = false,
            AllowPhysicalDisk = true,
            PartMode = MediaPartMode.PartPath,
            PartLabel = "Part of source physical disk to read from",
            ShowSize = true,
            ShowByteswap = true,
            ByteswapLabel = "Byteswap source sectors"
        });
        SourceLayout = new MediaPartitionLayoutViewModel(mediaService, Source);
        Destination = new MediaSelectionViewModel(mediaService, dialogService, new MediaSelectionOptions
        {
            Title = "Destination",
            SaveImageFile = true,
            BrowseTitle = "Select destination image",
            FileFilters =
            [
                new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd", "gz", "zip"] },
                new FileFilterItem { Name = "All files", Extensions = ["*"] }
            ],
            LoadMedia = false
        });

        StartReadCommand = ReactiveCommand.CreateFromTask(StartReadAsync,
            this.WhenAnyValue(x => x.Source.IsSelected, x => x.Destination.IsSelected, x => x.Progress.IsRunning,
                (source, destination, running) => source && destination && !running));
        ResetCommand = ReactiveCommand.Create(() => _navigationService.NavigateTo("Read"),
            this.WhenAnyValue(x => x.Progress.IsRunning, running => !running));

        _ = Source.InitializeAsync();
    }

    public ProgressViewModel Progress { get; }
    public MediaSelectionViewModel Source { get; }
    public MediaSelectionViewModel Destination { get; }
    public MediaPartitionLayoutViewModel SourceLayout { get; }

    public ReactiveCommand<Unit, Unit> StartReadCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }

    private async Task StartReadAsync()
    {
        var description = $"source physical disk '{Source.SelectedDisk!.Name}{Source.PartPath.Formatted}' to destination image file '{Destination.Path}'{Source.FormattedSize}";
        if (!await _dialogService.ShowConfirmDialogAsync("Read", $"Do you want to read {description}?"))
            return;

        var sourcePath = Source.ResolvedPath!;
        var destinationPath = Destination.Path!;
        var startOffset = Source.StartOffset;
        var size = Source.SizeInBytes;
        var byteswap = Source.Byteswap;
        await Progress.RunAsync($"Reading {description}", (progress, token) =>
            _imagingService.ReadAsync(sourcePath, destinationPath, startOffset, size, byteswap, progress, token));
    }
}
