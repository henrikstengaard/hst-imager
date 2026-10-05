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
        DeletePartitionCommand = ReactiveCommand.Create(DeletePartition, this.WhenAnyValue(x => x.IsPartitionSelected));
        EditPartitionCommand = ReactiveCommand.CreateFromTask(EditPartitionAsync, this.WhenAnyValue(x => x.IsPartitionSelected));
        EditFileSystemsCommand = ReactiveCommand.CreateFromTask(EditFileSystemsAsync, this.WhenAnyValue(x => x.IsRdb));
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
            if (rdbLayouts.Count == 0)
                return "Start and end in sectors of 512 bytes from start of disk. New partitions are aligned to 1 MB.";

            var cylinders = rdbLayouts.Select(x => x.Alignment).Distinct().Count() == 1
                ? $"cylinders of {MediaOptions.FormatBytes(rdbLayouts[0].Alignment)}"
                : "cylinders";
            var start = $"Start and end in sectors of 512 bytes from start of disk. Start and end cylinder of Rigid Disk Block partitions in {cylinders} from start of Rigid Disk Block.";
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
    public bool NeedsFastFileSystem => GetMissingDosTypes().Any(x => x is "DOS3" or "DOS7");
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
    public ReactiveCommand<Unit, Unit> DeletePartitionCommand { get; }

    /// <summary>
    /// Show partition dialog to edit selected partition.
    /// </summary>
    public ReactiveCommand<Unit, Unit> EditPartitionCommand { get; }

    /// <summary>
    /// Show file systems dialog to add, import, update, export and delete file systems in selected rigid disk block.
    /// </summary>
    public ReactiveCommand<Unit, Unit> EditFileSystemsCommand { get; }
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
    /// Read partition tables of disk and PiStorm disks in master boot record partitions with bios type 0x76 (118).
    /// PiStorm disks are read from their master boot record partition.
    /// </summary>
    private async Task<List<DiskPartitionTable>> ReadPartitionTablesAsync(MediaInfo? media)
    {
        if (media == null)
            return [];

        var diskInfo = media.DiskInfo;
        var tableTypes = PartitionLayout.GetPartitionTableTypes(diskInfo);

        // disk without partition tables is uninitialized, if first sectors only contain zeroes
        if (tableTypes.Count == 0)
        {
            var isBlank = false;
            try
            {
                isBlank = await _mediaService.IsBlankAsync(media.Path, Source.Byteswap);
            }
            catch (Exception)
            {
                // shown as no partition table, if first sectors can't be read
            }

            return [new DiskPartitionTable(PartitionLayout.CreateEmpty(media.DiskSize, isBlank), media.Path)];
        }

        var tables = tableTypes
            .Select(x => new DiskPartitionTable(PartitionLayout.FromMediaInfo(media, x), media.Path))
            .ToList();

        var mbrLayout = tables.FirstOrDefault(x => x.Layout.TableType == PartitionTableType.MasterBootRecord)?.Layout;
        if (mbrLayout == null)
            return tables;

        var separator = media.Path.StartsWith('/') ? "/" : "\\";
        foreach (var part in (diskInfo?.MbrPartitionTablePart?.Parts ?? [])
                 .Where(x => x.PartType == PartType.Partition && x.BiosType == "118"))
        {
            var container = mbrLayout.Partitions.FirstOrDefault(x => x.Number == part.PartitionNumber);
            if (container == null)
                continue;

            var path = string.Concat(media.Path, separator, "mbr", separator, part.PartitionNumber);
            MediaInfo? piStormMedia;
            try
            {
                piStormMedia = await _mediaService.GetMediaInfoAsync(path, Source.Byteswap);
            }
            catch (Exception ex)
            {
                // disk is still partitioned without PiStorm disk, which can't be read
                HasError = true;
                ErrorMessage = $"Failed to read PiStorm Rigid Disk Block in Master Boot Record partition #{part.PartitionNumber}: {ex.Message}";
                continue;
            }

            if (piStormMedia == null)
                continue;

            tables.Add(new DiskPartitionTable(
                PartitionLayout.FromMediaInfo(piStormMedia, PartitionTableType.RigidDiskBlock), path)
            {
                Container = container,
                ContainerLayout = mbrLayout
            });
        }

        return tables;
    }

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
            mbrLayout.ReserveRigidDiskBlock(dialog.RdbSize);
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

        SetSelectedSegment(partitionSegment);

        var sizeUnit = SizeUnit;
        bool added;
        try
        {
            IsAddingPartition = true;
            added = await _dialogService.ShowPartitionDialogAsync(this);
        }
        finally
        {
            IsAddingPartition = false;
        }

        if (added)
            return;

        layout.DeletePartition(partition);
        SizeUnit = sizeUnit;
        var unallocated = Segments.FirstOrDefault(x => x.IsUnallocated && ReferenceEquals(x.Layout, layout) &&
                                                       x.Start <= segment.Start && x.End >= segment.End);
        if (unallocated != null)
            SetSelectedSegment(unallocated);
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

        if (await _dialogService.ShowPartitionDialogAsync(this))
            return;

        layout.RestorePartition(partition, state);
        SizeUnit = sizeUnit;
    }

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
        var segments = new List<PartitionSegmentViewModel>();
        foreach (var table in ActiveTables.Where(x => x.IsDiskPath))
            segments.AddRange(table.Layout.BuildSegments().Where(x => !x.IsReserved));

        foreach (var table in ActiveTables.Where(x => !x.IsDiskPath))
        {
            var container = segments.FirstOrDefault(x => ReferenceEquals(x.Partition, table.Container));
            if (container == null)
                continue;
            container.NestedLayout = table.Layout;
            segments.AddRange(table.Layout.BuildSegments(table.Offset, 1).Where(x => !x.IsReserved));
        }

        Segments = new ObservableCollection<PartitionSegmentViewModel>(
            segments.OrderBy(x => x.DiskStart).ThenBy(x => x.Depth));

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
        RaiseEditorChanged();
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
            errors.Add("Path to media with FastFileSystem is required for DOS\\3 and DOS\\7 partitions");

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
        RaiseEditorChanged();
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

        foreach (var partition in layout.Partitions.Where(x => x.IsNew))
            operations.Add(partition.IsPiStorm
                ? $"Add {layout.TableTypeName} PiStorm partition of {MediaOptions.FormatBytes(partition.Size)}"
                : $"Add {layout.TableTypeName} partition{FormatDeviceName(partition)} of {MediaOptions.FormatBytes(partition.Size)} formatted with {PartitionFileSystems.GetTitle(partition.FileSystem)} named '{partition.Label}'{location}");

        foreach (var partition in layout.Partitions.Where(x => x.IsExisting && x.FormatRequested))
            operations.Add(
                $"Format {layout.TableTypeName} partition #{partition.Number}{FormatDeviceName(partition)} with {partition.FileSystemDisplay} named '{partition.Label}'{(partition.RequiresTypeChange ? " and change partition type" : string.Empty)}{location}");

        AddFileSystemOperations(layout, location, operations);
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
            operations.Add(fileSystem.IsFromMedia
                ? $"Import file system '{fileSystem.Name}' with DOS type {fileSystem.DosType} from '{fileSystem.Path}' to {layout.TableTypeName}{location}"
                : $"Add file system '{fileSystem.Name}' with DOS type {fileSystem.DosType} from file '{fileSystem.Path}' to {layout.TableTypeName}{location}");
    }

    private void Validate(DiskPartitionTable table, List<string> errors)
    {
        var layout = table.Layout;
        var location = table.IsDiskPath ? string.Empty : $" in {table.ContainerName}";
        var editedPartitions = layout.Partitions.Where(x => (x.IsNew && !x.IsPiStorm) || x.FormatRequested).ToList();

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

        foreach (var partition in layout.Partitions.Where(x => x.IsNew))
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
