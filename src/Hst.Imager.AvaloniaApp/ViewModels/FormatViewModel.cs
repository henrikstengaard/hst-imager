using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using ReactiveUI;
using Unit = System.Reactive.Unit;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class FormatTypeOption
{
    public string Title { get; set; } = string.Empty;
    public FormatType Value { get; set; }
}

public class FormatViewModel : ViewModelBase
{
    private const string Pfs3AioUrl = FormatCommand.Pfs3AioLhaUrl;

    private static readonly List<SelectOption> BasicFileSystemOptions =
    [
        new() { Title = "FAT32", Value = "fat32" },
        new() { Title = "exFAT", Value = "exfat" },
        new() { Title = "NTFS", Value = "ntfs" }
    ];

    private static readonly List<SelectOption> RdbFileSystemOptions =
    [
        new() { Title = "PDS\\3 (direct scsi)", Value = "pds3" },
        new() { Title = "PFS\\3", Value = "pfs3" },
        new() { Title = "DOS\\3", Value = "dos3" },
        new() { Title = "DOS\\7 (long filename)", Value = "dos7" }
    ];

    private static readonly List<SelectOption> DefaultMaxPartitionSizeOptions =
    [
        new() { Title = "128 GB", Value = "137438953472" },
        new() { Title = "64 GB", Value = "68719476736" },
        new() { Title = "32 GB", Value = "34359738368" },
        new() { Title = "16 GB", Value = "17179869184" },
        new() { Title = "8 GB", Value = "8589934592" },
        new() { Title = "4 GB", Value = "4294967296" },
        new() { Title = "2 GB", Value = "2147483648" },
        new() { Title = "1 GB", Value = "1073741824" }
    ];

    private static readonly List<SelectOption> Pfs3MaxPartitionSizeOptions =
    [
        new() { Title = "101.6 GB", Value = "109067239424" },
        new() { Title = "64 GB", Value = "68719476736" },
        new() { Title = "32 GB", Value = "34359738368" },
        new() { Title = "16 GB", Value = "17179869184" },
        new() { Title = "8 GB", Value = "8589934592" },
        new() { Title = "4 GB", Value = "4294967296" },
        new() { Title = "2 GB", Value = "2147483648" },
        new() { Title = "1 GB", Value = "1073741824" }
    ];

    private static readonly List<SelectOption> Pfs3ExperimentalMaxPartitionSizeOptions =
    [
        new() { Title = "2 TB", Value = "2199023255552" },
        new() { Title = "1 TB", Value = "1099511627776" },
        new() { Title = "512 GB", Value = "549755813888" },
        new() { Title = "256 GB", Value = "274877906944" },
        new() { Title = "128 GB", Value = "137438953472" }
    ];

    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    private FormatTypeOption _selectedFormatType;
    private List<SelectOption> _fileSystemOptions = BasicFileSystemOptions;
    private SelectOption _selectedFileSystem = BasicFileSystemOptions[0];
    private bool _downloadPfs3Aio = true;
    private string _fileSystemPath = Pfs3AioUrl;
    private decimal _size;
    private string _sizeUnit = "Bytes";
    private List<SelectOption> _maxPartitionSizeOptions = Pfs3MaxPartitionSizeOptions;
    private SelectOption _maxPartitionSize = Pfs3MaxPartitionSizeOptions[0];
    private bool _useExperimental;
    private bool _kickstart31;

    public static readonly string[] SizeUnits = ["GB", "MB", "KB", "Bytes"];

