using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class TransferViewModel : ViewModelBase
{
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    public TransferViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService,
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
            PartMode = MediaPartMode.PartPath,
            PartLabel = "Part of source image file to read from",
            ShowSize = true,
            ShowByteswap = true,
            ByteswapLabel = "Byteswap source sectors"
        });
        Destination = new MediaSelectionViewModel(mediaService, dialogService, new MediaSelectionOptions
        {
            Title = "Destination",
            SaveImageFile = true,
            BrowseTitle = "Select destination image file",
            FileFilters =
            [
                new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd", "gz", "zip"] },
                new FileFilterItem { Name = "All files", Extensions = ["*"] }
            ],
            AllowNonExisting = true,
            PartMode = MediaPartMode.PartPath,
            PartLabel = "Part of destination image file to write to"
        });

        // size is also used for custom part of destination
        Destination.Committed += (_, _) => Source.SizeAlwaysEnabled = Destination.PartPath.IsCustom;

        StartTransferCommand = ReactiveCommand.CreateFromTask(StartTransferAsync,
            this.WhenAnyValue(x => x.Source.IsSelected, x => x.Destination.IsSelected, x => x.Progress.IsRunning,
                (source, destination, running) => source && destination && !running));
        ResetCommand = ReactiveCommand.Create(() => _navigationService.NavigateTo("Transfer"),
            this.WhenAnyValue(x => x.Progress.IsRunning, running => !running));
    }

    public ProgressViewModel Progress { get; }
    public MediaSelectionViewModel Source { get; }
    public MediaSelectionViewModel Destination { get; }

    public ReactiveCommand<Unit, Unit> StartTransferCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }

    private async Task StartTransferAsync()
    {
        var srcMediaName = Source.Media?.Name ?? Path.GetFileName(Source.Path);
        var destMediaName = Destination.Media?.Name ?? Path.GetFileName(Destination.Path);
        var description = $"source image file '{srcMediaName}{Source.PartPath.Formatted}' to destination image file '{destMediaName}{Destination.PartPath.Formatted}'{Source.FormattedSize}";
        if (!await _dialogService.ShowConfirmDialogAsync("Transfer", $"Do you want to transfer {description}?"))
            return;

        var srcPath = Source.ResolvedPath!;
        var srcStartOffset = Source.StartOffset;
        var destPath = Destination.ResolvedPath!;
        var destStartOffset = Destination.StartOffset;
        var size = Source.SizeInBytes;
        var byteswap = Source.Byteswap;
        await Progress.RunAsync($"Transferring {description}", (progress, token) =>
            _imagingService.TransferAsync(srcPath, srcStartOffset, destPath, destStartOffset, size, byteswap,
                progress, token));
    }
}
