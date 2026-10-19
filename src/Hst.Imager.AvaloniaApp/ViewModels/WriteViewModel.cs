using System.Reactive;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;
using ReactiveUI.Reactive;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class WriteViewModel : ViewModelBase
{
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    public WriteViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService,
        INavigationService navigationService, ProgressViewModel progress)
    {
        _imagingService = imagingService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        Progress = progress;
        Source = new MediaSelectionViewModel(mediaService, dialogService, new MediaSelectionOptions
        {
            Title = "Source",
            BrowseTitle = "Select source image file",
            ShowByteswap = true,
            ByteswapLabel = "Byteswap source sectors"
        });
        SourceLayout = new MediaPartitionLayoutViewModel(mediaService, Source);
        Destination = new MediaSelectionViewModel(mediaService, dialogService, new MediaSelectionOptions
        {
            Title = "Destination",
            AllowImageFile = false,
            AllowPhysicalDisk = true,
            PartMode = MediaPartMode.PartPath,
            PartLabel = "Part of destination physical disk to write to",
            ShowSize = true
        });

        StartWriteCommand = ReactiveCommand.CreateFromTask(StartWriteAsync,
            this.WhenAnyValue(x => x.Source.IsSelected, x => x.Destination.IsSelected, x => x.Progress.IsRunning,
                (source, destination, running) => source && destination && !running));
        ResetCommand = ReactiveCommand.Create(() => _navigationService.NavigateTo("Write"),
            this.WhenAnyValue(x => x.Progress.IsRunning, running => !running));

        _ = Destination.InitializeAsync();
    }

    public ProgressViewModel Progress { get; }
    public MediaSelectionViewModel Source { get; }
    public MediaSelectionViewModel Destination { get; }
    public MediaPartitionLayoutViewModel SourceLayout { get; }

    public ReactiveCommand<Unit, Unit> StartWriteCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }

    private async Task StartWriteAsync()
    {
        var description = $"source image file '{Source.Path}' to destination physical disk '{Destination.SelectedDisk!.Name}{Destination.PartPath.Formatted}'{Destination.FormattedSize}";
        if (!await _dialogService.ShowConfirmDialogAsync("Write", $"Do you want to write {description}?"))
            return;

        var sourcePath = Source.Path!;
        var destPath = Destination.ResolvedPath!;
        var startOffset = Destination.StartOffset;
        var size = Destination.SizeInBytes;
        var byteswap = Source.Byteswap;
        await Progress.RunAsync($"Writing {description}", (progress, token) =>
            _imagingService.WriteAsync(sourcePath, destPath, startOffset, size, byteswap, progress, token));
    }
}
