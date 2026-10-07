using System;
using System.Collections.Generic;
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
    private readonly INavigationService _navigationService;

    private MediaInfo? _mediaInfo;
    private List<DiskPartitionTable> _tables = [];
    private ObservableCollection<PartitionSegmentViewModel> _segments = [];
    private PartitionSegmentViewModel? _selectedSegment;
    private ObservableCollection<DetailSectionBase> _detailSections = [];
    private bool _showHumanReadable = true;
    private bool _isDetailsExpanded;
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private bool _isLoading;

    public InfoViewModel(IMediaService mediaService, IDialogService dialogService,
        INavigationService navigationService)
    {
        _mediaService = mediaService;
        _navigationService = navigationService;

        // PiStorm disks in master boot record partitions can be selected to read their rigid disk block
        Source = new MediaSelectionViewModel(mediaService, dialogService, new MediaSelectionOptions
        {
            Title = "Disk",
            AllowPhysicalDisk = true,
            AllowNonExisting = true,
            PartMode = MediaPartMode.PiStormDisk,
            PartLabel = "PiStorm disk to read",
            ShowByteswap = true
        });
        Source.Committed += (_, _) => _ = GetInfoAsync();

        GetInfoCommand = ReactiveCommand.CreateFromTask(() => GetInfoAsync(reload: true),
            this.WhenAnyValue(x => x.Source.IsSelected));
        ResetCommand = ReactiveCommand.Create(() => _navigationService.NavigateTo("Info"));
    }

    public MediaSelectionViewModel Source { get; }

    public string SourceTypeFormatted => Source.IsImageFile ? "image file" : "physical disk";

    /// <summary>
    /// Segments of partition tables shown in partition layout bar and list same as partition view.
    /// </summary>
    public ObservableCollection<PartitionSegmentViewModel> Segments
    {
        get => _segments;
        set => this.RaiseAndSetIfChanged(ref _segments, value);
    }

    public PartitionSegmentViewModel? SelectedSegment
    {
        get => _selectedSegment;
        set => this.RaiseAndSetIfChanged(ref _selectedSegment, value);
    }

    /// <summary>
    /// Details of disk and partition tables read from disk shown in expandable details panel.
    /// </summary>
    public ObservableCollection<DetailSectionBase> DetailSections
    {
        get => _detailSections;
        set => this.RaiseAndSetIfChanged(ref _detailSections, value);
    }

    public bool ShowHumanReadable
    {
        get => _showHumanReadable;
        set
        {
            this.RaiseAndSetIfChanged(ref _showHumanReadable, value);
            BuildDetailSections();
        }
    }

    public bool IsDetailsExpanded
    {
        get => _isDetailsExpanded;
        set => this.RaiseAndSetIfChanged(ref _isDetailsExpanded, value);
    }

    public long DiskSize => _mediaInfo?.DiskSize ?? 0;

    public bool HasDiskInfo => _mediaInfo?.DiskInfo != null;

    /// <summary>
    /// Start and end cylinder columns are shown, when disk has a rigid disk block or PiStorm rigid disk block.
    /// </summary>
    public bool ShowCylinders => _tables.Any(x => x.Layout.IsRdb);

    public string SectorsOrCylindersText =>
        DiskPartitionTables.FormatSectorsOrCylinders(_tables.Select(x => x.Layout));

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

    public ReactiveCommand<Unit, Unit> GetInfoCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }

    private void ClearInfo() => SetInfo(null, []);

    private void SetInfo(MediaInfo? info, List<DiskPartitionTable> tables)
    {
        _mediaInfo = info;
        _tables = tables;
        Segments = new ObservableCollection<PartitionSegmentViewModel>(DiskPartitionTables.BuildSegments(tables));
        SelectedSegment = null;
        BuildDetailSections();
        this.RaisePropertyChanged(nameof(DiskSize));
        this.RaisePropertyChanged(nameof(HasDiskInfo));
        this.RaisePropertyChanged(nameof(ShowCylinders));
        this.RaisePropertyChanged(nameof(SectorsOrCylindersText));
        this.RaisePropertyChanged(nameof(SourceTypeFormatted));
    }

    /// <summary>
    /// Show info of selected source or PiStorm disk. Media info loaded by source is used, unless reloading or
    /// a PiStorm disk is selected.
    /// </summary>
    private async Task GetInfoAsync(bool reload = false)
    {
        ClearInfo();
        var path = Source.ResolvedPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        IsLoading = true;
        HasError = false;

        try
        {
            var media = !reload && Source.Media != null && path == Source.Path
                ? Source.Media
                : await _mediaService.GetMediaInfoAsync(path, Source.Byteswap, allowNonExisting: true);
            await ShowInfoAsync(media, path);
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

    private async Task ShowInfoAsync(MediaInfo? info, string path)
    {
        var tables = info?.DiskInfo != null
            ? await DiskPartitionTables.ReadAsync(_mediaService, info, Source.Byteswap, error =>
            {
                HasError = true;
                ErrorMessage = error;
            })
            : [];

        // ignore result, if path was changed while loading
        if (path != Source.ResolvedPath) return;

        SetInfo(info, tables);
    }

    // ─── Details ──────────────────────────────────────────────────────────────

    private void BuildDetailSections()
    {
        var diskInfo = _mediaInfo?.DiskInfo;
        if (diskInfo == null)
        {
            DetailSections = [];
            return;
        }

        var humanReadable = _showHumanReadable;
        var detailSections = new ObservableCollection<DetailSectionBase>
        {
            new DiskInfoDetailSection
            {
                Title = $"Disk: {diskInfo.Name}, {FormatSize(diskInfo.Size, humanReadable)}",
                Name = diskInfo.Name ?? string.Empty,
                Path = diskInfo.Path ?? string.Empty,
                Size = FormatSize(diskInfo.Size, humanReadable),
                IsSparseFile = diskInfo.IsSparseFile,
                SparseFileSize = diskInfo.IsSparseFile ? FormatSize(diskInfo.SparseFileSize, humanReadable) : string.Empty
            }
        };

        if (diskInfo.GptPartitionTablePart != null)
            AddGptSections(diskInfo.GptPartitionTablePart, detailSections, humanReadable);

        if (diskInfo.MbrPartitionTablePart != null)
            AddMbrSections(diskInfo.MbrPartitionTablePart, detailSections, humanReadable);

        if (diskInfo.RigidDiskBlock != null)
            AddRdbDetailSections(diskInfo.RigidDiskBlock, detailSections, humanReadable);

        DetailSections = detailSections;
    }

    private static void AddGptSections(
        PartitionTablePart gpt,
        ObservableCollection<DetailSectionBase> detailSections,
        bool humanReadable)
    {
        if (gpt.DiskGeometry != null)
            detailSections.Add(ToGeometrySection("Guid Partition Table: Geometry", gpt.DiskGeometry, humanReadable));

        var partitions = (gpt.Parts ?? []).Where(p => p.PartType == PartType.Partition).ToList();
        if (partitions.Count > 0)
        {
            var s = new GptPartitionsDetailSection { Title = "Guid Partition Table: Partitions" };
            foreach (var p in partitions)
                s.Rows.Add(new GptPartitionRow
                {
                    Number = p.PartitionNumber?.ToString() ?? string.Empty,
                    Guid = p.GuidType ?? string.Empty,
                    Type = p.PartitionType ?? string.Empty,
                    FileSystem = p.FileSystem ?? string.Empty,
                    Size = FormatSize(p.Size, humanReadable),
                    StartSector = p.StartSector.ToString(),
                    EndSector = p.EndSector.ToString()
                });
            detailSections.Add(s);
        }
    }

    private static void AddMbrSections(
        PartitionTablePart mbr,
        ObservableCollection<DetailSectionBase> detailSections,
        bool humanReadable)
    {
        if (mbr.DiskGeometry != null)
            detailSections.Add(ToGeometrySection("Master Boot Record: Geometry", mbr.DiskGeometry, humanReadable));

        var partitions = (mbr.Parts ?? []).Where(p => p.PartType == PartType.Partition).ToList();
        if (partitions.Count > 0)
        {
            var s = new MbrPartitionsDetailSection { Title = "Master Boot Record: Partitions" };
            foreach (var p in partitions)
            {
                var biosTypeInt = int.TryParse(p.BiosType, out var bt) ? bt : 0;
                s.Rows.Add(new MbrPartitionRow
                {
                    Number = p.PartitionNumber?.ToString() ?? string.Empty,
                    Id = $"0x{biosTypeInt:x}",
                    Type = p.PartitionType ?? string.Empty,
                    FileSystem = p.FileSystem ?? string.Empty,
                    Size = FormatSize(p.Size, humanReadable),
                    StartSector = p.StartSector.ToString(),
                    EndSector = p.EndSector.ToString(),
                    Active = p.IsActive ? "Yes" : "No",
                    Primary = p.IsPrimary ? "Yes" : "No"
                });
            }
            detailSections.Add(s);
        }
    }

    private static void AddRdbDetailSections(
        Hst.Amiga.RigidDiskBlocks.RigidDiskBlock rdb,
        ObservableCollection<DetailSectionBase> detailSections,
        bool humanReadable)
    {
        var flags = new List<string>();
        if ((rdb.Flags & 0x1) == 0x1) flags.Add("Last");
        if ((rdb.Flags & 0x2) == 0x2) flags.Add("LastLun");
        if ((rdb.Flags & 0x4) == 0x4) flags.Add("LastId");
        if ((rdb.Flags & 0x8) == 0x8) flags.Add("NoReSelect");
        if ((rdb.Flags & 0x10) == 0x10) flags.Add("DiskId");
        if ((rdb.Flags & 0x20) == 0x20) flags.Add("CtrlRId");
        if ((rdb.Flags & 0x40) == 0x40) flags.Add("Synch");

        detailSections.Add(new RdbInfoDetailSection
        {
            Title = $"Rigid Disk Block: {rdb.DiskProduct}, {FormatSize(rdb.DiskSize, humanReadable)}",
            Product = rdb.DiskProduct ?? string.Empty,
            Vendor = rdb.DiskVendor ?? string.Empty,
            Revision = rdb.DiskRevision ?? string.Empty,
            Size = FormatSize(rdb.DiskSize, humanReadable),
            Cylinders = rdb.Cylinders.ToString(),
            Heads = rdb.Heads.ToString(),
            Sectors = rdb.Sectors.ToString(),
            BlockSize = rdb.BlockSize.ToString(),
            StartCylinder = rdb.LoCylinder.ToString(),
            EndCylinder = rdb.HiCylinder.ToString(),
            Flags = flags.Count > 0 ? $"{rdb.Flags} ({string.Join(", ", flags)})" : rdb.Flags.ToString(),
            HostId = rdb.HostId.ToString(),
            RdbBlockLo = rdb.RdbBlockLo.ToString(),
            RdbBlockHi = rdb.RdbBlockHi.ToString()
        });

        if (rdb.FileSystemHeaderBlocks?.Any() == true)
        {
            var s = new RdbFileSystemsDetailSection { Title = "Rigid Disk Block: File systems" };
            var i = 1;
            foreach (var fs in rdb.FileSystemHeaderBlocks)
            {
                var size = fs.LoadSegBlocks?.Sum(x => x.Data?.Length ?? 0) ?? 0;
                s.Rows.Add(new RdbFileSystemRow
                {
                    Number = (i++).ToString(),
                    DosType = $"{fs.DosTypeHex} ({fs.DosTypeFormatted})",
                    Version = fs.VersionFormatted ?? string.Empty,
                    FileSystemName = fs.FileSystemName ?? string.Empty,
                    Size = FormatSize(size, humanReadable)
                });
            }
            detailSections.Add(s);
        }

        if (rdb.PartitionBlocks?.Any() == true)
        {
            var s = new RdbPartitionsDetailSection { Title = "Rigid Disk Block: Partitions" };
            var i = 1;
            foreach (var pb in rdb.PartitionBlocks)
                s.Rows.Add(new RdbPartitionRow
                {
                    Number = (i++).ToString(),
                    Name = pb.DriveName ?? string.Empty,
                    Size = FormatSize(pb.PartitionSize, humanReadable),
                    StartCyl = pb.LowCyl.ToString(),
                    EndCyl = pb.HighCyl.ToString(),
                    TotalCyl = (pb.HighCyl - pb.LowCyl + 1).ToString(),
                    Heads = pb.Surfaces.ToString(),
                    BlocksPerTrack = pb.BlocksPerTrack.ToString(),
                    Buffers = pb.NumBuffer.ToString(),
                    FsBlockSize = pb.FileSystemBlockSize.ToString(),
                    Reserved = pb.Reserved.ToString(),
                    PreAlloc = pb.PreAlloc.ToString(),
                    Bootable = pb.Bootable ? "Yes" : "No",
                    BootPriority = pb.BootPriority.ToString(),
                    NoMount = pb.NoMount ? "Yes" : "No",
                    DosType = $"{pb.DosTypeHex} ({pb.DosTypeFormatted})",
                    Mask = $"{pb.MaskHex} ({pb.Mask})",
                    MaxTransfer = $"{pb.MaxTransferHex} ({pb.MaxTransfer})"
                });
            detailSections.Add(s);
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static GeometryDetailSection ToGeometrySection(string title, DiskGeometry geom, bool humanReadable) =>
        new()
        {
            Title = title,
            Capacity = FormatSize(geom.Capacity, humanReadable),
            SectorSize = geom.BytesPerSector.ToString(),
            TotalSectors = geom.TotalSectors.ToString(),
            Cylinders = geom.Cylinders.ToString(),
            HeadsPerCylinder = geom.HeadsPerCylinder.ToString(),
            SectorsPerTrack = geom.SectorsPerTrack.ToString()
        };

    private static string FormatSize(long bytes, bool humanReadable) =>
        humanReadable ? ByteSize.FromBytes(bytes).Humanize("#.#") : bytes.ToString();
}
