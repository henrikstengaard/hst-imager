using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.Core.Commands;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class OptimizeViewModel : ViewModelBase
{
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    private ObservableCollection<SelectOption> _sizeOptions = [];
    private SelectOption? _selectedSizeOption;
    private decimal _size;
    private string _sizeUnit = "Bytes";

    public static readonly string[] SizeUnits = ["GB", "MB", "KB", "Bytes"];

    public OptimizeViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService,
        INavigationService navigationService, ProgressViewModel progress)
    {
        _imagingService = imagingService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        Progress = progress;
        Source = new MediaSelectionViewModel(mediaService, dialogService, new MediaSelectionOptions
        {
            Title = "Disk",
            FileFilters =
            [
                new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd"] },
                new FileFilterItem { Name = "All files", Extensions = ["*"] }
            ],
            ShowByteswap = true
        });
        Source.Committed += (_, _) => UpdateSizeOptions(Source.Media);

        ResetCommand = ReactiveCommand.Create(() => _navigationService.NavigateTo("Optimize"),
            this.WhenAnyValue(x => x.Progress.IsRunning, running => !running));
        StartOptimizeCommand = ReactiveCommand.CreateFromTask(StartOptimizeAsync,
            this.WhenAnyValue(x => x.Source.IsSelected, x => x.Source.HasMedia, x => x.Progress.IsRunning,
                (selected, hasMedia, running) => selected && hasMedia && !running));
    }

    public ProgressViewModel Progress { get; }
    public MediaSelectionViewModel Source { get; }

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

    public ReactiveCommand<Unit, Unit> StartOptimizeCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }

    private long SizeInBytes => _sizeUnit switch
    {
        "GB" => (long)(_size * 1_000_000_000m),
        "MB" => (long)(_size * 1_000_000m),
        "KB" => (long)(_size * 1_000m),
        _ => (long)_size
    };

    private void UpdateSizeOptions(MediaInfo? media)
    {
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

    private async Task StartOptimizeAsync()
    {
        var sizeFormatted = Size > 0 ? $" to size {Size} {SizeUnit}" : string.Empty;
        if (!await _dialogService.ShowConfirmDialogAsync("Optimize",
                $"Do you want to optimize image file '{Source.Path}'{sizeFormatted}?"))
            return;

        var path = Source.Path!;
        var size = SizeInBytes;
        var byteswap = Source.Byteswap;
        await Progress.RunAsync($"Optimizing image file '{path}'", (progress, token) =>
            _imagingService.OptimizeAsync(path, size, byteswap, progress, token));
    }
}
