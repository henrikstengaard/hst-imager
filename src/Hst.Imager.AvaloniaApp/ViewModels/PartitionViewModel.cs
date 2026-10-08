using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.Core.Commands;
using ReactiveUI;
using Unit = System.Reactive.Unit;

namespace Hst.Imager.AvaloniaApp.ViewModels;

/// <summary>
/// Partition view model to initialize partition table, add, delete, resize and format partitions of a physical disk
/// or an image file. Changes are pending until applied. Disks with multiple partition tables like hybrid disks and
/// PiStorm disks are edited as one layout with partitions of all partition tables.
/// </summary>
public class PartitionViewModel : ViewModelBase
{
    private const string Pfs3AioUrl = "https://aminet.net/disk/misc/pfs3aio.lha";

    public static readonly string[] SizeUnits = ["MB", "GB"];

    private readonly IMediaService _mediaService;
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;

    private MediaInfo? _media;
    private bool _isLoading;
    private string _errorMessage = string.Empty;
    private bool _hasError;

    // partition tables of disk edited as one, e.g. rigid disk block and master boot record of a hybrid disk
    private List<DiskPartitionTable> _tables = [];
    private IReadOnlyList<PartitionTableSummary> _partitionTables = [];

    // layout of selected segment
    private PartitionLayout? _layout;
    private ObservableCollection<PartitionSegmentViewModel> _segments = [];
    private PartitionSegmentViewModel? _selectedSegment;
    private PartitionEntryViewModel? _selectedPartition;
    private string _sizeUnit = SizeUnits[0];
    private bool _isAddingPartition;

    // file systems imported for partition in partition dialog, which are removed again, if dialog is cancelled
    private readonly List<RdbFileSystemEntry> _dialogFileSystems = [];

    private bool _downloadPfs3Aio = true;
    private string _pfs3FileSystemPath = string.Empty;
    private string _fastFileSystemPath = string.Empty;
    private bool _useExperimental;

    private ObservableCollection<string> _pendingOperations = [];
    private ObservableCollection<string> _validationErrors = [];

