using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.Core.Commands;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

/// <summary>
/// Part of media selectable in media selection.
/// </summary>
public enum MediaPartMode
{
    /// <summary>
    /// Whole media only.
    /// </summary>
    None,

    /// <summary>
    /// Disk, partition tables, partitions or custom start offset.
    /// </summary>
    PartPath,

    /// <summary>
    /// Disk or PiStorm disks in master boot record partitions with bios type 0x76 (118).
    /// </summary>
    PiStormDisk
}

/// <summary>
/// Options for media selection, which media types and settings can be selected.
/// </summary>
public class MediaSelectionOptions
{
    /// <summary>
    /// Title of media selection, e.g. Source or Destination.
    /// </summary>
    public string Title { get; init; } = "Source";

    public bool AllowImageFile { get; init; } = true;
    public bool AllowPhysicalDisk { get; init; }

    /// <summary>
    /// Image file is selected with save file dialog, e.g. destination image file created by operation.
    /// </summary>
    public bool SaveImageFile { get; init; }

    public string BrowseTitle { get; init; } = "Select image file";

    public IReadOnlyList<FileFilterItem> FileFilters { get; init; } =
    [
        new() { Name = "Hard disk image files", Extensions = ["img", "hdf", "vhd", "xz", "gz", "zip", "rar"] },
        new() { Name = "All files", Extensions = ["*"] }
    ];

    /// <summary>
    /// Media info is loaded for selected media to show name and size and to get parts of media.
    /// </summary>
    public bool LoadMedia { get; init; } = true;

    /// <summary>
    /// Media info is loaded for non existing image files, e.g. destination image file created by operation.
    /// </summary>
    public bool AllowNonExisting { get; init; }

    public MediaPartMode PartMode { get; init; }
    public bool IncludePartitions { get; init; } = true;
    public string PartLabel { get; init; } = "Part of media";

    /// <summary>
    /// Size of custom part.
    /// </summary>
    public bool ShowSize { get; init; }

    public bool ShowByteswap { get; init; }
    public string ByteswapLabel { get; init; } = "Byteswap sectors";
}

/// <summary>
/// Media selection of source or destination image file or physical disk with part of media, size and byteswap.
/// Media selection is shown as a card in pages and edited in a dialog, which edits a copy of media selection that is
/// copied back when OK is clicked.
/// </summary>
public class MediaSelectionViewModel : ViewModelBase
{
    public static readonly string[] SizeUnits = ["GB", "MB", "KB", "Bytes"];

    private static readonly bool IsAdministrator = User.IsAdministrator();

    private readonly IMediaService _mediaService;
    private readonly IDialogService _dialogService;
    private readonly Subject<Unit> _loadRequests = new();

    // last choice in media choices to select image file
    private readonly MediaItemViewModel _imageFileChoice;

    private SelectOption _type;
    private string _imagePath = string.Empty;
    private ObservableCollection<MediaItemViewModel> _mediaItems = [];
    private MediaItemViewModel? _selectedDisk;
    private IReadOnlyList<MediaItemViewModel> _mediaChoices = [];
    private bool _isLoadingMedia;
    private MediaInfo? _media;
    private bool _isLoading;

    // load requested, but not completed yet
    private bool _isStale;

    // copy edited in media selection dialog, which doesn't load media info
    private bool _isEditCopy;

    private decimal _size;
    private string _sizeUnit = "Bytes";
    private bool _sizeAlwaysEnabled;
    private bool _byteswap;
    private string _errorMessage = string.Empty;
    private bool _hasError;