    public FormatViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService,
        INavigationService navigationService, ProgressViewModel progress)
    {
        _imagingService = imagingService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        Progress = progress;
        Source = new MediaSelectionViewModel(mediaService, dialogService, new MediaSelectionOptions
        {
            Title = "Disk",
            AllowPhysicalDisk = true,
            FileFilters =
            [
                new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd"] },
                new FileFilterItem { Name = "All files", Extensions = ["*"] }
            ],
            ShowByteswap = true
        });
        Source.Committed += (_, _) => ResetSize();

        FormatTypeOptions =
        [
            new FormatTypeOption { Title = "Master Boot Record", Value = FormatType.Mbr },
            new FormatTypeOption { Title = "Guid Partition Table", Value = FormatType.Gpt },
            new FormatTypeOption { Title = "Rigid Disk Block", Value = FormatType.Rdb },
            new FormatTypeOption { Title = "PiStorm (Master Boot Record)", Value = FormatType.PiStormMbr },
            new FormatTypeOption { Title = "PiStorm (Guid Partition Table)", Value = FormatType.PiStormGpt }
        ];
        _selectedFormatType = FormatTypeOptions[0];

        BrowseFileSystemPathCommand = ReactiveCommand.CreateFromTask(BrowseFileSystemPathAsync);
        ResetSizeCommand = ReactiveCommand.Create(ResetSize);
        StartFormatCommand = ReactiveCommand.CreateFromTask(StartFormatAsync,
            this.WhenAnyValue(x => x.Source.IsSelected, x => x.SelectedFormatType, x => x.FileSystemPath,
                x => x.Progress.IsRunning, (_, _, _, running) => !running && CanFormat));
        ResetCommand = ReactiveCommand.Create(() => _navigationService.NavigateTo("Format"),
            this.WhenAnyValue(x => x.Progress.IsRunning, running => !running));

    }

    public ProgressViewModel Progress { get; }
    public List<FormatTypeOption> FormatTypeOptions { get; }

    public MediaSelectionViewModel Source { get; }

    public FormatTypeOption SelectedFormatType
    {
        get => _selectedFormatType;
        set
        {
            if (value == null || ReferenceEquals(_selectedFormatType, value)) return;
            this.RaiseAndSetIfChanged(ref _selectedFormatType, value);
            this.RaisePropertyChanged(nameof(IsRdbFormat));

            if (IsRdbFormat)
            {
                FileSystemOptions = RdbFileSystemOptions;
                _selectedFileSystem = RdbFileSystemOptions[0];
                DownloadPfs3Aio = true;
                Kickstart31 = false;
                UpdateMaxPartitionSizeOptions();
            }
            else
            {
                FileSystemOptions = BasicFileSystemOptions;
                _selectedFileSystem = BasicFileSystemOptions[0];
            }
            RaiseFileSystemChanged();
        }
    }

    public bool IsRdbFormat => _selectedFormatType.Value is FormatType.Rdb or FormatType.PiStorm or FormatType.PiStormMbr
        or FormatType.PiStormGpt;

    public List<SelectOption> FileSystemOptions
    {
        get => _fileSystemOptions;
        private set => this.RaiseAndSetIfChanged(ref _fileSystemOptions, value);
    }

    public SelectOption SelectedFileSystem
    {
        get => _selectedFileSystem;
        set
        {
            if (value == null || ReferenceEquals(_selectedFileSystem, value)) return;
            this.RaiseAndSetIfChanged(ref _selectedFileSystem, value);
            DownloadPfs3Aio = IsPfs3FileSystem;
            UpdateMaxPartitionSizeOptions();
            RaiseFileSystemChanged();
        }
    }

    public bool IsPfs3FileSystem => _selectedFileSystem.Value is "pds3" or "pfs3";
    public bool ShowDownloadPfs3Aio => IsRdbFormat && IsPfs3FileSystem;
    public bool ShowFileSystemPath => IsRdbFormat && !_downloadPfs3Aio;
    public bool ShowUseExperimental => IsRdbFormat && IsPfs3FileSystem;

    public bool DownloadPfs3Aio
    {
        get => _downloadPfs3Aio;
        set
        {
            this.RaiseAndSetIfChanged(ref _downloadPfs3Aio, value);
            FileSystemPath = value ? Pfs3AioUrl : string.Empty;
            this.RaisePropertyChanged(nameof(ShowFileSystemPath));
        }
    }

    public string FileSystemPath { get => _fileSystemPath; set => this.RaiseAndSetIfChanged(ref _fileSystemPath, value); }

    public decimal Size { get => _size; set => this.RaiseAndSetIfChanged(ref _size, value); }
    public string SizeUnit { get => _sizeUnit; set => this.RaiseAndSetIfChanged(ref _sizeUnit, value); }

    public List<SelectOption> MaxPartitionSizeOptions
    {
        get => _maxPartitionSizeOptions;
        private set => this.RaiseAndSetIfChanged(ref _maxPartitionSizeOptions, value);
    }

    public SelectOption MaxPartitionSize
    {
        get => _maxPartitionSize;
        set
        {
            if (value == null) return;
            this.RaiseAndSetIfChanged(ref _maxPartitionSize, value);
        }
    }

    public bool UseExperimental
    {
        get => _useExperimental;
        set
        {
            this.RaiseAndSetIfChanged(ref _useExperimental, value);
            UpdateMaxPartitionSizeOptions();
        }
    }

    public bool Kickstart31 { get => _kickstart31; set => this.RaiseAndSetIfChanged(ref _kickstart31, value); }

    public ReactiveCommand<Unit, Unit> BrowseFileSystemPathCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetSizeCommand { get; }
    public ReactiveCommand<Unit, Unit> StartFormatCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }

    private bool CanFormat =>
        Source.IsSelected &&
        (!IsRdbFormat || !string.IsNullOrWhiteSpace(_fileSystemPath));

    private long SizeInBytes => _sizeUnit switch
    {
        "GB" => (long)(_size * 1_000_000_000m),
        "MB" => (long)(_size * 1_000_000m),
        "KB" => (long)(_size * 1_000m),
        _ => (long)_size
    };

    private void RaiseFileSystemChanged()
    {
        this.RaisePropertyChanged(nameof(SelectedFileSystem));
        this.RaisePropertyChanged(nameof(IsPfs3FileSystem));
        this.RaisePropertyChanged(nameof(ShowDownloadPfs3Aio));
        this.RaisePropertyChanged(nameof(ShowFileSystemPath));
        this.RaisePropertyChanged(nameof(ShowUseExperimental));
    }

    private void UpdateMaxPartitionSizeOptions()
    {
        var options = IsPfs3FileSystem
            ? _useExperimental ? Pfs3ExperimentalMaxPartitionSizeOptions : Pfs3MaxPartitionSizeOptions
            : DefaultMaxPartitionSizeOptions;
        MaxPartitionSizeOptions = options;
        MaxPartitionSize = options[0];
    }

    private void ResetSize()
    {
        Size = Source.Media?.DiskSize ?? 0;
        SizeUnit = "Bytes";
    }

    private async Task BrowseFileSystemPathAsync()
    {
        var path = await _dialogService.ShowOpenFileDialogAsync("Select file system file",
        [
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);
        if (path != null) FileSystemPath = path;
    }

    private async Task StartFormatAsync()
    {
        var path = Source.Path!;
        var sourceTypeFormatted = Source.IsImageFile ? "image file" : "physical disk";
        var sizeFormatted = _size == 0 ? string.Empty : $", size {_size} {_sizeUnit}";
        var description = $"{sourceTypeFormatted} '{Source.Media?.Name ?? path}' with '{_selectedFormatType.Title}' format type, '{_selectedFileSystem.Title}' file system{sizeFormatted}";
        var downloadFormatted = IsRdbFormat && IsPfs3FileSystem && _downloadPfs3Aio
            ? $" This will download pfs3aio from '{Pfs3AioUrl}'."
            : string.Empty;
        if (!await _dialogService.ShowConfirmDialogAsync("Format", $"Do you want to format {description}?{downloadFormatted}"))
            return;

        var formatType = _selectedFormatType.Value;
        var fileSystem = _selectedFileSystem.Value;
        var fileSystemPath = string.IsNullOrWhiteSpace(FileSystemPath) ? null : FileSystemPath;
        var size = SizeInBytes;
        var maxPartitionSize = long.Parse(_maxPartitionSize.Value, CultureInfo.InvariantCulture);
        var useExperimental = UseExperimental;
        var kickstart31 = Kickstart31;
        var byteswap = Source.Byteswap;
        await Progress.RunAsync($"Formatting {description}", (progress, token) =>
            _imagingService.FormatAsync(path, formatType, fileSystem, fileSystemPath, size, maxPartitionSize,
                useExperimental, kickstart31, byteswap, progress, token));
    }
}
