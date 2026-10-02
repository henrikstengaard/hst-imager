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

    private bool _showUnallocated = true;
    private bool _showHumanReadable = true;
    private MediaInfo? _mediaInfo;
    private ObservableCollection<OverviewSectionViewModel> _overviewSections = [];
    private ObservableCollection<DetailSectionBase> _detailSections = [];
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

    public bool ShowUnallocated
    {
        get => _showUnallocated;
        set
        {
            this.RaiseAndSetIfChanged(ref _showUnallocated, value);
            if (_mediaInfo?.DiskInfo != null)
                BuildSections(_mediaInfo.DiskInfo, _showHumanReadable, value);
        }
    }

    public bool ShowHumanReadable
    {
        get => _showHumanReadable;
        set
        {
            this.RaiseAndSetIfChanged(ref _showHumanReadable, value);
            if (_mediaInfo?.DiskInfo != null)
                BuildSections(_mediaInfo.DiskInfo, value, _showUnallocated);
        }
    }

    public ObservableCollection<OverviewSectionViewModel> OverviewSections
    {
        get => _overviewSections;
        set => this.RaiseAndSetIfChanged(ref _overviewSections, value);
    }

    public ObservableCollection<DetailSectionBase> DetailSections
    {
        get => _detailSections;
        set => this.RaiseAndSetIfChanged(ref _detailSections, value);
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

    public ReactiveCommand<Unit, Unit> GetInfoCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }

    private void ClearInfo()
    {
        _mediaInfo = null;
        OverviewSections = [];
        DetailSections = [];
        this.RaisePropertyChanged(nameof(HasDiskInfo));
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

        if (!reload && Source.Media != null && path == Source.Path)
        {
            HasError = false;
            ShowInfo(Source.Media);
            return;
        }

        IsLoading = true;
        HasError = false;

        try
        {
            ShowInfo(await _mediaService.GetMediaInfoAsync(path, Source.Byteswap, allowNonExisting: true));
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

    private void ShowInfo(MediaInfo? info)
    {
        _mediaInfo = info;
        this.RaisePropertyChanged(nameof(HasDiskInfo));

        if (info?.DiskInfo != null)
            BuildSections(info.DiskInfo, _showHumanReadable, _showUnallocated);
    }

    // ─── Section building ─────────────────────────────────────────────────────

    private void BuildSections(DiskInfo diskInfo, bool humanReadable, bool showUnallocated)
    {
        var overviewSections = new ObservableCollection<OverviewSectionViewModel>();
        var detailSections = new ObservableCollection<DetailSectionBase>();

        // Disk overview
        if (diskInfo.DiskParts != null)
        {
            var sec = new OverviewSectionViewModel
            {
                Title = $"Disk: {diskInfo.Name}, {FormatSize(diskInfo.Size, humanReadable)}{SparseLabel(diskInfo, humanReadable)}",
                IsRdb = false
            };
            foreach (var p in diskInfo.DiskParts.Where(p => showUnallocated || p.PartType != PartType.Unallocated))
                sec.Parts.Add(ToPartOverviewRow(p, isRdb: false, humanReadable));
            overviewSections.Add(sec);
        }

        // Disk info details
        detailSections.Add(new DiskInfoDetailSection
        {
            Title = $"Disk: {diskInfo.Name}, {FormatSize(diskInfo.Size, humanReadable)}",
            Name = diskInfo.Name ?? string.Empty,
            Path = diskInfo.Path ?? string.Empty,
            Size = FormatSize(diskInfo.Size, humanReadable),
            IsSparseFile = diskInfo.IsSparseFile,
            SparseFileSize = diskInfo.IsSparseFile ? FormatSize(diskInfo.SparseFileSize, humanReadable) : string.Empty
        });

        // GPT
        if (diskInfo.GptPartitionTablePart != null)
            AddGptSections(diskInfo.GptPartitionTablePart, overviewSections, detailSections, humanReadable, showUnallocated);

        // MBR
        if (diskInfo.MbrPartitionTablePart != null)
            AddMbrSections(diskInfo.MbrPartitionTablePart, overviewSections, detailSections, humanReadable, showUnallocated);

        // RDB overview (parts from PartitionTablePart)
        if (diskInfo.RdbPartitionTablePart != null)
        {
            var sec = new OverviewSectionViewModel
            {
                Title = $"Rigid Disk Block: {FormatSize(diskInfo.RdbPartitionTablePart.Size, humanReadable)}",
                IsRdb = true
            };
            foreach (var p in (diskInfo.RdbPartitionTablePart.Parts ?? []).Where(p => showUnallocated || p.PartType != PartType.Unallocated))
                sec.Parts.Add(ToPartOverviewRow(p, isRdb: true, humanReadable));
            overviewSections.Add(sec);
        }

        // RDB details (from raw RigidDiskBlock)
        if (diskInfo.RigidDiskBlock != null)
            AddRdbDetailSections(diskInfo.RigidDiskBlock, detailSections, humanReadable);

        OverviewSections = overviewSections;
        DetailSections = detailSections;
    }

    private static void AddGptSections(
        PartitionTablePart gpt,
        ObservableCollection<OverviewSectionViewModel> overviewSections,
        ObservableCollection<DetailSectionBase> detailSections,
        bool humanReadable, bool showUnallocated)
    {
        var sec = new OverviewSectionViewModel
        {
            Title = $"Guid Partition Table: {FormatSize(gpt.Size, humanReadable)}",
            IsRdb = false
        };
        foreach (var p in (gpt.Parts ?? []).Where(p => showUnallocated || p.PartType != PartType.Unallocated))
            sec.Parts.Add(ToPartOverviewRow(p, isRdb: false, humanReadable));
        overviewSections.Add(sec);

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
        ObservableCollection<OverviewSectionViewModel> overviewSections,
        ObservableCollection<DetailSectionBase> detailSections,
        bool humanReadable, bool showUnallocated)
    {
        var sec = new OverviewSectionViewModel
        {
            Title = $"Master Boot Record: {FormatSize(mbr.Size, humanReadable)}",
            IsRdb = false
        };
        foreach (var p in (mbr.Parts ?? []).Where(p => showUnallocated || p.PartType != PartType.Unallocated))
            sec.Parts.Add(ToPartOverviewRow(p, isRdb: false, humanReadable));
        overviewSections.Add(sec);

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

    private static PartOverviewRow ToPartOverviewRow(PartInfo part, bool isRdb, bool humanReadable) =>
        new()
        {
            Color = PartColor(part.PartType),
            TypeDisplay = part.PartType == PartType.PartitionTable
                ? FormatPartitionTableType(part.PartitionTableType)
                : part.PartitionType ?? string.Empty,
            FileSystem = part.FileSystem ?? string.Empty,
            Number = part.PartitionNumber?.ToString() ?? string.Empty,
            Size = FormatSize(part.Size, humanReadable),
            StartOffset = part.StartOffset.ToString(),
            EndOffset = part.EndOffset.ToString(),
            StartSecOrCyl = isRdb ? part.StartCylinder.ToString() : part.StartSector.ToString(),
            EndSecOrCyl = isRdb ? part.EndCylinder.ToString() : part.EndSector.ToString()
        };

    private static string FormatSize(long bytes, bool humanReadable) =>
        humanReadable ? ByteSize.FromBytes(bytes).Humanize("#.#") : bytes.ToString();

    private static string SparseLabel(DiskInfo diskInfo, bool humanReadable) =>
        diskInfo.IsSparseFile ? $" / {FormatSize(diskInfo.SparseFileSize, humanReadable)} (sparse)" : string.Empty;

    private static string PartColor(PartType partType) => partType switch
    {
        PartType.PartitionTable => "#6060ff",
        PartType.Partition => "#50ff50",
        PartType.Unallocated => "#808080",
        _ => "#ffff00"
    };

    private static string FormatPartitionTableType(PartitionTableType type) => type switch
    {
        PartitionTableType.GuidPartitionTable => "Guid Partition Table",
        PartitionTableType.MasterBootRecord => "Master Boot Record",
        PartitionTableType.RigidDiskBlock => "Rigid Disk Block",
        _ => string.Empty
    };
}
