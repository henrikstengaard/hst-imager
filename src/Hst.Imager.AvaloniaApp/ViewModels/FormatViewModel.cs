using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
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
    private readonly IMediaService _mediaService;
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;

    private ObservableCollection<MediaItemViewModel> _mediaItems = [];
    private MediaItemViewModel? _selectedMedia;
    private string _customPath = string.Empty;
    private bool _useCustomPath;
    private FormatTypeOption? _selectedFormatType;
    private string _selectedFileSystem = "fat32";
    private string _fileSystemPath = string.Empty;
    private long _size;
    private long _maxPartitionSize;
    private bool _useExperimental;
    private bool _kickstart31;
    private bool _byteswap;
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private CancellationTokenSource? _cts;

    public FormatViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService)
    {
        _mediaService = mediaService;
        _imagingService = imagingService;
        _dialogService = dialogService;

        Progress = new ProgressViewModel();

        FormatTypeOptions =
        [
            new FormatTypeOption { Title = "Master Boot Record (MBR)", Value = FormatType.Mbr },
            new FormatTypeOption { Title = "Guid Partition Table (GPT)", Value = FormatType.Gpt },
            new FormatTypeOption { Title = "Rigid Disk Block (RDB)", Value = FormatType.Rdb },
            new FormatTypeOption { Title = "PiStorm", Value = FormatType.PiStorm }
        ];
        _selectedFormatType = FormatTypeOptions[0];

        FileSystemOptions = ["fat32", "exfat", "ntfs"];

        RefreshMediaCommand = ReactiveCommand.CreateFromTask(RefreshMediaAsync);
        BrowsePathCommand = ReactiveCommand.CreateFromTask(BrowsePathAsync);
        StartFormatCommand = ReactiveCommand.CreateFromTask(StartFormatAsync,
            this.WhenAnyValue(x => x.Progress.IsRunning, running => !running));
        CancelCommand = ReactiveCommand.Create(Cancel, this.WhenAnyValue(x => x.Progress.IsRunning));

        _ = RefreshMediaAsync();
    }

    public ProgressViewModel Progress { get; }
    public List<FormatTypeOption> FormatTypeOptions { get; }
    public List<string> FileSystemOptions { get; }

    public ObservableCollection<MediaItemViewModel> MediaItems { get => _mediaItems; set => this.RaiseAndSetIfChanged(ref _mediaItems, value); }
    public MediaItemViewModel? SelectedMedia { get => _selectedMedia; set => this.RaiseAndSetIfChanged(ref _selectedMedia, value); }
    public string CustomPath { get => _customPath; set => this.RaiseAndSetIfChanged(ref _customPath, value); }
    public bool UseCustomPath { get => _useCustomPath; set => this.RaiseAndSetIfChanged(ref _useCustomPath, value); }
    public FormatTypeOption? SelectedFormatType { get => _selectedFormatType; set => this.RaiseAndSetIfChanged(ref _selectedFormatType, value); }
    public string SelectedFileSystem { get => _selectedFileSystem; set => this.RaiseAndSetIfChanged(ref _selectedFileSystem, value); }
    public string FileSystemPath { get => _fileSystemPath; set => this.RaiseAndSetIfChanged(ref _fileSystemPath, value); }
    public long Size { get => _size; set => this.RaiseAndSetIfChanged(ref _size, value); }
    public long MaxPartitionSize { get => _maxPartitionSize; set => this.RaiseAndSetIfChanged(ref _maxPartitionSize, value); }
    public bool UseExperimental { get => _useExperimental; set => this.RaiseAndSetIfChanged(ref _useExperimental, value); }
    public bool Kickstart31 { get => _kickstart31; set => this.RaiseAndSetIfChanged(ref _kickstart31, value); }
    public bool Byteswap { get => _byteswap; set => this.RaiseAndSetIfChanged(ref _byteswap, value); }
    public string ErrorMessage { get => _errorMessage; set => this.RaiseAndSetIfChanged(ref _errorMessage, value); }
    public bool HasError { get => _hasError; set => this.RaiseAndSetIfChanged(ref _hasError, value); }

    public ReactiveCommand<Unit, Unit> RefreshMediaCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowsePathCommand { get; }
    public ReactiveCommand<Unit, Unit> StartFormatCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    private async Task RefreshMediaAsync()
    {
        try
        {
            var medias = await _mediaService.ListMediaAsync();
            MediaItems = new ObservableCollection<MediaItemViewModel>(medias.Select(m => new MediaItemViewModel
                { Path = m.Path, Name = m.Name, DiskSize = m.DiskSize, IsPhysicalDrive = m.IsPhysicalDrive, MediaInfo = m }));
            if (SelectedMedia == null && MediaItems.Count > 0) SelectedMedia = MediaItems[0];
        }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
    }

    private async Task BrowsePathAsync()
    {
        var path = await _dialogService.ShowOpenFileDialogAsync("Select image or disk",
        [
            new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);
        if (path != null) { CustomPath = path; UseCustomPath = true; }
    }

    private async Task StartFormatAsync()
    {
        var path = UseCustomPath ? CustomPath : _selectedMedia?.Path;
        if (string.IsNullOrEmpty(path)) { HasError = true; ErrorMessage = "No path selected."; return; }

        HasError = false;
        _cts = new CancellationTokenSource();
        Progress.Reset();
        Progress.IsRunning = true;
        try
        {
            var progress = new Progress<Models.ProgressModel>(p =>
            {
                Progress.Update(p);
                if (p.IsComplete && !p.HasError) Progress.IsRunning = false;
            });
            await _imagingService.FormatAsync(path, _selectedFormatType?.Value ?? FormatType.Mbr,
                SelectedFileSystem, string.IsNullOrEmpty(FileSystemPath) ? null : FileSystemPath,
                Size, MaxPartitionSize, UseExperimental, Kickstart31, Byteswap, progress, _cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { _cts?.Dispose(); _cts = null; Progress.IsRunning = false; }
    }

    private void Cancel() => _cts?.Cancel();
}
