using System;
using System.Reactive;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;
using ReactiveUI.Reactive;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class CompareViewModel : ViewModelBase
{
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    public CompareViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService,
        INavigationService navigationService, ProgressViewModel progress)
    {
        _imagingService = imagingService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        Progress = progress;
        Source = new MediaSelectionViewModel(mediaService, dialogService, new MediaSelectionOptions
        {
            Title = "Source",
            AllowPhysicalDisk = true,
            BrowseTitle = "Select source file",
            PartMode = MediaPartMode.PartPath,
            PartLabel = "Part of source to compare",
            ShowSize = true,
            ShowByteswap = true,
            ByteswapLabel = "Byteswap source sectors"
        });
        Destination = new MediaSelectionViewModel(mediaService, dialogService, new MediaSelectionOptions
        {
            Title = "Destination",
            AllowPhysicalDisk = true,
            BrowseTitle = "Select destination image file",
            PartMode = MediaPartMode.PartPath,
            PartLabel = "Part of destination to compare"
        });

        // size is also used for custom part of destination
        this.WhenAnyValue(x => x.Destination.PartPath.IsCustom)
            .Subscribe(isCustom => Source.SizeAlwaysEnabled = isCustom);

        StartCompareCommand = ReactiveCommand.CreateFromTask(StartCompareAsync,
            this.WhenAnyValue(x => x.Source.IsSelected, x => x.Destination.IsSelected, x => x.Progress.IsRunning,
                (source, destination, running) => source && destination && !running));
        ResetCommand = ReactiveCommand.Create(() => _navigationService.NavigateTo("Compare"),
            this.WhenAnyValue(x => x.Progress.IsRunning, running => !running));
    }

    public ProgressViewModel Progress { get; }
    public MediaSelectionViewModel Source { get; }
    public MediaSelectionViewModel Destination { get; }

    public ReactiveCommand<Unit, Unit> StartCompareCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }

    private async Task StartCompareAsync()
    {
        var srcName = Source.Media?.Name ?? Source.Path;
        var dstName = Destination.Media?.Name ?? Destination.Path;
        var description = $"'{srcName}{Source.PartPath.Formatted}' and '{dstName}{Destination.PartPath.Formatted}'{Source.FormattedSize}";
        if (!await _dialogService.ShowConfirmDialogAsync("Compare", $"Do you want to compare {description}?"))
            return;

        var sourcePath = Source.ResolvedPath!;
        var srcStartOffset = Source.StartOffset;
        var destinationPath = Destination.ResolvedPath!;
        var destStartOffset = Destination.StartOffset;
        var size = Source.SizeInBytes;
        var byteswap = Source.Byteswap;
        await Progress.RunAsync($"Comparing {description}", (progress, token) =>
            _imagingService.CompareAsync(sourcePath, srcStartOffset, destinationPath, destStartOffset, size, byteswap,
                progress, token));
    }
}