    public MediaSelectionViewModel(IMediaService mediaService, IDialogService dialogService,
        MediaSelectionOptions options)
    {
        _mediaService = mediaService;
        _dialogService = dialogService;
        Options = options;

        TypeOptions = MediaOptions.SourceTypeOptions
            .Where(x => x.Value == MediaOptions.ImageFile ? options.AllowImageFile : options.AllowPhysicalDisk)
            .ToList();
        _type = TypeOptions[0];
        _imageFileChoice = new MediaItemViewModel { Name = "Image file" };
        UpdateMediaChoices();

        PartPath = new PartPathSelection();
        PartPath.SelectionChanged += option =>
        {
            Size = option?.Value == MediaOptions.CustomPartPath ? _media?.DiskSize ?? 0 : 0;
            SizeUnit = "Bytes";
            RaisePartChanged();
        };
        PartPath.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PartPathSelection.StartOffset))
                RaiseDetailsChanged();
        };

        RefreshMediaCommand = ReactiveCommand.CreateFromTask(RefreshMediaAsync);
        BrowseCommand = ReactiveCommand.CreateFromTask(BrowseAsync);
        EditCommand = ReactiveCommand.CreateFromTask(EditAsync);

        // load media info, when user stops changing selection
        _loadRequests
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(_ => Observable.FromAsync(LoadMediaAsync))
            .Concat()
            .Subscribe();
    }

    public MediaSelectionOptions Options { get; }

    /// <summary>
    /// Raised when media selection is changed by OK in media selection dialog.
    /// </summary>
    public event EventHandler? Committed;

    // ─── Type ─────────────────────────────────────────────────────────────────

    public IReadOnlyList<SelectOption> TypeOptions { get; }
    public bool HasTypeOptions => TypeOptions.Count > 1;

    public SelectOption Type
    {
        get => _type;
        set
        {
            if (value == null || ReferenceEquals(_type, value)) return;
            this.RaiseAndSetIfChanged(ref _type, value);
            RaiseTypeChanged();
            RequestLoad();
        }
    }

    public bool IsImageFile => _type.Value == MediaOptions.ImageFile;
    public bool IsPhysicalDisk => !IsImageFile;

    /// <summary>
    /// Warning is shown to indicate why no physical disks are listed.
    /// </summary>
    public bool ShowElevationWarning => Options.AllowPhysicalDisk && !IsAdministrator;

    // ─── Image file ───────────────────────────────────────────────────────────

    public string ImagePath
    {
        get => _imagePath;
        set
        {
            if (_imagePath == value) return;
            this.RaiseAndSetIfChanged(ref _imagePath, value);
            RaiseSelectionChanged();
            RequestLoad();
        }
    }

    // ─── Physical disk ────────────────────────────────────────────────────────

    public ObservableCollection<MediaItemViewModel> MediaItems
    {
        get => _mediaItems;
        private set
        {
            this.RaiseAndSetIfChanged(ref _mediaItems, value);
            UpdateMediaChoices();
        }
    }

    /// <summary>
    /// Physical disks and last image file choice, if image file is allowed.
    /// </summary>
    public IReadOnlyList<MediaItemViewModel> MediaChoices
    {
        get => _mediaChoices;
        private set => this.RaiseAndSetIfChanged(ref _mediaChoices, value);
    }

    /// <summary>
    /// Selected physical disk or image file choice.
    /// </summary>
    public MediaItemViewModel? SelectedChoice
    {
        get => IsImageFile ? _imageFileChoice : _selectedDisk;
        set
        {
            // ignore null pushed by combobox, when media choices are changed
            if (value == null || ReferenceEquals(SelectedChoice, value)) return;
            if (ReferenceEquals(value, _imageFileChoice))
            {
                Type = TypeOptions.First(x => x.Value == MediaOptions.ImageFile);
                return;
            }

            SelectedDisk = value;
            Type = TypeOptions.First(x => x.Value == MediaOptions.PhysicalDisk);
        }
    }

    /// <summary>
    /// Physical disks are listed with administrator privileges. Without image file, physical disks are listed when
    /// none exist to show physical disks are empty, otherwise only image file can be selected.
    /// </summary>
    public bool ShowMediaChoices => Options.AllowPhysicalDisk && IsAdministrator &&
                                    (!Options.AllowImageFile || _isLoadingMedia || _mediaItems.Count > 0);

    public string MediaChoicesLabel => Options.AllowImageFile ? "Physical disk or image file" : "Physical disk";

    /// <summary>
    /// Image file path is shown, when image file is selected or no physical disks are listed.
    /// </summary>
    public bool ShowImageFile => Options.AllowImageFile && (IsImageFile || !ShowMediaChoices);

    public MediaItemViewModel? SelectedDisk
    {
        get => _selectedDisk;
        set
        {
            if (ReferenceEquals(_selectedDisk, value)) return;
            this.RaiseAndSetIfChanged(ref _selectedDisk, value);
            this.RaisePropertyChanged(nameof(SelectedChoice));
            RaiseSelectionChanged();
            RequestLoad();
        }
    }

    /// <summary>
    /// Physical disks are being loaded
    /// </summary>
    public bool IsLoadingMedia
    {
        get => _isLoadingMedia;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isLoadingMedia, value);
            this.RaisePropertyChanged(nameof(CanConfirm));
            RaiseMediaChoicesChanged();
        }
    }

    // ─── Media ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Path of selected image file or physical disk.
    /// </summary>
    public string? Path => IsImageFile ? _imagePath : _selectedDisk?.Path;

    public bool IsSelected => !string.IsNullOrWhiteSpace(Path);

    /// <summary>
    /// Media info of selected image file or physical disk.
    /// </summary>
    public MediaInfo? Media
    {
        get => _media;
        private set
        {
            this.RaiseAndSetIfChanged(ref _media, value);
            this.RaisePropertyChanged(nameof(HasMedia));
            this.RaisePropertyChanged(nameof(DisplayName));
            RaiseDetailsChanged();
        }
    }

    public bool HasMedia => _media != null;

    /// <summary>
    /// Media info is being loaded.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    /// <summary>
    /// Media selection can be confirmed, when physical disks are not being loaded.
    /// </summary>
    public bool CanConfirm => !_isLoadingMedia;

    // ─── Part ─────────────────────────────────────────────────────────────────

    public PartPathSelection PartPath { get; }

    public bool ShowPart => Options.PartMode switch
    {
        MediaPartMode.PartPath => PartPath.HasOptions,
        MediaPartMode.PiStormDisk => PartPath.Options.Count > 1,
        _ => false
    };

    public bool ShowStartOffset => Options.PartMode == MediaPartMode.PartPath && PartPath.HasOptions;

    /// <summary>
    /// Size is shown, when part of media is shown or size is used for custom part of other media.
    /// </summary>
    public bool ShowSize => Options.ShowSize && (ShowPart || _sizeAlwaysEnabled);

    /// <summary>
    /// Path to selected part of media.
    /// </summary>
    public string? ResolvedPath => Path == null ? null : PartPath.ResolvePath(Path);

    public long StartOffset => PartPath.StartOffset;

    // ─── Size and byteswap ────────────────────────────────────────────────────

    public decimal Size
    {
        get => _size;
        set
        {
            this.RaiseAndSetIfChanged(ref _size, value);
            RaiseDetailsChanged();
        }
    }

    public string SizeUnit
    {
        get => _sizeUnit;
        set
        {
            this.RaiseAndSetIfChanged(ref _sizeUnit, value);
            RaiseDetailsChanged();
        }
    }

    /// <summary>
    /// Size can be changed without custom part selected, e.g. when size is used for custom part of other media.
    /// </summary>
    public bool SizeAlwaysEnabled
    {
        get => _sizeAlwaysEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _sizeAlwaysEnabled, value);
            this.RaisePropertyChanged(nameof(IsSizeEnabled));
            this.RaisePropertyChanged(nameof(ShowSize));
        }
    }

    public bool IsSizeEnabled => PartPath.IsCustom || _sizeAlwaysEnabled;

    public long SizeInBytes => _sizeUnit switch
    {
        "GB" => (long)(_size * 1_000_000_000m),
        "MB" => (long)(_size * 1_000_000m),
        "KB" => (long)(_size * 1_000m),
        _ => (long)_size
    };

    /// <summary>
    /// Size formatted for confirm dialog descriptions, when size is used.
    /// </summary>
    public string FormattedSize => IsSizeEnabled ? $" with size {_size} {_sizeUnit}" : string.Empty;

    public bool Byteswap
    {
        get => _byteswap;
        set
        {
            if (_byteswap == value) return;
            this.RaiseAndSetIfChanged(ref _byteswap, value);
            RaiseDetailsChanged();
            if (_media != null)
                RequestLoad();
        }
    }

    // ─── Errors ───────────────────────────────────────────────────────────────

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => this.RaiseAndSetIfChanged(ref _hasError, value);
    }

    // ─── Card ─────────────────────────────────────────────────────────────────

    public string TypeIcon => IsImageFile ? "fa-file" : "fa-hdd";

    /// <summary>
    /// Name of selected media shown in card.
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (!IsSelected)
                return IsImageFile
                    ? HasTypeOptions ? "Select image file or physical disk" : "Select image file"
                    : "Select physical disk";

            if (IsPhysicalDisk)
                return _selectedDisk!.Label;

            return System.IO.Path.GetFileName(_imagePath);
        }
    }

    /// <summary>
    /// Details of selected media shown in card, e.g. size, part and byteswap.
    /// </summary>
    public string Details
    {
        get
        {
            if (!IsSelected)
                return string.Empty;

            var details = new List<string>();
            if (IsImageFile)
            {
                details.Add(_imagePath);
                if (_media != null)
                    details.Add(MediaOptions.FormatBytes(_media.DiskSize));
            }

            if (Options.ShowByteswap && _byteswap)
                details.Add("Byteswap");
            return string.Join(" · ", details);
        }
    }

    public bool HasDetails => !string.IsNullOrEmpty(Details);

    public ReactiveCommand<Unit, Unit> RefreshMediaCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseCommand { get; }

    /// <summary>
    /// Edit media selection in media selection dialog.
    /// </summary>
    public ReactiveCommand<Unit, Unit> EditCommand { get; }

    /// <summary>
    /// Get physical disks and select first physical disk, if physical disk is selected type.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (CanListPhysicalDisks)
            await RefreshMediaAsync();
    }

    private bool CanListPhysicalDisks => Options.AllowPhysicalDisk && IsAdministrator;

    private async Task EditAsync()
    {
        var copy = new MediaSelectionViewModel(_mediaService, _dialogService, Options) { _isEditCopy = true };
        copy.CopyFrom(this);
        if (copy.CanListPhysicalDisks && copy.MediaItems.Count == 0)
            _ = copy.RefreshMediaAsync();

        if (!await _dialogService.ShowMediaSelectionDialogAsync(copy))
            return;

        CopyFrom(copy);

        // load media info, if media selection was changed in media selection dialog
        if (_isStale)
        {
            Media = null;
            UpdatePartOptions(null);
            await LoadMediaAsync();
        }

        Committed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Copy media selection without loading media info again, which would reset part selection.
    /// </summary>
    private void CopyFrom(MediaSelectionViewModel other)
    {
        _type = other._type;
        _imagePath = other._imagePath;
        _mediaItems = other._mediaItems;
        _selectedDisk = other._selectedDisk;
        _media = other._media;
        _isStale = other._isStale || other._isLoading;
        _size = other._size;
        _sizeUnit = other._sizeUnit;
        _sizeAlwaysEnabled = other._sizeAlwaysEnabled;
        _byteswap = other._byteswap;
        _errorMessage = other._errorMessage;
        _hasError = other._hasError;
        PartPath.CopyFrom(other.PartPath);

        this.RaisePropertyChanged(nameof(Type));
        this.RaisePropertyChanged(nameof(ImagePath));
        this.RaisePropertyChanged(nameof(MediaItems));
        UpdateMediaChoices();
        this.RaisePropertyChanged(nameof(SelectedDisk));
        this.RaisePropertyChanged(nameof(Media));
        this.RaisePropertyChanged(nameof(HasMedia));
        this.RaisePropertyChanged(nameof(Size));
        this.RaisePropertyChanged(nameof(SizeUnit));
        this.RaisePropertyChanged(nameof(SizeAlwaysEnabled));
        this.RaisePropertyChanged(nameof(Byteswap));
        this.RaisePropertyChanged(nameof(ErrorMessage));
        this.RaisePropertyChanged(nameof(HasError));
        RaiseTypeChanged();
        RaisePartChanged();
    }

    private void RaiseTypeChanged()
    {
        this.RaisePropertyChanged(nameof(IsImageFile));
        this.RaisePropertyChanged(nameof(IsPhysicalDisk));
        this.RaisePropertyChanged(nameof(TypeIcon));
        this.RaisePropertyChanged(nameof(SelectedChoice));
        this.RaisePropertyChanged(nameof(ShowImageFile));
        RaiseSelectionChanged();
    }

    private void UpdateMediaChoices()
    {
        MediaChoices = Options.AllowImageFile ? [.. _mediaItems, _imageFileChoice] : [.. _mediaItems];
        this.RaisePropertyChanged(nameof(SelectedChoice));
        RaiseMediaChoicesChanged();
    }

    private void RaiseMediaChoicesChanged()
    {
        this.RaisePropertyChanged(nameof(ShowMediaChoices));
        this.RaisePropertyChanged(nameof(ShowImageFile));
    }

    private void RaiseSelectionChanged()
    {
        this.RaisePropertyChanged(nameof(Path));
        this.RaisePropertyChanged(nameof(ResolvedPath));
        this.RaisePropertyChanged(nameof(IsSelected));
        this.RaisePropertyChanged(nameof(DisplayName));
        RaiseDetailsChanged();
    }

    private void RaisePartChanged()
    {
        this.RaisePropertyChanged(nameof(ShowPart));
        this.RaisePropertyChanged(nameof(ShowStartOffset));
        this.RaisePropertyChanged(nameof(ShowSize));
        this.RaisePropertyChanged(nameof(IsSizeEnabled));
        this.RaisePropertyChanged(nameof(ResolvedPath));
        RaiseDetailsChanged();
    }

    private void RaiseDetailsChanged()
    {
        this.RaisePropertyChanged(nameof(Details));
        this.RaisePropertyChanged(nameof(HasDetails));
    }

    private void RequestLoad()
    {
        if (!Options.LoadMedia) return;
        _isStale = true;

        // media info is loaded, when media selection is confirmed in media selection dialog
        if (_isEditCopy)
        {
            HasError = false;
            return;
        }

        _loadRequests.OnNext(Unit.Default);
    }

    private async Task RefreshMediaAsync()
    {
        try
        {
            IsLoadingMedia = true;
            HasError = false;
            var medias = await _mediaService.ListMediaAsync();
            MediaItems = new ObservableCollection<MediaItemViewModel>(medias.Select(m => new MediaItemViewModel
            {
                Path = m.Path, Name = m.Name, DiskSize = m.DiskSize, IsPhysicalDrive = m.IsPhysicalDrive,
                MediaInfo = m
            }));

            // keep selected physical disk, if it still exists
            var selected = _selectedDisk == null
                ? null
                : MediaItems.FirstOrDefault(x => x.Path == _selectedDisk.Path);
            if (IsImageFile && _selectedDisk == null && string.IsNullOrWhiteSpace(_imagePath) &&
                MediaItems.Count > 0)
            {
                // select first physical disk, if no image file or physical disk has been selected
                SelectedChoice = MediaItems[0];
            }
            else if (IsPhysicalDisk)
            {
                _selectedDisk = null;
                SelectedDisk = selected ?? MediaItems.FirstOrDefault();

                // select image file, if no physical disks exist and image file is allowed
                if (_selectedDisk == null && Options.AllowImageFile)
                    Type = TypeOptions.First(x => x.Value == MediaOptions.ImageFile);
            }
            else
            {
                // keep image file selected without loading media info again
                _selectedDisk = selected;
                this.RaisePropertyChanged(nameof(SelectedDisk));
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingMedia = false;
        }
    }

    private async Task BrowseAsync()
    {
        var path = Options.SaveImageFile
            ? await _dialogService.ShowSaveFileDialogAsync(Options.BrowseTitle, Options.FileFilters)
            : await _dialogService.ShowOpenFileDialogAsync(Options.BrowseTitle, Options.FileFilters);
        if (path != null)
            ImagePath = path;
    }

    private async Task LoadMediaAsync()
    {
        var path = Path;
        var byteswap = _byteswap;
        MediaInfo? media = null;
        HasError = false;
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                IsLoading = true;
                media = await _mediaService.GetMediaInfoAsync(path, byteswap, Options.AllowNonExisting);
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

        // ignore result, if selection was changed while loading
        if (path != Path || byteswap != _byteswap) return;

        _isStale = false;
        Media = media;
        UpdatePartOptions(media);
    }

    private void UpdatePartOptions(MediaInfo? media)
    {
        switch (Options.PartMode)
        {
            case MediaPartMode.PartPath:
                PartPath.Update(media, Options.IncludePartitions);
                break;
            case MediaPartMode.PiStormDisk:
                PartPath.SetOptions(media == null ? [] : GetPiStormDiskOptions(media));
                break;
        }

        RaisePartChanged();
    }

    /// <summary>
    /// Build PiStorm disk options from master boot record partitions with bios type 0x76 (118) and guid partition
    /// table partitions with partition type guid 3F82EEBC-87C9-4097-8165-89D6540557C0.
    /// </summary>
    private static List<SelectOption> GetPiStormDiskOptions(MediaInfo media)
    {
        var options = new List<SelectOption>
        {
            new() { Title = $"Disk ({MediaOptions.FormatBytes(media.DiskSize)})", Value = media.Path }
        };

        foreach (var tableType in new[] { PartitionTableType.MasterBootRecord, PartitionTableType.GuidPartitionTable })
        {
            var tableName = PartitionLayout.GetTableTypeAbbreviation(tableType);
            options.AddRange(DiskPartitionTables.GetPiStormParts(media, tableType)
                .Select(part => new SelectOption
                {
                    Title = $"{tableName} partition #{part.PartitionNumber}: {MediaOptions.FormatPartType(part)} ({MediaOptions.FormatBytes(part.Size)})",
                    Value = DiskPartitionTables.GetPiStormPath(media.Path, tableType, part.PartitionNumber!.Value)
                }));
        }

        return options;
    }
}
