using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Humanizer;
using Humanizer.Bytes;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.Core.Commands;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class InfoViewModel : ViewModelBase
{
    private readonly IMediaService _mediaService;
    private readonly IDialogService _dialogService;

    private ObservableCollection<MediaItemViewModel> _mediaItems = [];
    private MediaItemViewModel? _selectedMedia;
    private string _customPath = string.Empty;
    private bool _byteswap;
    private bool _useCustomPath;
    private MediaInfo? _mediaInfo;
    private string _diskInfoText = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private bool _isLoading;

    public InfoViewModel(IMediaService mediaService, IDialogService dialogService)
    {
        _mediaService = mediaService;
        _dialogService = dialogService;

        RefreshMediaCommand = ReactiveCommand.CreateFromTask(RefreshMediaAsync);
        BrowsePathCommand = ReactiveCommand.CreateFromTask(BrowsePathAsync);
        GetInfoCommand = ReactiveCommand.CreateFromTask(GetInfoAsync);

        _ = RefreshMediaAsync();
    }

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
            if (value != null && !UseCustomPath)
                _ = GetInfoAsync();
        }
    }

    public string CustomPath
    {
        get => _customPath;
        set => this.RaiseAndSetIfChanged(ref _customPath, value);
    }

    public bool UseCustomPath
    {
        get => _useCustomPath;
        set => this.RaiseAndSetIfChanged(ref _useCustomPath, value);
    }

    public bool Byteswap
    {
        get => _byteswap;
        set
        {
            this.RaiseAndSetIfChanged(ref _byteswap, value);
            _ = GetInfoAsync();
        }
    }

    public MediaInfo? MediaInfo
    {
        get => _mediaInfo;
        set => this.RaiseAndSetIfChanged(ref _mediaInfo, value);
    }

    public string DiskInfoText
    {
        get => _diskInfoText;
        set => this.RaiseAndSetIfChanged(ref _diskInfoText, value);
    }

    public bool HasDiskInfo => _mediaInfo?.DiskInfo != null;

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

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshMediaCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowsePathCommand { get; }
    public ReactiveCommand<Unit, Unit> GetInfoCommand { get; }

    private async Task RefreshMediaAsync()
    {
        try
        {
            HasError = false;
            var medias = await _mediaService.ListMediaAsync();
            var items = medias.Select(m => new MediaItemViewModel
            {
                Path = m.Path, Name = m.Name, DiskSize = m.DiskSize,
                IsPhysicalDrive = m.IsPhysicalDrive, MediaInfo = m
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

    private async Task BrowsePathAsync()
    {
        var path = await _dialogService.ShowOpenFileDialogAsync("Select disk image",
        [
            new FileFilterItem { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd", "gz", "zip"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);
        if (path != null)
        {
            CustomPath = path;
            UseCustomPath = true;
            await GetInfoAsync();
        }
    }

    private async Task GetInfoAsync()
    {
        var path = UseCustomPath ? CustomPath : _selectedMedia?.Path;
        if (string.IsNullOrEmpty(path)) return;

        IsLoading = true;
        HasError = false;
        MediaInfo = null;
        DiskInfoText = string.Empty;

        try
        {
            var info = await _mediaService.GetMediaInfoAsync(path, Byteswap, allowNonExisting: true);
            MediaInfo = info;
            this.RaisePropertyChanged(nameof(HasDiskInfo));

            if (info?.DiskInfo != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Name: {info.DiskInfo.Name}");
                sb.AppendLine($"Size: {Humanizer.Bytes.ByteSize.FromBytes(info.DiskInfo.Size).Humanize("#.#")}");
                if (info.DiskInfo.GptPartitionTablePart != null)
                    sb.AppendLine($"Partition table: GPT ({info.DiskInfo.GptPartitionTablePart.Parts?.Count() ?? 0} partitions)");
                if (info.DiskInfo.MbrPartitionTablePart != null)
                    sb.AppendLine($"Partition table: MBR ({info.DiskInfo.MbrPartitionTablePart.Parts?.Count() ?? 0} partitions)");
                if (info.DiskInfo.RdbPartitionTablePart != null)
                    sb.AppendLine($"Partition table: RDB ({info.DiskInfo.RdbPartitionTablePart.Parts?.Count() ?? 0} partitions)");
                DiskInfoText = sb.ToString();
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