    public PartitionViewModel(IMediaService mediaService, IImagingService imagingService,
        IDialogService dialogService, ProgressViewModel progress)
    {
        _mediaService = mediaService;
        _imagingService = imagingService;
        _dialogService = dialogService;

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
        Source.Committed += (_, _) => _ = LoadAsync(Source.Path, Source.Media);

        BrowsePfs3FileSystemPathCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var path = await BrowseFileSystemPathAsync("Select media with pfs3aio file system");
            if (path != null) Pfs3FileSystemPath = path;
        });
        BrowseFastFileSystemPathCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var path = await BrowseFileSystemPathAsync("Select media with FastFileSystem");
            if (path != null) FastFileSystemPath = path;
        });
        InitializeCommand = ReactiveCommand.CreateFromTask(InitializeAsync, this.WhenAnyValue(x => x.HasMedia));
        AddPartitionCommand = ReactiveCommand.CreateFromTask(AddPartitionAsync, this.WhenAnyValue(x => x.CanAddPartition));
        AddPartitionToCommand = ReactiveCommand.CreateFromTask<AddPartitionRequest>(AddPartitionToAsync);
        ClonePartitionCommand = ReactiveCommand.CreateFromTask(ClonePartitionAsync,
            this.WhenAnyValue(x => x.CanClonePartition));
        DeletePartitionCommand = ReactiveCommand.Create(DeletePartition, this.WhenAnyValue(x => x.IsPartitionSelected));
        EditPartitionCommand = ReactiveCommand.CreateFromTask(EditPartitionAsync, this.WhenAnyValue(x => x.IsPartitionSelected));
        EditFileSystemsCommand = ReactiveCommand.CreateFromTask(EditFileSystemsAsync, this.WhenAnyValue(x => x.IsRdb));
        ImportFileSystemFromMediaCommand = ReactiveCommand.CreateFromTask(() => ImportFileSystemAsync(true),
            this.WhenAnyValue(x => x.CanImportFileSystem));
        AddFileSystemFromFileCommand = ReactiveCommand.CreateFromTask(() => ImportFileSystemAsync(false),
            this.WhenAnyValue(x => x.CanImportFileSystem));
        RemoveImportedFileSystemCommand = ReactiveCommand.Create(RemoveImportedFileSystem,
            this.WhenAnyValue(x => x.IsImportedFileSystemSelected));
        ResetCommand = ReactiveCommand.CreateFromTask(ResetAsync, this.WhenAnyValue(x => x.HasMedia));
        ApplyCommand = ReactiveCommand.CreateFromTask(ApplyAsync,
            this.WhenAnyValue(x => x.CanApply, x => x.Progress.IsRunning, (canApply, running) => canApply && !running));
    }

    public ProgressViewModel Progress { get; }

    // ─── Source ───────────────────────────────────────────────────────────────

    public MediaSelectionViewModel Source { get; }

    public bool HasMedia => _media != null;

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
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

    // ─── Layout ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Partition tables edited, which excludes partition tables in deleted or formatted partitions.
    /// </summary>
    private IEnumerable<DiskPartitionTable> ActiveTables => _tables.Where(x => x.IsActive);

    /// <summary>
    /// Partition tables in order changes are applied. Partition tables in master boot record partitions are applied
    /// first, as partition numbers in their paths can change by changes to master boot record. Master boot record is
    /// applied before rigid disk block, as rigid disk block initialized for a hybrid disk keeps master boot record.
    /// Partition tables in new PiStorm partitions are applied last, when their partitions are added.
    /// </summary>
    private IEnumerable<DiskPartitionTable> TablesInApplyOrder => ActiveTables
        .OrderBy(x => x.IsInNewPartition ? 3 : !x.IsDiskPath ? 0 : x.Layout.IsRdb ? 2 : 1);

    private IEnumerable<PartitionLayout> RdbLayouts => ActiveTables.Select(x => x.Layout).Where(x => x.IsRdb);

    private DiskPartitionTable? FindTable(PartitionLayout? layout) =>
        _tables.FirstOrDefault(x => ReferenceEquals(x.Layout, layout));

    /// <summary>
    /// Master boot record of disk, which can be kept when initializing rigid disk block for a hybrid disk.
    /// </summary>
    private DiskPartitionTable? MasterBootRecordTable =>
        _tables.FirstOrDefault(x => x.IsDiskPath && x.Layout.TableType == PartitionTableType.MasterBootRecord);

    public bool HasLayout => _tables.Count > 0;

    public long DiskSize => _media?.DiskSize ?? 0;

    public bool HasNoPartitionTable => _tables is [{ IsDiskPath: true, Layout.HasPartitionTable: false }];

    public string NoPartitionTableText => _tables is [{ Layout.IsBlank: true }]
        ? "Disk is uninitialized with zeroes in first sectors used by partition tables. Initialize a partition table to add partitions."
        : "No partition table found. Initialize a partition table to add partitions.";

    /// <summary>
    /// Partition tables of disk with their size, e.g. rigid disk block and master boot record for a hybrid disk.
    /// </summary>
    public IReadOnlyList<PartitionTableSummary> PartitionTables
    {
        get => _partitionTables;
        private set => this.RaiseAndSetIfChanged(ref _partitionTables, value);
    }

    /// <summary>
    /// Build partition tables in order they are placed on disk. Rigid disk block is placed before master boot record
    /// partitions in a hybrid disk and PiStorm rigid disk blocks are in master boot record partitions.
    /// Size of rigid disk block is the cylinders it uses, other partition tables use the whole disk.
    /// </summary>
    private List<PartitionTableSummary> BuildPartitionTableSummaries() => ActiveTables
        .OrderBy(x => x.IsDiskPath ? 0 : 1)
        .ThenBy(x => x.Layout.IsRdb ? 0 : 1)
        .ThenBy(x => x.Offset)
        .Select(x =>
        {
            var layout = x.Layout;
            var name = layout.HasPartitionTable
                ? $"{FormatTableType(layout.TableType)}{(layout.IsInitialize ? " (new)" : string.Empty)}"
                : layout.IsBlank ? "Uninitialized" : "No partition table";
            return new PartitionTableSummary
            {
                Name = x.IsDiskPath ? name : $"PiStorm {name} in {x.ContainerName}",
                Abbreviation = PartitionLayout.GetTableTypeAbbreviation(layout.TableType),
                SizeText = MediaOptions.FormatBytes(layout.TableSize),
                Color = PartitionLayout.GetTableTypeColor(layout.TableType)
            };
        })
        .ToList();

    public string SectorsOrCylindersText
    {
        get
        {
            var layouts = ActiveTables.Select(x => x.Layout).Where(x => x.HasPartitionTable).ToList();
            var rdbLayouts = layouts.Where(x => x.IsRdb).ToList();
            var start = DiskPartitionTables.FormatSectorsOrCylinders(layouts);
            if (rdbLayouts.Count == 0)
                return $"{start} New partitions are aligned to 1 MB.";

            return rdbLayouts.Count == layouts.Count
                ? $"{start} New partitions are aligned to cylinders."
                : $"{start} New partitions are aligned to 1 MB and to cylinders for Rigid Disk Block partitions.";
        }
    }

    /// <summary>
    /// Start and end cylinder columns are shown, when disk has a rigid disk block or PiStorm rigid disk block.
    /// </summary>
    public bool ShowCylinders => RdbLayouts.Any();

    public ObservableCollection<PartitionSegmentViewModel> Segments
    {
        get => _segments;
        private set => this.RaiseAndSetIfChanged(ref _segments, value);
    }

    /// <summary>
    /// Selected partition or unallocated space. Null selections are ignored, as list view clears selection when
    /// segments are rebuilt after changes.
    /// </summary>
    public PartitionSegmentViewModel? SelectedSegment
    {
        get => _selectedSegment;
        set
        {
            if (value == null) return;
            SetSelectedSegment(value);
        }
    }

    public PartitionEntryViewModel? SelectedPartition
    {
        get => _selectedPartition;
        private set => this.RaiseAndSetIfChanged(ref _selectedPartition, value);
    }

    public bool IsPartitionSelected => _selectedPartition != null;
    public bool IsNewPartitionSelected => _selectedPartition is { IsNew: true };
    public bool IsExistingPartitionSelected => _selectedPartition is { IsNew: false };
    public bool IsUnallocatedSelected => _selectedSegment is { IsUnallocated: true } && _layout is { HasPartitionTable: true };
    public bool IsRdb => _layout is { IsRdb: true };
    public bool IsMbr => _layout is { TableType: PartitionTableType.MasterBootRecord };

    public bool CanAddPartition => IsUnallocatedSelected && _layout!.CanAddPartitionTo(_selectedSegment);

    /// <summary>
    /// Text for adding partition, which indicates partition table partition is added to.
    /// </summary>
    public string AddPartitionText => _layout is { HasPartitionTable: true }
        ? $"Add {_layout.TableTypeName} partition"
        : "Add partition";

    /// <summary>
    /// Tooltip for adding partition, which shows max partitions when no more partitions can be added.
    /// </summary>
    public string AddPartitionHint => _layout is { HasPartitionTable: true } && !_layout.CanAddPartition
        ? $"Max {_layout.MaxPartitions} partitions can be added to {FormatTableType(_layout.TableType)}."
        : $"{AddPartitionText} in selected unallocated space";

    /// <summary>
    /// Selected partition can be cloned to unallocated space with room for it.
    /// </summary>
    public bool CanClonePartition => _layout != null && _layout.CanClonePartition(_selectedPartition);

    /// <summary>
    /// Tooltip for cloning partition, which shows why selected partition can't be cloned.
    /// </summary>
    public string ClonePartitionHint
    {
        get
        {
            if (_layout == null || _selectedPartition is not { } partition)
                return "Clone selected partition to a new partition with same partition type, file system and size";
            if (!_layout.CanAddPartition)
                return $"Max {_layout.MaxPartitions} partitions can be added to {FormatTableType(_layout.TableType)}.";
            return CanClonePartition
                ? "Clone selected partition to a new partition with same partition type, file system and size. Data isn't copied"
                : $"Unallocated space of {MediaOptions.FormatBytes(partition.Size)} is required to clone selected partition.";
        }
    }

    /// <summary>
    /// Partition dialog is shown for a partition being added, which is removed again, if dialog is cancelled.
    /// </summary>
    public bool IsAddingPartition
    {
        get => _isAddingPartition;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isAddingPartition, value);
            this.RaisePropertyChanged(nameof(PartitionDialogOkText));
            this.RaisePropertyChanged(nameof(PartitionDialogHelpText));
        }
    }

    public string PartitionDialogOkText => _isAddingPartition ? "Add" : "OK";

    public string PartitionDialogHelpText => _isAddingPartition
        ? "Partition is added as a pending operation when Add is clicked, which is applied when Apply is clicked."
        : "Changes are added as pending operations when OK is clicked, which are applied when Apply is clicked.";

    // ─── Rigid disk block file system of partition ────────────────────────────

    /// <summary>
    /// File system is shown in partition dialog for new rigid disk block partitions.
    /// </summary>
    public bool ShowRdbFileSystem => _selectedPartition is { IsNew: true, IsRdb: true };

    /// <summary>
    /// File system in rigid disk block with dos type of selected new rigid disk block partition.
    /// </summary>
    public RdbFileSystemEntry? SelectedPartitionFileSystem => ShowRdbFileSystem
        ? _layout?.FindFileSystem(_selectedPartition!.PartitionType)
        : null;

    /// <summary>
    /// File system of selected partition is imported in partition dialog, which can be removed again.
    /// </summary>
    public bool IsImportedFileSystemSelected =>
        SelectedPartitionFileSystem is { } fileSystem && _dialogFileSystems.Contains(fileSystem);

    /// <summary>
    /// Name of file system imported in partition dialog from media is used to find it in media.
    /// </summary>
    public bool IsImportedFileSystemFromMedia => IsImportedFileSystemSelected && SelectedPartitionFileSystem!.IsFromMedia;

    /// <summary>
    /// File system can be imported for selected partition, when rigid disk block doesn't have a file system with
    /// dos type of partition.
    /// </summary>
    public bool CanImportFileSystem => ShowRdbFileSystem && SelectedPartitionFileSystem == null &&
                                       !_selectedPartition!.HasPartitionTypeError;

    /// <summary>
    /// Rigid disk block doesn't have a file system with dos type of selected partition, which isn't added
    /// automatically like pfs3aio and FastFileSystem.
    /// </summary>
    public bool IsRdbFileSystemMissing => CanImportFileSystem &&
                                          !PartitionFileSystems.IsPfs3(_selectedPartition!.FileSystem) &&
                                          !PartitionFileSystems.IsFastFileSystem(_selectedPartition.FileSystem);

    public string RdbFileSystemText
    {
        get
        {
            if (!ShowRdbFileSystem || _selectedPartition!.HasPartitionTypeError)
                return string.Empty;

            var dosType = PartitionFileSystems.FormatDosType(_selectedPartition.PartitionType);
            if (SelectedPartitionFileSystem is { } fileSystem)
                return fileSystem.Source switch
                {
                    RdbFileSystemSource.Existing =>
                        $"Partition uses file system {PartitionLayout.FormatFileSystemName(fileSystem)} with DOS type {dosType}.",
                    RdbFileSystemSource.Media =>
                        $"File system with DOS type {dosType} is imported from media '{fileSystem.Path}' to Rigid Disk Block, when applied.",
                    RdbFileSystemSource.Clone =>
                        $"File system with DOS type {dosType} is cloned from file system #{fileSystem.CloneNumber} in Rigid Disk Block, when applied.",
                    _ =>
                        $"File system with DOS type {dosType} is added from file '{fileSystem.Path}' to Rigid Disk Block, when applied."
                };

            if (PartitionFileSystems.IsPfs3(_selectedPartition.FileSystem))
                return $"Rigid Disk Block doesn't have a file system with DOS type {dosType}. pfs3aio is added as set in Amiga file systems below partition layout, when applied. Import a file system to use another file system.";
            if (PartitionFileSystems.IsFastFileSystem(_selectedPartition.FileSystem))
                return $"Rigid Disk Block doesn't have a file system with DOS type {dosType}. FastFileSystem is added from media set in Amiga file systems below partition layout, when applied. Import a file system to use another file system.";
            return $"Rigid Disk Block doesn't have a file system with DOS type {dosType}. Import a file system from media like lha, adf or iso or add it from a file system file.";
        }
    }

    // ─── Size editor ──────────────────────────────────────────────────────────

    public string SizeUnit
    {
        get => _sizeUnit;
        set
        {
            this.RaiseAndSetIfChanged(ref _sizeUnit, value);
            RaiseEditorChanged();
        }
    }

    private long UnitSize => _sizeUnit == "GB" ? 1024L * 1024 * 1024 : 1024L * 1024;

    public string EditorSize
    {
        get => _selectedPartition == null ? string.Empty : FormatUnitValue(_selectedPartition.Size);
        set => EditSize(value, (layout, partition, bytes) => layout.SetSize(partition, bytes));
    }

    public string EditorFreeSpacePreceding
    {
        get
        {
            if (_selectedPartition == null || _layout == null) return string.Empty;
            var (lower, _) = _layout.GetBounds(_selectedPartition);
            return FormatUnitValue(Math.Max(0, _selectedPartition.Start - lower));
        }
        set => EditSize(value, (layout, partition, bytes) => layout.SetFreeSpacePreceding(partition, bytes));
    }

    public string EditorFreeSpaceFollowing
    {
        get
        {
            if (_selectedPartition == null || _layout == null) return string.Empty;
            var (_, upper) = _layout.GetBounds(_selectedPartition);
            return FormatUnitValue(Math.Max(0, upper - _selectedPartition.End));
        }
        set => EditSize(value, (layout, partition, bytes) => layout.SetFreeSpaceFollowing(partition, bytes));
    }

    public string EditorMaxSizeText
    {
        get
        {
            if (_selectedPartition == null || _layout == null) return string.Empty;
            var (lower, upper) = _layout.GetBounds(_selectedPartition);
            var maxSize = Math.Min(upper - _layout.AlignUp(lower),
                _layout.GetMaxPartitionSize(_selectedPartition.FileSystem, _useExperimental));
            return $"Min size {MediaOptions.FormatBytes(_layout.MinPartitionSize)}, max size {MediaOptions.FormatBytes(maxSize)}.";
        }
    }

    public string SelectedPartitionTitle => _selectedPartition == null
        ? string.Empty
        : _selectedPartition.IsNew
            ? $"New {_layout?.TableTypeName} partition{FormatContainer(_layout)}"
            : $"{_layout?.TableTypeName} partition #{_selectedPartition.Number}{FormatContainer(_layout)}";

    // ─── Amiga file systems ───────────────────────────────────────────────────

    public bool NeedsPfs3FileSystem => GetMissingDosTypes().Any(x => x is "PFS3" or "PDS3");
    public bool NeedsFastFileSystem => GetMissingDosTypes().Any(PartitionFileSystems.IsFastFileSystem);
    public bool ShowPfs3FileSystemPath => NeedsPfs3FileSystem && !_downloadPfs3Aio;
    public bool ShowAmigaFileSystems => RdbLayouts.Any();
    public bool ShowUseExperimental => RdbLayouts.Any(layout =>
        layout.Partitions.Any(x => x.IsNew && PartitionFileSystems.IsPfs3(x.FileSystem)));

    public bool DownloadPfs3Aio
    {
        get => _downloadPfs3Aio;
        set
        {
            this.RaiseAndSetIfChanged(ref _downloadPfs3Aio, value);
            this.RaisePropertyChanged(nameof(ShowPfs3FileSystemPath));
            UpdatePending();
        }
    }

    public string Pfs3FileSystemPath
    {
        get => _pfs3FileSystemPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _pfs3FileSystemPath, value);
            UpdatePending();
        }
    }

    public string FastFileSystemPath
    {
        get => _fastFileSystemPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _fastFileSystemPath, value);
            UpdatePending();
        }
    }

    public bool UseExperimental
    {
        get => _useExperimental;
        set
        {
            this.RaiseAndSetIfChanged(ref _useExperimental, value);
            this.RaisePropertyChanged(nameof(EditorMaxSizeText));
            UpdatePending();
        }
    }

    // ─── Pending operations ───────────────────────────────────────────────────

    public ObservableCollection<string> PendingOperations
    {
        get => _pendingOperations;
        private set => this.RaiseAndSetIfChanged(ref _pendingOperations, value);
    }

    public ObservableCollection<string> ValidationErrors
    {
        get => _validationErrors;
        private set => this.RaiseAndSetIfChanged(ref _validationErrors, value);
    }

    public bool HasPendingOperations => _pendingOperations.Count > 0;
    public bool HasValidationErrors => _validationErrors.Count > 0;
    public bool CanApply => HasPendingOperations && !HasValidationErrors;

    public ReactiveCommand<Unit, Unit> BrowsePfs3FileSystemPathCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseFastFileSystemPathCommand { get; }
    public ReactiveCommand<Unit, Unit> InitializeCommand { get; }
    public ReactiveCommand<Unit, Unit> AddPartitionCommand { get; }

    /// <summary>
    /// Add partition to start, all or end of unallocated space clicked in visual view.
    /// </summary>
    public ReactiveCommand<AddPartitionRequest, Unit> AddPartitionToCommand { get; }

    /// <summary>
    /// Clone selected partition to a new partition with same partition type, file system and size.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ClonePartitionCommand { get; }
    public ReactiveCommand<Unit, Unit> DeletePartitionCommand { get; }

    /// <summary>
    /// Show partition dialog to edit selected partition.
    /// </summary>
    public ReactiveCommand<Unit, Unit> EditPartitionCommand { get; }

    /// <summary>
    /// Show file systems dialog to add, import, update, export and delete file systems in selected rigid disk block.
    /// </summary>
    public ReactiveCommand<Unit, Unit> EditFileSystemsCommand { get; }

    /// <summary>
    /// Import file system with dos type of selected partition from media in partition dialog.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ImportFileSystemFromMediaCommand { get; }

    /// <summary>
    /// Add file system with dos type of selected partition from a file system file in partition dialog.
    /// </summary>
    public ReactiveCommand<Unit, Unit> AddFileSystemFromFileCommand { get; }

    /// <summary>
    /// Remove file system imported in partition dialog.
    /// </summary>
    public ReactiveCommand<Unit, Unit> RemoveImportedFileSystemCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }
    public ReactiveCommand<Unit, Unit> ApplyCommand { get; }

    // ─── Loading ──────────────────────────────────────────────────────────────

    private async Task<string?> BrowseFileSystemPathAsync(string title) =>
        await _dialogService.ShowOpenFileDialogAsync(title,
        [
            new FileFilterItem { Name = "File system and media files", Extensions = ["lha", "adf", "iso", "hdf", "img"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ]);

    /// <summary>
    /// Read partition tables again discarding pending operations.
    /// </summary>
    private Task ResetAsync() => LoadAsync(Source.Path);

    /// <summary>
    /// Load media info and partition tables of path. Media info loaded by source is used, when it's available.
    /// </summary>
    private async Task LoadAsync(string? path, MediaInfo? loadedMedia = null)
    {
        HasError = false;
        List<DiskPartitionTable> tables = [];
        MediaInfo? media = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                IsLoading = true;
                media = loadedMedia ?? await _mediaService.GetMediaInfoAsync(path, Source.Byteswap);
                tables = await ReadPartitionTablesAsync(media);
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                media = null;
                tables = [];
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ignore result, if path was changed while loading
        if (path != Source.Path) return;

        _media = media;
        this.RaisePropertyChanged(nameof(HasMedia));
        this.RaisePropertyChanged(nameof(DiskSize));
        SetTables(tables);
    }

    /// <summary>
    /// Read partition tables of disk and PiStorm disks in master boot record partitions.
    /// </summary>
    private Task<List<DiskPartitionTable>> ReadPartitionTablesAsync(MediaInfo? media) =>
        DiskPartitionTables.ReadAsync(_mediaService, media, Source.Byteswap, error =>
        {
            HasError = true;
            ErrorMessage = error;
        });

    /// <summary>
    /// Set partition tables edited and select first segment of layout.
    /// </summary>
    private void SetTables(List<DiskPartitionTable> tables, PartitionLayout? selectLayout = null)
    {
        foreach (var table in _tables)
            table.Layout.Changed -= OnLayoutChanged;

        _tables = tables;

        foreach (var table in _tables)
            table.Layout.Changed += OnLayoutChanged;

        SyncNewPiStormTables();
        _layout = null;
        _selectedSegment = null;
        SelectedPartition = null;

        this.RaisePropertyChanged(nameof(HasLayout));
        this.RaisePropertyChanged(nameof(HasNoPartitionTable));
        this.RaisePropertyChanged(nameof(NoPartitionTableText));

        RebuildSegments();
        var segment = Segments.FirstOrDefault(x => ReferenceEquals(x.Layout, selectLayout) && !x.IsContainer &&
                                                   !x.IsPartitionTable) ??
                      Segments.FirstOrDefault(x => !x.IsContainer && !x.IsPartitionTable) ?? Segments.FirstOrDefault();
        if (segment != null)
            SetSelectedSegment(segment);
        UpdatePending();
    }

    // ─── Changes ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Show initialize dialog to initialize a new partition table for disk or a PiStorm disk.
    /// </summary>
    private async Task InitializeAsync()
    {
        if (_media == null)
            return;

        var targets = new List<InitializeTarget>
        {
            new() { Title = $"Disk ({MediaOptions.FormatBytes(_media.DiskSize)})", DiskSize = _media.DiskSize }
        };
        targets.AddRange(ActiveTables.Where(x => !x.IsDiskPath).Select(x => new InitializeTarget
        {
            Title = $"PiStorm Rigid Disk Block in {x.ContainerName} ({MediaOptions.FormatBytes(x.Layout.DiskSize)})",
            Table = x,
            DiskSize = x.Layout.DiskSize
        }));

        var selectedTable = FindTable(_layout);
        var selectedTarget = targets.FirstOrDefault(x => x.Table != null && ReferenceEquals(x.Table, selectedTable)) ??
                             targets[0];
        var tableType = selectedTable is { IsDiskPath: true, Layout.HasPartitionTable: true }
            ? selectedTable.Layout.TableType
            : PartitionTableType.MasterBootRecord;
        var mbrLayout = MasterBootRecordTable?.Layout;

        var dialog = new InitializePartitionTableViewModel(targets, selectedTarget, tableType,
            mbrLayout?.GetPartitionAreas().ToList());
        if (!await _dialogService.ShowInitializePartitionTableDialogAsync(dialog) || !dialog.CanInitialize)
            return;

        var rdbBlockLo = (int)dialog.RdbBlockLo;

        // PiStorm disk is initialized with rigid disk block keeping other partition tables
        if (dialog.SelectedTarget.Table is { } piStormTable)
        {
            var piStormLayout = PartitionLayout.CreateNewRdb(piStormTable.Layout.DiskSize, dialog.RdbSize, rdbBlockLo,
                null);
            SetTables(_tables.Select(x => ReferenceEquals(x, piStormTable) ? x with { Layout = piStormLayout } : x)
                .ToList(), piStormLayout);
            return;
        }

        // rigid disk block is initialized keeping master boot record, its pending changes and PiStorm disks in its
        // partitions. master boot record partitions are added after rigid disk block
        if (dialog.IsKeepingMasterBootRecord && mbrLayout != null)
        {
            var rdbLayout = PartitionLayout.CreateNewRdb(_media.DiskSize, dialog.RdbSize, rdbBlockLo,
                mbrLayout.GetPartitionAreas().ToList());
            mbrLayout.ReserveRigidDiskBlock(dialog.RdbSize, rdbBlockLo);
            SetTables(new[] { new DiskPartitionTable(rdbLayout, _media.Path) }
                .Concat(_tables.Where(x => ReferenceEquals(x.Layout, mbrLayout) || !x.IsDiskPath))
                .ToList(), rdbLayout);
            return;
        }

        // new partition table replaces all partition tables of disk
        var layout = dialog.TableType == PartitionTableType.RigidDiskBlock
            ? PartitionLayout.CreateNewRdb(_media.DiskSize, dialog.RdbSize, rdbBlockLo, null)
            : PartitionLayout.CreateNew(dialog.TableType, _media.DiskSize);
        SetTables([new DiskPartitionTable(layout, _media.Path)], layout);
    }

    private Task AddPartitionAsync() => AddPartitionAsync(AddPartitionPlacement.All);

    /// <summary>
    /// Add partition in selected unallocated space and show partition dialog to change partition type, file system,
    /// names and size before adding it. Partition is removed again and unallocated space is selected, if dialog is
    /// cancelled.
    /// </summary>
    private async Task AddPartitionAsync(AddPartitionPlacement placement)
    {
        if (_layout == null || _selectedSegment is not { IsUnallocated: true } segment)
            return;

        var layout = _layout;
        var partition = layout.AddPartition(segment.Start, segment.End, _useExperimental, placement);
        var partitionSegment = Segments.FirstOrDefault(x => ReferenceEquals(x.Partition, partition));
        if (partition == null || partitionSegment == null)
            return;

        if (await ShowNewPartitionDialogAsync(layout, partition, partitionSegment))
            return;

        var unallocated = Segments.FirstOrDefault(x => x.IsUnallocated && ReferenceEquals(x.Layout, layout) &&
                                                       x.Start <= segment.Start && x.End >= segment.End);
        if (unallocated != null)
            SetSelectedSegment(unallocated);
    }

    /// <summary>
    /// Clone selected partition to a new partition with same partition type, file system and size in unallocated space
    /// and show partition dialog to change it before adding it. Only layout details are cloned, not data. Clone is
    /// removed again and partition cloned is selected, if dialog is cancelled.
    /// </summary>
    private async Task ClonePartitionAsync()
    {
        if (_layout == null || _selectedSegment is not { Partition: { } source } sourceSegment)
            return;

        var layout = _layout;
        var clone = layout.ClonePartition(source);
        var cloneSegment = Segments.FirstOrDefault(x => ReferenceEquals(x.Partition, clone));
        if (clone == null || cloneSegment == null)
            return;

        if (await ShowNewPartitionDialogAsync(layout, clone, cloneSegment))
            return;

        SetSelectedSegment(Segments.FirstOrDefault(x => ReferenceEquals(x.Partition, source)) ?? sourceSegment);
    }

    /// <summary>
    /// Select new partition and show partition dialog for it. Partition is deleted again, if dialog is cancelled.
    /// Returns true, if Add is clicked.
    /// </summary>
    private async Task<bool> ShowNewPartitionDialogAsync(PartitionLayout layout, PartitionEntryViewModel partition,
        PartitionSegmentViewModel segment)
    {
        SetSelectedSegment(segment);

        var sizeUnit = SizeUnit;
        bool added;
        try
        {
            IsAddingPartition = true;
            added = await ShowPartitionDialogAsync(layout, partition);
        }
        finally
        {
            IsAddingPartition = false;
        }

        if (added)
            return true;

        layout.DeletePartition(partition);
        SizeUnit = sizeUnit;
        return false;
    }

    private async Task AddPartitionToAsync(AddPartitionRequest request)
    {
        if (!request.Segment.Layout.CanAddPartitionTo(request.Segment, request.Placement))
            return;

        SetSelectedSegment(request.Segment);
        await AddPartitionAsync(request.Placement);
    }

    private void DeletePartition()
    {
        if (_layout == null || _selectedPartition == null || _selectedSegment == null)
            return;

        var deleted = _selectedSegment;
        var index = Segments.IndexOf(deleted);
        _layout.DeletePartition(_selectedPartition);

        // select unallocated space where partition was
        var segment = Segments.FirstOrDefault(x => x.IsUnallocated && ReferenceEquals(x.Layout, deleted.Layout) &&
                                                   x.Start <= deleted.Start && x.End >= deleted.End)
                      ?? Segments.ElementAtOrDefault(Math.Min(index, Segments.Count - 1));
        if (segment != null)
            SetSelectedSegment(segment);
    }

    /// <summary>
    /// Edit selected partition in partition dialog. Changes are made directly to partition while editing, so state of
    /// partition is restored, if dialog is cancelled.
    /// </summary>
    private async Task EditPartitionAsync()
    {
        if (_layout == null || _selectedPartition == null)
            return;

        var layout = _layout;
        var partition = _selectedPartition;
        var state = partition.GetState();
        var sizeUnit = SizeUnit;

        if (await ShowPartitionDialogAsync(layout, partition))
            return;

        layout.RestorePartition(partition, state);
        SizeUnit = sizeUnit;
    }

    /// <summary>
    /// Show partition dialog for partition. File systems imported in dialog are removed again, if dialog is cancelled
    /// or they aren't used by partition, when dialog is closed. Returns true, if OK or Add is clicked.
    /// </summary>
    private async Task<bool> ShowPartitionDialogAsync(PartitionLayout layout, PartitionEntryViewModel partition)
    {
        if (partition is { IsNew: true, IsRdb: true })
            partition.PartitionTypeOptions = layout.GetDosTypeOptions();

        _dialogFileSystems.Clear();
        bool ok;
        try
        {
            ok = await _dialogService.ShowPartitionDialogAsync(this);
        }
        finally
        {
            var unused = _dialogFileSystems.Where(x => !string.Equals(x.DosType, partition.PartitionType,
                StringComparison.OrdinalIgnoreCase)).ToList();
            _dialogFileSystems.Clear();
            foreach (var fileSystem in unused)
                layout.RemoveFileSystem(fileSystem);
        }

        return ok;
    }

    /// <summary>
    /// Import file system with dos type of selected new rigid disk block partition from media or add it from a file
    /// system file. File system is added to rigid disk block as a pending operation.
    /// </summary>
    private async Task ImportFileSystemAsync(bool fromMedia)
    {
        if (_layout is not { IsRdb: true } layout || _selectedPartition is not { IsNew: true } partition)
            return;

        var path = fromMedia
            ? await _dialogService.ShowOpenFileDialogAsync("Select media with file system",
            [
                new FileFilterItem { Name = "Media files", Extensions = ["lha", "adf", "iso", "hdf", "img"] },
                new FileFilterItem { Name = "All files", Extensions = ["*"] }
            ])
            : await _dialogService.ShowOpenFileDialogAsync("Select file system file",
                [new FileFilterItem { Name = "All files", Extensions = ["*"] }]);
        if (path == null || layout.FindFileSystem(partition.PartitionType) != null)
            return;

        var fileSystem = new RdbFileSystemEntry(fromMedia ? RdbFileSystemSource.Media : RdbFileSystemSource.File)
        {
            DosType = partition.PartitionType,
            Name = fromMedia ? GuessFileSystemName(partition.FileSystem) : System.IO.Path.GetFileName(path),
            Path = path
        };
        fileSystem.PropertyChanged += (_, _) => UpdatePending();
        _dialogFileSystems.Add(fileSystem);
        layout.AddFileSystem(fileSystem);
        partition.PartitionTypeOptions = layout.GetDosTypeOptions();
    }

    private void RemoveImportedFileSystem()
    {
        if (_layout == null || _selectedPartition == null || SelectedPartitionFileSystem is not { } fileSystem ||
            !_dialogFileSystems.Remove(fileSystem))
            return;

        _layout.RemoveFileSystem(fileSystem);
        _selectedPartition.PartitionTypeOptions = _layout.GetDosTypeOptions();
    }

    /// <summary>
    /// Guess name of file system to find in media by dos type, e.g. pfs3aio for PDS3.
    /// </summary>
    private static string GuessFileSystemName(string fileSystem) =>
        PartitionFileSystems.IsPfs3(fileSystem)
            ? "pfs3aio"
            : PartitionFileSystems.IsFastFileSystem(fileSystem)
                ? "FastFileSystem"
                : fileSystem.StartsWith("sfs", StringComparison.OrdinalIgnoreCase)
                    ? "SmartFilesystem"
                    : string.Empty;

    /// <summary>
    /// Edit file systems of selected rigid disk block in file systems dialog. File systems are edited as copies, which
    /// are set as pending operations, when dialog is closed with OK. Existing file systems are exported from rigid
    /// disk block on disk, which isn't available for new rigid disk blocks.
    /// </summary>
    private async Task EditFileSystemsAsync()
    {
        if (_layout is not { IsRdb: true } layout || FindTable(layout) is not { } table)
            return;

        var byteswap = Source.Byteswap;
        Func<int, string, Task>? export = layout.IsInitialize || table.IsInNewPartition
            ? null
            : (number, path) => _imagingService.ExportRdbFileSystemAsync(table.Path, byteswap, number, path,
                CancellationToken.None);
        var dialog = new RdbFileSystemsViewModel($"File systems in {layout.TableTypeName}{FormatContainer(layout)}",
            layout, _dialogService, _imagingService, export);
        if (await _dialogService.ShowRdbFileSystemsDialogAsync(dialog))
            dialog.Apply();
    }

    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        SyncNewPiStormTables();
        RebuildSegments();
        UpdatePending();
    }

    /// <summary>
    /// Add new rigid disk block for new master boot record partitions switched to PiStorm partitions and initialize
    /// rigid disk block again with new partitions kept, when PiStorm partition is resized. Rigid disk blocks of
    /// partitions switched back to regular partitions are kept inactive, so switching again restores them.
    /// </summary>
    private void SyncNewPiStormTables()
    {
        if (_media == null || MasterBootRecordTable?.Layout is not { } mbrLayout)
            return;

        for (var i = 0; i < _tables.Count; i++)
        {
            var table = _tables[i];
            if (!table.IsInNewPartition || table.Layout.DiskSize == table.Container!.Size)
                continue;

            var layout = PartitionLayout.CreateNewRdb(table.Container.Size, table.Layout.RdbSize,
                table.Layout.RdbBlockLo, null);
            layout.AdoptPartitions(table.Layout);
            table.Layout.Changed -= OnLayoutChanged;
            layout.Changed += OnLayoutChanged;
            _tables[i] = table with { Layout = layout };
        }

        foreach (var partition in mbrLayout.Partitions.Where(x => x.IsPiStorm &&
                                                                  _tables.All(t => !ReferenceEquals(t.Container, x))))
        {
            var layout = PartitionLayout.CreateNewRdb(partition.Size, 0, 0, null);
            layout.Changed += OnLayoutChanged;
            _tables.Add(new DiskPartitionTable(layout, _media.Path)
            {
                Container = partition,
                ContainerLayout = mbrLayout
            });
        }
    }

    /// <summary>
    /// Rebuild segments of all partition tables keeping selection of partition or unallocated space. Areas reserved
    /// by other partition tables are left out, as they are shown by the other partition table. Partition tables in
    /// partitions are nested in the partition containing them.
    /// </summary>
    private void RebuildSegments()
    {
        var previous = _selectedSegment;
        Segments = new ObservableCollection<PartitionSegmentViewModel>(DiskPartitionTables.BuildSegments(ActiveTables));

        PartitionTables = BuildPartitionTableSummaries();
        this.RaisePropertyChanged(nameof(SectorsOrCylindersText));
        this.RaisePropertyChanged(nameof(ShowCylinders));
        this.RaisePropertyChanged(nameof(ShowAmigaFileSystems));

        if (previous == null)
            return;

        var segment = previous.Partition != null
            ? Segments.FirstOrDefault(x => ReferenceEquals(x.Partition, previous.Partition))
            : Segments.FirstOrDefault(x => x.IsUnallocated == previous.IsUnallocated &&
                                           x.IsPartitionTable == previous.IsPartitionTable &&
                                           ReferenceEquals(x.Layout, previous.Layout) &&
                                           x.Start < previous.End && x.End > previous.Start);
        if (segment != null)
            SetSelectedSegment(segment);
        else
            this.RaisePropertyChanged(nameof(SelectedSegment));
    }

    private void SetSelectedSegment(PartitionSegmentViewModel segment)
    {
        _selectedSegment = segment;
        _layout = segment.Layout;
        this.RaisePropertyChanged(nameof(SelectedSegment));
        SelectedPartition = segment.Partition;

        this.RaisePropertyChanged(nameof(IsPartitionSelected));
        this.RaisePropertyChanged(nameof(IsNewPartitionSelected));
        this.RaisePropertyChanged(nameof(IsExistingPartitionSelected));
        this.RaisePropertyChanged(nameof(IsUnallocatedSelected));
        this.RaisePropertyChanged(nameof(IsRdb));
        this.RaisePropertyChanged(nameof(IsMbr));
        this.RaisePropertyChanged(nameof(SelectedPartitionTitle));
        this.RaisePropertyChanged(nameof(CanAddPartition));
        this.RaisePropertyChanged(nameof(AddPartitionText));
        this.RaisePropertyChanged(nameof(AddPartitionHint));
        this.RaisePropertyChanged(nameof(CanClonePartition));
        this.RaisePropertyChanged(nameof(ClonePartitionHint));
        RaiseEditorChanged();
        RaiseFileSystemChanged();
    }

    private void RaiseFileSystemChanged()
    {
        this.RaisePropertyChanged(nameof(ShowRdbFileSystem));
        this.RaisePropertyChanged(nameof(SelectedPartitionFileSystem));
        this.RaisePropertyChanged(nameof(IsImportedFileSystemSelected));
        this.RaisePropertyChanged(nameof(IsImportedFileSystemFromMedia));
        this.RaisePropertyChanged(nameof(CanImportFileSystem));
        this.RaisePropertyChanged(nameof(IsRdbFileSystemMissing));
        this.RaisePropertyChanged(nameof(RdbFileSystemText));
    }

    private void RaiseEditorChanged()
    {
        this.RaisePropertyChanged(nameof(EditorSize));
        this.RaisePropertyChanged(nameof(EditorFreeSpacePreceding));
        this.RaisePropertyChanged(nameof(EditorFreeSpaceFollowing));
        this.RaisePropertyChanged(nameof(EditorMaxSizeText));
    }

    private void EditSize(string value, Action<PartitionLayout, PartitionEntryViewModel, long> edit)
    {
        if (_layout != null && _selectedPartition != null && TryParseUnitValue(value, out var bytes))
            edit(_layout, _selectedPartition, bytes);

        // raise changes to show aligned and limited values or reset invalid values
        RaiseEditorChanged();
    }

    private string FormatUnitValue(long bytes) =>
        ((decimal)bytes / UnitSize).ToString("0.##", CultureInfo.CurrentCulture);

    private bool TryParseUnitValue(string value, out long bytes)
    {
        bytes = 0;
        if (!TryParseNumber(value, out var number))
            return false;
        bytes = (long)(number * UnitSize);
        return true;
    }

    private static bool TryParseNumber(string value, out decimal number) =>
        (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out number) ||
         decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number)) && number >= 0;

    // ─── Pending operations and validation ────────────────────────────────────

    private IEnumerable<string> GetMissingDosTypes() => RdbLayouts
        .SelectMany(layout => layout.Partitions.Where(x => x.IsNew)
            .Select(x => x.FileSystem.ToUpperInvariant())
            .Where(x => !layout.FileSystemDosTypes.Contains(x)))
        .Distinct();

    private void UpdatePending()
    {
        var operations = new List<string>();
        var errors = new List<string>();

        foreach (var table in TablesInApplyOrder)
        {
            AddOperations(table, operations);
            Validate(table, errors);
        }

        if (NeedsPfs3FileSystem && !_downloadPfs3Aio && string.IsNullOrWhiteSpace(_pfs3FileSystemPath))
            errors.Add("Path to media with pfs3aio file system is required for PFS\\3 and PDS\\3 partitions");

        if (NeedsFastFileSystem && string.IsNullOrWhiteSpace(_fastFileSystemPath))
            errors.Add("Path to media with FastFileSystem is required for DOS\\0 to DOS\\7 partitions");

        PendingOperations = new ObservableCollection<string>(operations);
        ValidationErrors = new ObservableCollection<string>(errors);
        this.RaisePropertyChanged(nameof(HasPendingOperations));
        this.RaisePropertyChanged(nameof(HasValidationErrors));
        this.RaisePropertyChanged(nameof(CanApply));
        this.RaisePropertyChanged(nameof(NeedsPfs3FileSystem));
        this.RaisePropertyChanged(nameof(NeedsFastFileSystem));
        this.RaisePropertyChanged(nameof(ShowPfs3FileSystemPath));
        this.RaisePropertyChanged(nameof(ShowUseExperimental));
        this.RaisePropertyChanged(nameof(CanAddPartition));
        this.RaisePropertyChanged(nameof(AddPartitionHint));
        this.RaisePropertyChanged(nameof(AddPartitionText));
        this.RaisePropertyChanged(nameof(CanClonePartition));
        this.RaisePropertyChanged(nameof(ClonePartitionHint));
        RaiseEditorChanged();
        RaiseFileSystemChanged();
    }

    private static void AddOperations(DiskPartitionTable table, List<string> operations)
    {
        var layout = table.Layout;
        var location = table.IsDiskPath ? string.Empty : $" in {table.ContainerName}";

        if (layout.IsInitialize)
            operations.Add(layout.IsRdb
                ? $"Initialize Rigid Disk Block{(layout.RdbSize > 0 ? $" of {MediaOptions.FormatBytes(layout.RdbSize)}" : string.Empty)} at sector {layout.RdbBlockLo}{location}{(layout.KeepMasterBootRecord ? ", keeping Master Boot Record and its partitions" : ", which erases existing partitions")}"
                : $"Initialize {FormatTableType(layout.TableType)}, which erases existing partition tables and partitions");

        foreach (var partition in layout.DeletedPartitions)
            operations.Add(
                $"Delete {layout.TableTypeName} partition #{partition.Number}{FormatDeviceName(partition)} ({partition.ExistingFileSystem}, {MediaOptions.FormatBytes(partition.Size)}){location}");

        foreach (var partition in layout.Partitions)
        {
            if (partition.GetRdbUpdate() is { } update)
                operations.Add(
                    $"Update {layout.TableTypeName} partition #{partition.Number} {partition.ExistingDeviceName} {string.Join(", ", FormatRdbUpdate(update))}{location}");
        }

        foreach (var partition in layout.Partitions.Where(x => x.IsNew))
        {
            var type = layout.IsRdb
                ? $"DOS type {PartitionFileSystems.FormatDosType(partition.PartitionType)}"
                : $"partition type {PartitionTypes.GetTitle(layout.TableType, partition.PartitionType)}";
            var format = !partition.IsFormattable
                ? " not formatted"
                : layout.IsRdb
                    ? $" formatted named '{partition.Label}'"
                    : $" formatted with {PartitionFileSystems.GetTitle(partition.FileSystem)} named '{partition.Label}'";
            operations.Add(partition.IsPiStorm
                ? $"Add {layout.TableTypeName} PiStorm partition of {MediaOptions.FormatBytes(partition.Size)}"
                : $"Add {layout.TableTypeName} partition{FormatDeviceName(partition)} of {MediaOptions.FormatBytes(partition.Size)} with {type}{format}{location}");
        }

        foreach (var partition in layout.Partitions.Where(x => x.IsExisting && x.FormatRequested))
            operations.Add(
                $"Format {layout.TableTypeName} partition #{partition.Number}{FormatDeviceName(partition)} with {partition.FileSystemDisplay} named '{partition.Label}'{(partition.RequiresTypeChange ? " and change partition type" : string.Empty)}{location}");

        AddFileSystemOperations(layout, location, operations);
    }

    /// <summary>
    /// Format changes of rigid disk block partition update, e.g. "buffers to 50".
    /// </summary>
    private static IEnumerable<string> FormatRdbUpdate(PlannedPartitionUpdate update)
    {
        if (update.DeviceName != null)
            yield return $"device name to {update.DeviceName}";
        if (update.Bootable.HasValue)
            yield return update.Bootable.Value ? "bootable" : "not bootable";
        if (update.BootPriority.HasValue)
            yield return $"boot priority to {update.BootPriority}";
        if (update.NoMount.HasValue)
            yield return update.NoMount.Value ? "no mount" : "mount";
        if (update.Buffers.HasValue)
            yield return $"buffers to {update.Buffers}";
        if (update.MaxTransfer.HasValue)
            yield return $"max transfer to 0x{update.MaxTransfer:X}";
        if (update.Mask.HasValue)
            yield return $"mask to 0x{update.Mask:X}";
        if (update.Reserved.HasValue)
            yield return $"reserved blocks to {update.Reserved}";
        if (update.PreAlloc.HasValue)
            yield return $"pre alloc blocks to {update.PreAlloc}";
        if (update.FileSystemBlockSize.HasValue)
            yield return $"file system block size to {update.FileSystemBlockSize}";
    }

    /// <summary>
    /// Add operations for file systems in rigid disk block in order they are applied.
    /// </summary>
    private static void AddFileSystemOperations(PartitionLayout layout, string location, List<string> operations)
    {
        foreach (var fileSystem in layout.FileSystems.Where(x => x.IsUpdated))
        {
            var changes = new List<string>();
            if (fileSystem.IsDosTypeChanged)
                changes.Add($"DOS type to {fileSystem.DosType}, which also changes DOS type of partitions using it");
            if (fileSystem.IsNameChanged)
                changes.Add($"name to '{fileSystem.Name}'");
            if (fileSystem.IsDataReplaced)
                changes.Add($"data with file '{fileSystem.Path}'");
            operations.Add(
                $"Update {layout.TableTypeName} file system #{fileSystem.Number} ({fileSystem.OriginalDosType}) {string.Join(", ", changes)}{location}");
        }

        var newDosTypes = layout.FileSystems.Where(x => x.IsNew).Select(x => x.DosType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var fileSystem in layout.DeletedFileSystems)
            operations.Add(newDosTypes.Contains(fileSystem.OriginalDosType)
                ? $"Replace {layout.TableTypeName} file system #{fileSystem.Number} ({fileSystem.OriginalDosType}) '{fileSystem.OriginalName}' with new file system{location}"
                : $"Delete {layout.TableTypeName} file system #{fileSystem.Number} ({fileSystem.OriginalDosType}) '{fileSystem.OriginalName}'{location}");

        foreach (var fileSystem in layout.FileSystems.Where(x => x.IsNew))
            operations.Add(fileSystem.Source switch
            {
                RdbFileSystemSource.Media =>
                    $"Import file system '{fileSystem.Name}' with DOS type {fileSystem.DosType} from '{fileSystem.Path}' to {layout.TableTypeName}{location}",
                RdbFileSystemSource.Clone =>
                    $"Clone {layout.TableTypeName} file system #{fileSystem.CloneNumber} to file system '{fileSystem.Name}' with DOS type {fileSystem.DosType}{location}",
                _ =>
                    $"Add file system '{fileSystem.Name}' with DOS type {fileSystem.DosType} from file '{fileSystem.Path}' to {layout.TableTypeName}{location}"
            });
    }

    private void Validate(DiskPartitionTable table, List<string> errors)
    {
        var layout = table.Layout;
        var location = table.IsDiskPath ? string.Empty : $" in {table.ContainerName}";
        var editedPartitions = layout.Partitions.Where(x => (x.IsNew && x.IsFormattable) || x.FormatRequested).ToList();

        foreach (var partition in layout.Partitions.Where(x => x.PartitionTypeError != null))
            errors.Add($"{partition.PartitionTypeError}{location}");

        if (!layout.IsRdb)
        {
            foreach (var partition in editedPartitions)
            {
                var maxLength = partition.FileSystem == "ntfs" ? 32 : 11;
                if (partition.Label.Length > maxLength)
                    errors.Add(
                        $"Name '{partition.Label}' is longer than {maxLength} characters supported by {PartitionFileSystems.GetTitle(partition.FileSystem)}");

                // partition is added again to change type, which is limited to 100 sectors before end of disk
                if (partition.RequiresTypeChange && partition.End > (layout.DiskSize / 512 - 100) * 512)
                    errors.Add(
                        $"Partition #{partition.Number} can't be formatted with {PartitionFileSystems.GetTitle(partition.FileSystem)}, as changing its partition type requires it to end 100 sectors before end of disk. Delete and add the partition instead");
            }

            return;
        }

        errors.AddRange(layout.ValidateFileSystems().Select(x => $"{x}{location}"));

        // pfs3aio and FastFileSystem are added automatically, other file systems must be imported
        foreach (var partition in layout.Partitions.Where(x => x.IsNew && x.PartitionTypeError == null &&
                                                              !PartitionFileSystems.IsPfs3(x.FileSystem) &&
                                                              !PartitionFileSystems.IsFastFileSystem(x.FileSystem) &&
                                                              layout.FindFileSystem(x.PartitionType) == null))
            errors.Add(
                $"File system with DOS type {PartitionFileSystems.FormatDosType(partition.PartitionType)} is required for partition {partition.DeviceName}. Import it in partition dialog or file systems dialog{location}");

        foreach (var partition in layout.Partitions.Where(x => x.RdbPropertiesError != null))
            errors.Add(
                $"{partition.RdbPropertiesError} for partition {partition.DeviceName}{location}");

        // existing partitions are renamed one at a time, so a partition can't be renamed to device name of another
        // existing partition, which is renamed too
        foreach (var partition in layout.Partitions.Where(x => x.GetRdbUpdate() is { DeviceName: not null }))
        {
            var other = layout.Partitions.FirstOrDefault(x => !ReferenceEquals(x, partition) &&
                                                              x.GetRdbUpdate() is { DeviceName: not null } &&
                                                              string.Equals(x.ExistingDeviceName, partition.DeviceName,
                                                                  StringComparison.OrdinalIgnoreCase));
            if (other != null)
                errors.Add(
                    $"Partition #{partition.Number} can't be renamed to device name '{partition.DeviceName}' used by partition #{other.Number}, before partition #{other.Number} is renamed. Apply renaming partition #{other.Number} first{location}");
        }

        foreach (var partition in layout.Partitions.Where(x => x.IsNew || x.GetRdbUpdate() is { DeviceName: not null }))
        {
            if (string.IsNullOrWhiteSpace(partition.DeviceName))
                errors.Add($"Device name is required for all partitions{location}");
            else if (partition.DeviceName.Any(c => char.IsWhiteSpace(c) || c is ':' or '/'))
                errors.Add($"Device name '{partition.DeviceName}' can't contain spaces, ':' or '/'");

            if (PartitionFileSystems.IsPfs3(partition.FileSystem) && !_useExperimental &&
                partition.Size > layout.Pfs3MaxPartitionSize)
                errors.Add(
                    $"Partition {partition.DeviceName} is larger than PFS3 max partition size of {MediaOptions.FormatBytes(layout.Pfs3MaxPartitionSize)}. Reduce size or use PFS3 experimental partition sizes");
        }

        foreach (var deviceName in layout.Partitions
                     .Where(x => !string.IsNullOrWhiteSpace(x.DeviceName))
                     .GroupBy(x => x.DeviceName, StringComparer.OrdinalIgnoreCase)
                     .Where(x => x.Count() > 1)
                     .Select(x => x.Key))
            errors.Add($"Device name '{deviceName}' is used by more than one partition{location}");

        foreach (var partition in editedPartitions.Where(x => string.IsNullOrWhiteSpace(x.Label)))
            errors.Add($"Volume name is required for partition {partition.DeviceName}{location}");
    }

    // ─── Apply ────────────────────────────────────────────────────────────────

    private PartitionPlan BuildPlan(DiskPartitionTable table)
    {
        var plan = table.Layout.CreatePlan(table.Path);
        plan.IsDiskPath = table.IsDiskPath;
        plan.ContainerStartOffset = table.IsInNewPartition ? table.Container!.Start : null;
        plan.Byteswap = Source.Byteswap;
        plan.Pfs3FileSystemPath = _downloadPfs3Aio ? Pfs3AioUrl : _pfs3FileSystemPath;
        plan.FastFileSystemPath = _fastFileSystemPath;
        plan.UseExperimental = _useExperimental;
        return plan;
    }

    private async Task ApplyAsync()
    {
        if (_media == null)
            return;

        var plans = TablesInApplyOrder.Select(BuildPlan).Where(x => x.HasChanges).ToList();
        if (plans.Count == 0)
            return;

        var sourceTypeFormatted = Source.IsImageFile ? "image file" : "physical disk";
        var name = _media.Name ?? _media.Path;
        if (!await _dialogService.ShowConfirmDialogAsync("Partition",
                $"Do you want to apply {_pendingOperations.Count} pending operation(s) to {sourceTypeFormatted} '{name}'? Data on initialized disk, deleted and formatted partitions will be lost."))
            return;

        await Progress.RunAsync($"Partitioning {sourceTypeFormatted} '{name}'", async (progress, token) =>
        {
            foreach (var plan in plans)
            {
                token.ThrowIfCancellationRequested();
                await _imagingService.PartitionAsync(plan, progress, token);
            }
        });

        await LoadAsync(Source.Path);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Format partition containing partition table of layout, e.g. " in Master Boot Record partition #2" for a
    /// PiStorm rigid disk block.
    /// </summary>
    private string FormatContainer(PartitionLayout? layout) =>
        FindTable(layout) is { IsDiskPath: false } table ? $" in {table.ContainerName}" : string.Empty;

    private static string FormatDeviceName(PartitionEntryViewModel partition) =>
        partition.IsRdb && !string.IsNullOrWhiteSpace(partition.DeviceName) ? $" {partition.DeviceName}" : string.Empty;

    private static string FormatTableType(PartitionTableType tableType) => tableType switch
    {
        PartitionTableType.MasterBootRecord => "Master Boot Record",
        PartitionTableType.GuidPartitionTable => "Guid Partition Table",
        PartitionTableType.RigidDiskBlock => "Rigid Disk Block",
        _ => "No partition table"
    };
}
