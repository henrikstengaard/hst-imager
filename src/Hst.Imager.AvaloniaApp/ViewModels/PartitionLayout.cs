using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Media;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.FileSystems;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

/// <summary>
/// File systems available for partitions in partition tables.
/// </summary>
public static class PartitionFileSystems
{
    public static readonly List<SelectOption> BasicOptions =
    [
        new() { Title = "FAT32", Value = "fat32" },
        new() { Title = "exFAT", Value = "exfat" },
        new() { Title = "NTFS", Value = "ntfs" }
    ];

    public static readonly List<SelectOption> RdbOptions =
    [
        new() { Title = "PDS\\3 (direct scsi)", Value = "pds3" },
        new() { Title = "PFS\\3", Value = "pfs3" },
        new() { Title = "DOS\\3", Value = "dos3" },
        new() { Title = "DOS\\7 (long filename)", Value = "dos7" }
    ];

    public static List<SelectOption> GetOptions(PartitionTableType tableType) =>
        tableType == PartitionTableType.RigidDiskBlock ? RdbOptions : BasicOptions;

    public static string GetTitle(string fileSystem) => fileSystem switch
    {
        "fat32" => "FAT32",
        "exfat" => "exFAT",
        "ntfs" => "NTFS",
        "pds3" => "PDS\\3",
        "pfs3" => "PFS\\3",
        "dos3" => "DOS\\3",
        "dos7" => "DOS\\7",
        _ => fileSystem
    };

    public static bool IsPfs3(string fileSystem) => fileSystem is "pfs3" or "pds3";

    /// <summary>
    /// Bios type of master boot record partition added with file system, FAT32 LBA (0xc) or NTFS/exFAT (0x7).
    /// </summary>
    public static int GetMbrBiosType(string fileSystem) => fileSystem == "fat32" ? 0xc : 0x7;

    public static bool IsFastFileSystem(string fileSystem) => fileSystem is "dos3" or "dos7";

    /// <summary>
    /// Get color for file system similar to colors used by gparted.
    /// </summary>
    public static string GetColor(string fileSystem)
    {
        var normalized = fileSystem.Replace("\\", string.Empty).ToLowerInvariant();
        if (normalized.Contains("fat32") || normalized.Contains("fat16") || normalized.Contains("fat12"))
            return "#18D918";
        if (normalized.Contains("exfat"))
            return "#2E8B57";
        if (normalized.Contains("ntfs"))
            return "#42E5AC";
        if (normalized.Contains("pds3"))
            return "#D98C1A";
        if (normalized.Contains("pfs3"))
            return "#F0A830";
        if (normalized.Contains("dos7"))
            return "#9B6BD6";
        if (normalized.Contains("dos"))
            return "#C77DD9";
        return "#7A8FA6";
    }
}

/// <summary>
/// Partition in partition layout, either an existing partition read from disk or a new partition to add.
/// </summary>
public class PartitionEntryViewModel : ReactiveObject
{
    private long _start;
    private long _size;
    private string _fileSystem = string.Empty;
    private string _label = string.Empty;
    private string _deviceName = string.Empty;
    private bool _bootable;
    private bool _formatRequested;
    private bool _isPiStorm;

    /// <summary>
    /// Partition types of new master boot record partitions, a regular partition formatted with a file system or a
    /// PiStorm partition containing a rigid disk block.
    /// </summary>
    public static readonly List<SelectOption> PartitionTypeOptions =
    [
        new() { Title = "Regular partition", Value = "regular" },
        new() { Title = "PiStorm partition with Rigid Disk Block", Value = "pistorm" }
    ];

    public PartitionEntryViewModel(PartitionTableType tableType, bool isNew)
    {
        TableType = tableType;
        IsNew = isNew;
        FileSystemOptions = PartitionFileSystems.GetOptions(tableType);
    }

    public PartitionTableType TableType { get; }
    public bool IsNew { get; }
    public bool IsExisting => !IsNew;
    public bool IsRdb => TableType == PartitionTableType.RigidDiskBlock;
    public bool IsMbr => TableType == PartitionTableType.MasterBootRecord;

    /// <summary>
    /// Partition number of existing partition.
    /// </summary>
    public int? Number { get; init; }

    /// <summary>
    /// File system or type of existing partition read from disk.
    /// </summary>
    public string ExistingFileSystem { get; init; } = string.Empty;

    public long? UsedSize { get; init; }

    /// <summary>
    /// Bios type of existing master boot record partition.
    /// </summary>
    public int? ExistingBiosType { get; init; }

    /// <summary>
    /// Dos type of existing rigid disk block partition without backslash, e.g. PFS3.
    /// </summary>
    public string ExistingDosType { get; init; } = string.Empty;

    /// <summary>
    /// Formatting existing master boot record partition with file system requires changing its bios type,
    /// e.g. from NTFS to FAT32.
    /// </summary>
    public bool RequiresTypeChange => IsExisting && IsMbr && _formatRequested &&
                                      ExistingBiosType != PartitionFileSystems.GetMbrBiosType(_fileSystem);

    public List<SelectOption> FileSystemOptions { get; }

    public long Start => _start;
    public long Size => _size;
    public long End => _start + _size;

    public string FileSystem
    {
        get => _fileSystem;
        set
        {
            this.RaiseAndSetIfChanged(ref _fileSystem, value);
            this.RaisePropertyChanged(nameof(FileSystemOption));
            this.RaisePropertyChanged(nameof(FileSystemDisplay));
        }
    }

    public SelectOption? FileSystemOption
    {
        get => FileSystemOptions.FirstOrDefault(x => x.Value == _fileSystem);
        set
        {
            if (value != null)
                FileSystem = value.Value;
        }
    }

    /// <summary>
    /// File system displayed, which is the new file system for new or formatted partitions.
    /// </summary>
    public string FileSystemDisplay => _isPiStorm
        ? "PiStorm RDB"
        : IsNew || (_formatRequested && !IsRdb)
            ? PartitionFileSystems.GetTitle(_fileSystem)
            : ExistingFileSystem;

    /// <summary>
    /// New master boot record partition can be switched between a regular and a PiStorm partition.
    /// </summary>
    public bool CanBePiStorm => IsNew && IsMbr;

    /// <summary>
    /// New master boot record partition is a PiStorm partition containing a rigid disk block, which isn't formatted.
    /// </summary>
    public bool IsPiStorm
    {
        get => _isPiStorm;
        set
        {
            if (!CanBePiStorm) return;
            this.RaiseAndSetIfChanged(ref _isPiStorm, value);
            this.RaisePropertyChanged(nameof(PartitionTypeOption));
            this.RaisePropertyChanged(nameof(CanEditFileSystem));
            this.RaisePropertyChanged(nameof(CanEditLabel));
            this.RaisePropertyChanged(nameof(FileSystemDisplay));
        }
    }

    public SelectOption PartitionTypeOption
    {
        get => PartitionTypeOptions[_isPiStorm ? 1 : 0];
        set
        {
            if (value != null)
                IsPiStorm = value.Value == "pistorm";
        }
    }

    /// <summary>
    /// Volume name.
    /// </summary>
    public string Label
    {
        get => _label;
        set => this.RaiseAndSetIfChanged(ref _label, value);
    }

    /// <summary>
    /// Device name for rigid disk block partitions, e.g. DH0.
    /// </summary>
    public string DeviceName
    {
        get => _deviceName;
        set => this.RaiseAndSetIfChanged(ref _deviceName, value);
    }

    /// <summary>
    /// Active for master boot record and bootable for rigid disk block partitions.
    /// </summary>
    public bool Bootable
    {
        get => _bootable;
        set => this.RaiseAndSetIfChanged(ref _bootable, value);
    }

    /// <summary>
    /// Format existing partition.
    /// </summary>
    public bool FormatRequested
    {
        get => _formatRequested;
        set
        {
            this.RaiseAndSetIfChanged(ref _formatRequested, value);
            if (value && string.IsNullOrWhiteSpace(_label))
                Label = "Empty";
            this.RaisePropertyChanged(nameof(CanEditFileSystem));
            this.RaisePropertyChanged(nameof(CanEditLabel));
            this.RaisePropertyChanged(nameof(FileSystemDisplay));
        }
    }

    /// <summary>
    /// File system can be changed for new partitions and formatted master boot record and guid partitions.
    /// Formatting rigid disk block partitions uses dos type of the partition.
    /// </summary>
    public bool CanEditFileSystem => (IsNew && !_isPiStorm) || (_formatRequested && !IsRdb);

    public bool CanEditLabel => (IsNew && !_isPiStorm) || _formatRequested;

    /// <summary>
    /// Device name and bootable are set when rigid disk block partition is added.
    /// </summary>
    public bool CanEditRdbProperties => IsNew && IsRdb;

    public bool CanEditActive => IsNew && IsMbr;

    /// <summary>
    /// Get editable state of partition, which can be restored when editing partition is cancelled.
    /// </summary>
    public PartitionEntryState GetState() =>
        new(_start, _size, _fileSystem, _label, _deviceName, _bootable, _formatRequested, _isPiStorm);

    /// <summary>
    /// Restore editable state of partition except range, which is restored by partition layout.
    /// Label is restored after format requested, as requesting format sets an empty label.
    /// </summary>
    internal void RestoreState(PartitionEntryState state)
    {
        IsPiStorm = state.IsPiStorm;
        FormatRequested = state.FormatRequested;
        FileSystem = state.FileSystem;
        Label = state.Label;
        DeviceName = state.DeviceName;
        Bootable = state.Bootable;
    }

    internal void SetRange(long start, long size)
    {
        if (_start == start && _size == size)
            return;
        _start = start;
        _size = size;
        this.RaisePropertyChanged(nameof(Start));
        this.RaisePropertyChanged(nameof(Size));
        this.RaisePropertyChanged(nameof(End));
    }
}

/// <summary>
/// Placement of partition added to unallocated space.
/// </summary>
public enum AddPartitionPlacement
{
    /// <summary>
    /// First third of unallocated space.
    /// </summary>
    Start,

    /// <summary>
    /// All of unallocated space.
    /// </summary>
    All,

    /// <summary>
    /// Last third of unallocated space.
    /// </summary>
    End
}

/// <summary>
/// Request to add partition to unallocated space segment with placement.
/// </summary>
public record AddPartitionRequest(PartitionSegmentViewModel Segment, AddPartitionPlacement Placement);

/// <summary>
/// Editable state of partition.
/// </summary>
public record PartitionEntryState(long Start, long Size, string FileSystem, string Label, string DeviceName,
    bool Bootable, bool FormatRequested, bool IsPiStorm);

/// <summary>
/// Area reserved by another partition table, e.g. master boot record partitions in a hybrid disk.
/// </summary>
public record ReservedArea(long Start, long Size, string Name, PartitionTableType TableType);

/// <summary>
/// Segment of partition layout, either a partition, unallocated space, area used by the partition table itself or area
/// reserved by another partition table, shown in visual and list view. Start and size are relative to partition
/// layout, disk start is offset by start of partition table on disk, e.g. master boot record partition with a PiStorm
/// rigid disk block.
/// </summary>
public class PartitionSegmentViewModel
{
    private readonly string? _shortName;

    public PartitionSegmentViewModel(PartitionLayout layout, long start, long size, PartitionEntryViewModel? partition,
        ReservedArea? reserved = null, string? unallocatedName = null, long offset = 0, int depth = 0,
        bool isPartitionTable = false)
    {
        Layout = layout;
        Start = start;
        Size = size;
        DiskStart = offset + start;
        Depth = depth;
        Partition = partition;
        IsReserved = partition == null && reserved != null;
        IsPartitionTable = partition == null && reserved == null && isPartitionTable;

        // reserved areas belong to another partition table
        var tableType = IsReserved ? reserved!.TableType : layout.TableType;
        TableType = PartitionLayout.GetTableTypeAbbreviation(tableType);
        TableColor = PartitionLayout.GetTableTypeColor(tableType);

        // start and end in sectors from start of disk, so partition tables in partitions like PiStorm rigid disk
        // blocks use same sectors as partition table of disk. rigid disk block segments also show start and end in
        // cylinders of rigid disk block
        StartText = (DiskStart / 512).ToString();
        EndText = ((DiskStart + size) / 512 - 1).ToString();
        if (layout.IsRdb && !IsReserved)
        {
            StartCylinderText = (start / layout.Alignment).ToString();
            EndCylinderText = ((start + size) / layout.Alignment - 1).ToString();
        }
        SizeText = MediaOptions.FormatBytes(size);

        // area used by partition table is shown in color of partition table with abbreviation used, when name
        // doesn't fit
        if (IsPartitionTable)
        {
            Name = unallocatedName ?? layout.TableTypeName;
            _shortName = TableType;
            Color = TableColor;
            return;
        }

        if (partition == null)
        {
            Name = reserved?.Name ?? unallocatedName ?? "Unallocated";
            Color = IsReserved ? "#6060FF" : "#8A8A8A";
            Status = IsReserved ? "Reserved" : string.Empty;
            return;
        }

        Number = partition.Number?.ToString() ?? string.Empty;
        Name = layout.IsRdb
            ? string.IsNullOrWhiteSpace(partition.Label) || !partition.CanEditLabel
                ? partition.DeviceName
                : $"{partition.DeviceName}: {partition.Label}"
            : partition.CanEditLabel
                ? partition.Label
                : partition.IsPiStorm
                    ? "PiStorm"
                    : $"Partition #{partition.Number}";
        FileSystem = partition.FileSystemDisplay;
        Color = PartitionFileSystems.GetColor(partition.CanEditFileSystem ? partition.FileSystem : partition.ExistingFileSystem);
        UsedText = partition.UsedSize.HasValue && !partition.FormatRequested
            ? MediaOptions.FormatBytes(partition.UsedSize.Value)
            : string.Empty;
        Flags = partition.Bootable ? layout.IsRdb ? "bootable" : "active" : string.Empty;
        Status = partition.IsNew ? "New" : partition.FormatRequested ? "Format" : string.Empty;
    }

    /// <summary>
    /// Partition layout segment belongs to.
    /// </summary>
    public PartitionLayout Layout { get; }

    public PartitionEntryViewModel? Partition { get; }
    public bool IsReserved { get; }

    /// <summary>
    /// Area used by partition table itself before usable area, e.g. sectors of master boot record, guid partition
    /// table or cylinders of rigid disk block, and backup guid partition table at end of disk.
    /// </summary>
    public bool IsPartitionTable { get; }

    /// <summary>
    /// Layout of partition table nested in partition, e.g. master boot record partition with a PiStorm rigid disk
    /// block.
    /// </summary>
    public PartitionLayout? NestedLayout { get; set; }

    /// <summary>
    /// Partition contains another partition table shown nested in it.
    /// </summary>
    public bool IsContainer => NestedLayout != null;

    /// <summary>
    /// Nesting depth, 1 for segments of a partition table in a partition.
    /// </summary>
    public int Depth { get; }

    public bool IsNested => Depth > 0;

    public long DiskStart { get; }
    public long DiskEnd => DiskStart + Size;

    /// <summary>
    /// Partition table segment belongs to: MBR, GPT or RDB.
    /// </summary>
    public string TableType { get; }

    /// <summary>
    /// Color of partition table segment belongs to.
    /// </summary>
    public string TableColor { get; }
    public IBrush TableColorBrush => new SolidColorBrush(Avalonia.Media.Color.Parse(TableColor));
    public bool HasTableType => !string.IsNullOrEmpty(TableType);
    public bool IsUnallocated => Partition == null && !IsReserved && !IsPartitionTable;
    public long Start { get; }
    public long Size { get; }
    public long End => Start + Size;
    public string Number { get; } = string.Empty;
    public string Name { get; }

    /// <summary>
    /// Name shown in visual view, when name doesn't fit.
    /// </summary>
    public string ShortName => string.IsNullOrEmpty(_shortName) ? Name : _shortName;

    public string FileSystem { get; } = string.Empty;
    public string Color { get; }
    public IBrush ColorBrush => new SolidColorBrush(Avalonia.Media.Color.Parse(Color));
    public string SizeText { get; }
    public string UsedText { get; } = string.Empty;
    public string StartText { get; }
    public string EndText { get; }
    public string StartCylinderText { get; } = string.Empty;
    public string EndCylinderText { get; } = string.Empty;
    public string Flags { get; } = string.Empty;
    public string Status { get; } = string.Empty;
}

/// <summary>
/// Editable layout of partitions in a partition table. New partitions are aligned to 1MB for master boot record
/// and guid partition table and to cylinders for rigid disk block. Existing partitions can be deleted or formatted,
/// but not moved or resized.
/// </summary>
public class PartitionLayout
{
    public const long MiB = 1024 * 1024;

    // master boot record and guid partition table add commands limits last sector to 100 sectors before end of disk
    private const long LastSectorsReserved = 100;

    // master boot record partitions are added with a gap after rigid disk block in hybrid disks
    private const long RdbMbrGap = 512 * 1024;

    // master boot record uses 32-bit sector addresses
    private const long MaxMbrSize = 512L * uint.MaxValue;

    private readonly List<PartitionEntryViewModel> _partitions = [];
    private readonly List<PartitionEntryViewModel> _deletedPartitions = [];
    private readonly List<ReservedArea> _reservedAreas = [];
    private readonly List<RdbFileSystemEntry> _fileSystems = [];
    private readonly List<RdbFileSystemEntry> _deletedFileSystems = [];

    private PartitionLayout(PartitionTableType tableType, long diskSize, long usableStart, long usableEnd,
        long alignment, int maxPartitions, bool isInitialize)
    {
        TableType = tableType;
        DiskSize = diskSize;
        UsableStart = usableStart;
        UsableEnd = usableEnd;
        Alignment = alignment;
        MaxPartitions = maxPartitions;
        IsInitialize = isInitialize;
    }

    public event EventHandler? Changed;

    public PartitionTableType TableType { get; }
    public bool IsRdb => TableType == PartitionTableType.RigidDiskBlock;
    public bool HasPartitionTable => TableType != PartitionTableType.None;

    /// <summary>
    /// Disk without partition table is blank with zeroes in first sectors, which is uninitialized.
    /// </summary>
    public bool IsBlank { get; private init; }

    /// <summary>
    /// New partition table is initialized, which erases existing partition tables.
    /// </summary>
    public bool IsInitialize { get; }

    /// <summary>
    /// Existing master boot record is kept, when initializing rigid disk block for a hybrid disk.
    /// </summary>
    public bool KeepMasterBootRecord { get; private init; }

    /// <summary>
    /// Sector rigid disk block is written to, when initializing rigid disk block.
    /// </summary>
    public int RdbBlockLo { get; private init; }

    /// <summary>
    /// Size of rigid disk block, when initializing rigid disk block. 0 uses whole disk.
    /// </summary>
    public long RdbSize { get; private init; }

    /// <summary>
    /// Layout has changes to apply.
    /// </summary>
    public bool HasChanges => IsInitialize || _deletedPartitions.Count > 0 ||
                              _partitions.Any(x => x.IsNew || x.FormatRequested) || HasFileSystemChanges;

    /// <summary>
    /// Rigid disk block has file systems to add, import, update or delete.
    /// </summary>
    public bool HasFileSystemChanges => _deletedFileSystems.Count > 0 || _fileSystems.Any(x => x.IsNew || x.IsUpdated);

    public long DiskSize { get; }
    public long UsableStart { get; }
    public long UsableEnd { get; }

    /// <summary>
    /// Alignment of new partitions in bytes, 1MB or cylinder size for rigid disk block.
    /// </summary>
    public long Alignment { get; }

    public int MaxPartitions { get; }

    public long MinPartitionSize => Math.Max(Alignment, AlignUp(MiB));

    /// <summary>
    /// Pfs3 max partition size aligned to cylinders.
    /// </summary>
    public long Pfs3MaxPartitionSize => AlignDown(FileSystemHelper.Pfs3MaxPartitionSize);

    /// <summary>
    /// Dos types of file systems in rigid disk block after pending file system changes, e.g. PFS3.
    /// </summary>
    public IReadOnlySet<string> FileSystemDosTypes =>
        _fileSystems.Select(x => x.DosType).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<PartitionEntryViewModel> Partitions => _partitions;
    public IReadOnlyList<PartitionEntryViewModel> DeletedPartitions => _deletedPartitions;

    /// <summary>
    /// File systems in rigid disk block, existing file systems not deleted and new file systems to add or import.
    /// </summary>
    public IReadOnlyList<RdbFileSystemEntry> FileSystems => _fileSystems;

    /// <summary>
    /// Existing file systems to delete from rigid disk block.
    /// </summary>
    public IReadOnlyList<RdbFileSystemEntry> DeletedFileSystems => _deletedFileSystems;

    public bool CanAddPartition => HasPartitionTable && _partitions.Count < MaxPartitions;

    public static PartitionLayout CreateEmpty(long diskSize, bool isBlank = false) =>
        new(PartitionTableType.None, diskSize, 0, diskSize, MiB, 0, false) { IsBlank = isBlank };

    /// <summary>
    /// Create layout for a new initialized partition table without partitions.
    /// </summary>
    public static PartitionLayout CreateNew(PartitionTableType tableType, long diskSize) =>
        tableType == PartitionTableType.RigidDiskBlock
            ? CreateNewRdb(diskSize, 0, 0, null)
            : CreateBasicLayout(tableType, diskSize, true);

    /// <summary>
    /// Create layout for a new initialized rigid disk block written to sector rdb block lo with size.
    /// Partitions of master boot record kept for a hybrid disk are reserved areas.
    /// </summary>
    public static PartitionLayout CreateNewRdb(long diskSize, long rdbSize, int rdbBlockLo,
        IEnumerable<ReservedArea>? keptMasterBootRecordPartitions)
    {
        var size = rdbSize > 0 ? Math.Min(rdbSize, diskSize) : diskSize;
        var layout = CreateRdbLayout(RigidDiskBlock.Create(size / 512 * 512), diskSize, true,
            keptMasterBootRecordPartitions != null, rdbBlockLo, rdbSize);
        layout._reservedAreas.AddRange(keptMasterBootRecordPartitions ?? []);
        return layout;
    }

    /// <summary>
    /// Reserve area used by rigid disk block initialized keeping master boot record for a hybrid disk, so master boot
    /// record partitions are added after rigid disk block. Replaces rigid disk block area reserved before.
    /// </summary>
    public void ReserveRigidDiskBlock(long rdbSize)
    {
        _reservedAreas.RemoveAll(x => x.TableType == PartitionTableType.RigidDiskBlock);
        _reservedAreas.Add(new ReservedArea(0, AlignUp(rdbSize + RdbMbrGap, MiB), "Rigid Disk Block",
            PartitionTableType.RigidDiskBlock));
        OnChanged();
    }

    /// <summary>
    /// Move new partitions and file systems from another layout of same partition table, e.g. rigid disk block in a
    /// new PiStorm partition, which is resized. Partitions are kept, if they start in usable area and cylinders are
    /// same size. Last partition is shrunk to fit, if usable area is smaller.
    /// </summary>
    public void AdoptPartitions(PartitionLayout other)
    {
        _fileSystems.AddRange(other._fileSystems.Where(x => x.IsNew));
        other._fileSystems.Clear();

        foreach (var partition in other._partitions.ToList())
        {
            other._partitions.Remove(partition);
            partition.PropertyChanged -= other.OnPartitionPropertyChanged;

            if (!partition.IsNew || other.Alignment != Alignment || partition.Start < UsableStart ||
                partition.Start + MinPartitionSize > UsableEnd)
                continue;

            partition.SetRange(partition.Start, Math.Min(partition.End, UsableEnd) - partition.Start);
            AddEntry(partition);
        }
    }

    /// <summary>
    /// Areas of partitions in layout, including new partitions, used to reserve them in another layout.
    /// </summary>
    public IEnumerable<ReservedArea> GetPartitionAreas() =>
        _partitions.Select(x => new ReservedArea(x.Start, x.Size, x.IsNew
            ? $"{FormatTableTypeName(TableType)} partition (new)"
            : $"{FormatTableTypeName(TableType)} partition #{x.Number}", TableType));

    /// <summary>
    /// Abbreviation of partition table, e.g. RDB for rigid disk block.
    /// </summary>
    public static string GetTableTypeAbbreviation(PartitionTableType tableType) => tableType switch
    {
        PartitionTableType.MasterBootRecord => "MBR",
        PartitionTableType.GuidPartitionTable => "GPT",
        PartitionTableType.RigidDiskBlock => "RDB",
        _ => string.Empty
    };

    /// <summary>
    /// Color of partition table used to show partitions belonging to it, which differs from file system colors.
    /// </summary>
    public static string GetTableTypeColor(PartitionTableType tableType) => tableType switch
    {
        PartitionTableType.MasterBootRecord => "#2F7FD0",
        PartitionTableType.GuidPartitionTable => "#0F8F9E",
        PartitionTableType.RigidDiskBlock => "#C2456E",
        _ => "#8A8A8A"
    };

    /// <summary>
    /// Name of partition table, e.g. Rigid Disk Block.
    /// </summary>
    public string TableTypeName => FormatTableTypeName(TableType);

    /// <summary>
    /// Size of partition table. Size of rigid disk block is the cylinders it uses, other partition tables use the
    /// whole disk.
    /// </summary>
    public long TableSize => IsRdb ? Math.Min(UsableEnd, DiskSize) : DiskSize;

    public static string FormatTableTypeName(PartitionTableType tableType) => tableType switch
    {
        PartitionTableType.MasterBootRecord => "Master Boot Record",
        PartitionTableType.GuidPartitionTable => "Guid Partition Table",
        PartitionTableType.RigidDiskBlock => "Rigid Disk Block",
        _ => string.Empty
    };

    /// <summary>
    /// Get partition tables present in disk info, which can be partitioned. Master boot record is excluded,
    /// when guid partition table is present, as it's a protective master boot record.
    /// Hybrid disks have both a rigid disk block and a master boot record.
    /// </summary>
    public static IReadOnlyList<PartitionTableType> GetPartitionTableTypes(DiskInfo? diskInfo)
    {
        var tableTypes = new List<PartitionTableType>();
        if (diskInfo?.GptPartitionTablePart != null)
            tableTypes.Add(PartitionTableType.GuidPartitionTable);
        if (diskInfo?.RigidDiskBlock != null && diskInfo.RdbPartitionTablePart != null)
            tableTypes.Add(PartitionTableType.RigidDiskBlock);
        if (diskInfo?.MbrPartitionTablePart != null && diskInfo.GptPartitionTablePart == null)
            tableTypes.Add(PartitionTableType.MasterBootRecord);
        return tableTypes;
    }

    /// <summary>
    /// Create layout from partition table read from disk. First partition table present is used,
    /// if partition table type is not set.
    /// </summary>
    public static PartitionLayout FromMediaInfo(MediaInfo media, PartitionTableType? tableType = null,
        bool isBlank = false)
    {
        var diskInfo = media.DiskInfo;
        tableType ??= GetPartitionTableTypes(diskInfo).FirstOrDefault(PartitionTableType.None);

        return tableType switch
        {
            PartitionTableType.GuidPartitionTable when diskInfo?.GptPartitionTablePart != null =>
                FromBasicPartitionTable(PartitionTableType.GuidPartitionTable, media.DiskSize,
                    diskInfo.GptPartitionTablePart, diskInfo),
            PartitionTableType.RigidDiskBlock when diskInfo?.RigidDiskBlock != null &&
                                                   diskInfo.RdbPartitionTablePart != null =>
                FromRigidDiskBlock(diskInfo.RigidDiskBlock, media.DiskSize, diskInfo.RdbPartitionTablePart, diskInfo),
            PartitionTableType.MasterBootRecord when diskInfo?.MbrPartitionTablePart != null =>
                FromBasicPartitionTable(PartitionTableType.MasterBootRecord, media.DiskSize,
                    diskInfo.MbrPartitionTablePart, diskInfo),
            _ => CreateEmpty(media.DiskSize, isBlank)
        };
    }

    private static PartitionLayout CreateBasicLayout(PartitionTableType tableType, long diskSize, bool isInitialize,
        long usableStart = MiB)
    {
        var usableEnd = AlignDown((diskSize / 512 - LastSectorsReserved) * 512, MiB);
        if (tableType == PartitionTableType.MasterBootRecord)
            usableEnd = Math.Min(usableEnd, AlignDown(MaxMbrSize, MiB));

        return new PartitionLayout(tableType, diskSize, usableStart, Math.Max(usableStart, usableEnd), MiB,
            tableType == PartitionTableType.MasterBootRecord ? 4 : 128, isInitialize);
    }

    private static PartitionLayout CreateRdbLayout(RigidDiskBlock rigidDiskBlock, long diskSize, bool isInitialize,
        bool keepMasterBootRecord = false, int rdbBlockLo = 0, long rdbSize = 0)
    {
        var cylinderSize = (long)rigidDiskBlock.Heads * rigidDiskBlock.Sectors * rigidDiskBlock.BlockSize;
        return new PartitionLayout(PartitionTableType.RigidDiskBlock, diskSize,
            rigidDiskBlock.LoCylinder * cylinderSize, ((long)rigidDiskBlock.HiCylinder + 1) * cylinderSize,
            cylinderSize, 128, isInitialize)
        {
            KeepMasterBootRecord = keepMasterBootRecord,
            RdbBlockLo = rdbBlockLo,
            RdbSize = rdbSize
        };
    }

    private static PartitionLayout FromBasicPartitionTable(PartitionTableType tableType, long diskSize,
        PartitionTablePart partitionTablePart, DiskInfo diskInfo)
    {
        // master boot record partitions in hybrid disk are placed after rigid disk block
        var rdbSize = tableType == PartitionTableType.MasterBootRecord && diskInfo.RigidDiskBlock != null
            ? diskInfo.RdbPartitionTablePart?.Size ?? 0
            : 0;
        var layout = CreateBasicLayout(tableType, diskSize, false,
            rdbSize > 0 ? AlignUp(rdbSize + RdbMbrGap, MiB) : MiB);
        if (rdbSize > 0)
            layout._reservedAreas.Add(new ReservedArea(0, rdbSize, "Rigid Disk Block", PartitionTableType.RigidDiskBlock));
        foreach (var part in (partitionTablePart.Parts ?? []).Where(x => x.PartType == PartType.Partition))
        {
            var entry = new PartitionEntryViewModel(tableType, false)
            {
                Number = part.PartitionNumber,
                ExistingFileSystem = FormatExistingFileSystem(part),
                UsedSize = GetUsedSize(part),
                ExistingBiosType = int.TryParse(part.BiosType, out var biosType) ? biosType : null
            };
            entry.SetRange(part.StartOffset, part.EndOffset - part.StartOffset + 1);
            entry.FileSystem = GuessFileSystem(part);
            entry.Bootable = part.IsActive;
            layout.AddEntry(entry);
        }

        return layout;
    }

    private static PartitionLayout FromRigidDiskBlock(RigidDiskBlock rigidDiskBlock, long diskSize,
        PartitionTablePart partitionTablePart, DiskInfo diskInfo)
    {
        var layout = CreateRdbLayout(rigidDiskBlock, diskSize, false);
        layout._fileSystems.AddRange((rigidDiskBlock.FileSystemHeaderBlocks ?? [])
            .Select((x, i) => RdbFileSystemEntry.FromHeaderBlock(x, i + 1)));

        // master boot record partitions after rigid disk block in hybrid disk
        foreach (var part in (diskInfo.MbrPartitionTablePart?.Parts ?? [])
                     .Where(x => x.PartType == PartType.Partition && x.StartOffset >= layout.UsableEnd))
            layout._reservedAreas.Add(new ReservedArea(part.StartOffset, part.Size,
                $"Master Boot Record partition #{part.PartitionNumber}", PartitionTableType.MasterBootRecord));
        var partitionBlocks = (rigidDiskBlock.PartitionBlocks ?? []).ToList();
        foreach (var part in (partitionTablePart.Parts ?? []).Where(x => x.PartType == PartType.Partition))
        {
            var partitionBlock = part.PartitionNumber is > 0 && part.PartitionNumber <= partitionBlocks.Count
                ? partitionBlocks[part.PartitionNumber.Value - 1]
                : null;
            var entry = new PartitionEntryViewModel(PartitionTableType.RigidDiskBlock, false)
            {
                Number = part.PartitionNumber,
                ExistingFileSystem = part.FileSystem ?? part.PartitionType ?? string.Empty,
                ExistingDosType = RdbFileSystemEntry.NormalizeDosType(partitionBlock?.DosTypeFormatted ?? string.Empty),
                UsedSize = GetUsedSize(part)
            };
            entry.SetRange(part.StartOffset, part.EndOffset - part.StartOffset + 1);
            entry.DeviceName = partitionBlock?.DriveName ?? string.Empty;
            entry.Bootable = partitionBlock?.Bootable ?? false;
            entry.Label = entry.DeviceName.StartsWith("DH0", StringComparison.OrdinalIgnoreCase) ? "Workbench" : "Work";
            layout.AddEntry(entry);
        }

        return layout;
    }

    private static string FormatExistingFileSystem(PartInfo part) =>
        string.IsNullOrWhiteSpace(part.FileSystem) || part.FileSystem == part.PartitionType
            ? part.PartitionType ?? string.Empty
            : part.FileSystem;

    private static string GuessFileSystem(PartInfo part)
    {
        var fileSystem = $"{part.FileSystem} {part.PartitionType}".ToLowerInvariant();
        if (fileSystem.Contains("exfat")) return "exfat";
        if (fileSystem.Contains("ntfs")) return "ntfs";
        return "fat32";
    }

    private static long? GetUsedSize(PartInfo part) =>
        part.VolumeSize.HasValue && part.VolumeFree.HasValue
            ? Math.Max(0, part.VolumeSize.Value - part.VolumeFree.Value)
            : null;

    // ─── Alignment ────────────────────────────────────────────────────────────

    public long AlignDown(long value) => AlignDown(value, Alignment);

    public long AlignUp(long value) => AlignUp(value, Alignment);

    private long AlignNearest(long value) => AlignDown(value + Alignment / 2, Alignment);

    private static long AlignDown(long value, long alignment) => value / alignment * alignment;

    private static long AlignUp(long value, long alignment) => (value + alignment - 1) / alignment * alignment;

    // ─── Segments and free space ──────────────────────────────────────────────

    /// <summary>
    /// Build segments of partitions and unallocated space in usable area of partition table. Segments are offset by
    /// start of partition table on disk and nested with depth, when partition table is in a partition.
    /// </summary>
    public List<PartitionSegmentViewModel> BuildSegments(long offset = 0, int depth = 0)
    {
        PartitionSegmentViewModel Segment(long start, long size, PartitionEntryViewModel? partition,
            ReservedArea? reserved = null, string? unallocatedName = null) =>
            new(this, start, size, partition, reserved, unallocatedName, offset, depth);

        var segments = new List<PartitionSegmentViewModel>();
        if (!HasPartitionTable)
        {
            segments.Add(Segment(0, DiskSize, null, null, IsBlank ? "Uninitialized" : "No partition table"));
            return segments;
        }

        // area used by partition table before usable area. master boot record in a hybrid disk is placed after rigid
        // disk block, which shows its own area. existing partitions can start before usable area
        var tableEnd = _partitions.Select(x => x.Start).Append(UsableStart).Min();
        if (tableEnd > 0 && !_reservedAreas.Any(x => x.Start < UsableStart))
            segments.Add(new PartitionSegmentViewModel(this, 0, tableEnd, null, null, null, offset, depth, true));

        // partitions and areas reserved by other partition tables, which can be outside usable area
        var occupied = _partitions
            .Select(x => Segment(x.Start, x.Size, x))
            .Concat(_reservedAreas.Select(x => Segment(x.Start, x.Size, null, x)))
            .OrderBy(x => x.Start);

        var position = UsableStart;
        foreach (var segment in occupied)
        {
            var gapEnd = Math.Min(segment.Start, UsableEnd);
            if (gapEnd - position >= MinPartitionSize)
                segments.Add(Segment(position, gapEnd - position, null));
            segments.Add(segment);
            position = Math.Max(position, segment.End);
        }

        if (UsableEnd - position >= MinPartitionSize)
            segments.Add(Segment(position, UsableEnd - position, null));

        // backup guid partition table at end of disk after usable area
        var backupStart = Math.Max(position, UsableEnd);
        if (TableType == PartitionTableType.GuidPartitionTable && DiskSize > backupStart)
            segments.Add(new PartitionSegmentViewModel(this, backupStart, DiskSize - backupStart, null, null,
                $"{TableTypeName} (backup)", offset, depth, true));

        return segments;
    }

    /// <summary>
    /// Get bounds a partition can be resized or moved within, which is the space between previous and next partition
    /// or area reserved by another partition table.
    /// </summary>
    public (long Lower, long Upper) GetBounds(PartitionEntryViewModel partition)
    {
        var lower = UsableStart;
        var upper = UsableEnd;
        foreach (var other in _partitions.Where(x => !ReferenceEquals(x, partition))
                     .Select(x => (x.Start, x.End))
                     .Concat(_reservedAreas.Select(x => (x.Start, End: x.Start + x.Size))))
        {
            if (other.End <= partition.Start)
                lower = Math.Max(lower, other.End);
            else if (other.Start >= partition.End)
                upper = Math.Min(upper, other.Start);
        }

        return (lower, upper);
    }

    /// <summary>
    /// Max size of partition with file system. Pfs3 partitions are limited to 101.6GB unless experimental sizes are used.
    /// </summary>
    public long GetMaxPartitionSize(string fileSystem, bool useExperimental) =>
        IsRdb && PartitionFileSystems.IsPfs3(fileSystem) && !useExperimental
            ? Pfs3MaxPartitionSize
            : long.MaxValue;

    // ─── Changes ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Add new partition in unallocated space with default file system, name and size filling unallocated space.
    /// </summary>
    public PartitionEntryViewModel? AddPartition(long freeStart, long freeEnd, bool useExperimental,
        AddPartitionPlacement placement = AddPartitionPlacement.All)
    {
        if (!CanAddPartition || GetAddRange(freeStart, freeEnd, placement) is not { } range)
            return null;

        var (start, end) = range;

        var entry = new PartitionEntryViewModel(TableType, true);
        if (IsRdb)
        {
            var isFirst = _partitions.Count == 0;
            entry.FileSystem = PartitionFileSystems.RdbOptions[0].Value;
            entry.DeviceName = GetNextDeviceName();
            entry.Label = isFirst ? "Workbench" : GetNextWorkLabel();
            entry.Bootable = isFirst;
        }
        else
        {
            entry.FileSystem = PartitionFileSystems.BasicOptions[0].Value;
            entry.Label = "Empty";
            entry.Bootable = TableType == PartitionTableType.MasterBootRecord && !_partitions.Any(x => x.Bootable);
        }

        // partition placed at end of unallocated space keeps its end, when limited by max partition size
        var size = Math.Min(end - start, GetMaxPartitionSize(entry.FileSystem, useExperimental));
        if (placement == AddPartitionPlacement.End && size < end - start)
        {
            start = AlignUp(end - size);
            size = end - start;
        }
        entry.SetRange(start, size);
        AddEntry(entry);
        OnChanged();
        return entry;
    }

    /// <summary>
    /// Partition can be added to segment, if it's unallocated space with room for a partition aligned in it.
    /// </summary>
    public bool CanAddPartitionTo(PartitionSegmentViewModel? segment,
        AddPartitionPlacement placement = AddPartitionPlacement.All) =>
        CanAddPartition && HasRoomFor(segment, placement);

    /// <summary>
    /// Unallocated space has room for a partition aligned in it with placement, regardless of max partitions.
    /// </summary>
    public bool HasRoomFor(PartitionSegmentViewModel? segment,
        AddPartitionPlacement placement = AddPartitionPlacement.All) =>
        segment is { IsUnallocated: true } && HasPartitionTable &&
        GetAddRange(segment.Start, segment.End, placement) != null;

    /// <summary>
    /// Get aligned range for adding a partition in unallocated space with placement. End of usable space is used as
    /// is. Returns null, if there's no room for a partition.
    /// </summary>
    private (long Start, long End)? GetAddRange(long freeStart, long freeEnd, AddPartitionPlacement placement)
    {
        var start = AlignUp(Math.Max(freeStart, UsableStart));
        var end = Math.Min(freeEnd, UsableEnd);
        if (end < UsableEnd)
            end = AlignDown(end);

        var third = AlignUp((end - start) / 3);
        switch (placement)
        {
            case AddPartitionPlacement.Start:
                end = Math.Min(end, start + third);
                break;
            case AddPartitionPlacement.End:
                start = Math.Max(start, AlignUp(end - third));
                break;
        }

        return end - start >= MinPartitionSize ? (start, end) : null;
    }

    public void DeletePartition(PartitionEntryViewModel partition)
    {
        if (!_partitions.Remove(partition))
            return;

        partition.PropertyChanged -= OnPartitionPropertyChanged;
        if (partition.IsExisting)
            _deletedPartitions.Add(partition);
        OnChanged();
    }

    /// <summary>
    /// Resize start of new partition keeping its end.
    /// </summary>
    public void ResizeStart(PartitionEntryViewModel partition, long start)
    {
        if (!partition.IsNew) return;
        var (lower, _) = GetBounds(partition);
        var minStart = AlignUp(lower);
        var maxStart = partition.End - MinPartitionSize;
        if (maxStart < minStart) return;
        var newStart = Math.Clamp(AlignNearest(start), minStart, maxStart);
        SetRange(partition, newStart, partition.End - newStart);
    }

    /// <summary>
    /// Resize end of new partition keeping its start.
    /// </summary>
    public void ResizeEnd(PartitionEntryViewModel partition, long end)
    {
        if (!partition.IsNew) return;
        var (_, upper) = GetBounds(partition);
        var minEnd = partition.Start + MinPartitionSize;
        if (upper < minEnd) return;
        var newEnd = upper - end < Alignment / 2 ? upper : AlignNearest(end);
        newEnd = Math.Clamp(newEnd, minEnd, upper);
        SetRange(partition, partition.Start, newEnd - partition.Start);
    }

    /// <summary>
    /// Move new partition keeping its size.
    /// </summary>
    public void Move(PartitionEntryViewModel partition, long start)
    {
        if (!partition.IsNew) return;
        var (lower, upper) = GetBounds(partition);
        var minStart = AlignUp(lower);
        var maxStart = AlignDown(upper - partition.Size);
        if (maxStart < minStart) return;
        var newStart = Math.Clamp(AlignNearest(start), minStart, maxStart);
        SetRange(partition, newStart, partition.Size);
    }

    /// <summary>
    /// Set size of new partition keeping its start, limited by free space following partition.
    /// </summary>
    public void SetSize(PartitionEntryViewModel partition, long size)
    {
        if (!partition.IsNew) return;
        var (_, upper) = GetBounds(partition);
        var newEnd = partition.Start + Math.Max(AlignUp(size), MinPartitionSize);
        SetRange(partition, partition.Start, Math.Min(newEnd, upper) - partition.Start);
    }

    /// <summary>
    /// Set free space preceding new partition moving it, keeping its size if possible.
    /// </summary>
    public void SetFreeSpacePreceding(PartitionEntryViewModel partition, long freeSpace)
    {
        if (!partition.IsNew) return;
        var (lower, upper) = GetBounds(partition);
        var minStart = AlignUp(lower);
        var maxStart = AlignDown(upper - MinPartitionSize);
        if (maxStart < minStart) return;
        var newStart = Math.Clamp(AlignNearest(lower + Math.Max(0, freeSpace)), minStart, maxStart);
        var newEnd = Math.Min(newStart + partition.Size, upper);
        SetRange(partition, newStart, newEnd - newStart);
    }

    /// <summary>
    /// Set free space following new partition changing its size.
    /// </summary>
    public void SetFreeSpaceFollowing(PartitionEntryViewModel partition, long freeSpace)
    {
        if (!partition.IsNew) return;
        var (_, upper) = GetBounds(partition);
        var minEnd = partition.Start + MinPartitionSize;
        if (upper < minEnd) return;
        var newEnd = freeSpace <= 0 ? upper : AlignNearest(upper - freeSpace);
        newEnd = Math.Clamp(newEnd, minEnd, upper);
        SetRange(partition, partition.Start, newEnd - partition.Start);
    }

    /// <summary>
    /// Restore state of partition, e.g. when editing partition is cancelled.
    /// </summary>
    public void RestorePartition(PartitionEntryViewModel partition, PartitionEntryState state)
    {
        if (!_partitions.Contains(partition)) return;
        partition.RestoreState(state);
        SetRange(partition, state.Start, state.Size);
    }

    /// <summary>
    /// Create plan with changes to apply. Formatting master boot record partition requiring a type change deletes
    /// and adds partition again at same position, as partition type can't be changed.
    /// </summary>
    public PartitionPlan CreatePlan(string path) => new()
    {
        Path = path,
        TableType = TableType,
        Initialize = IsInitialize,
        KeepMasterBootRecord = KeepMasterBootRecord,
        RdbBlockLo = RdbBlockLo,
        RdbSize = RdbSize,
        DiskSize = DiskSize,
        CylinderSize = IsRdb ? Alignment : 512,
        DeletePartitionNumbers = _deletedPartitions
            .Concat(_partitions.Where(x => x.RequiresTypeChange))
            .Where(x => x.Number.HasValue)
            .Select(x => x.Number!.Value)
            .ToList(),
        AddPartitions = _partitions.Where(x => x.IsNew || x.RequiresTypeChange).Select(ToPlannedPartition).ToList(),
        FormatPartitions = _partitions.Where(x => x.IsExisting && x.FormatRequested && !x.RequiresTypeChange)
            .Select(ToPlannedPartition).ToList(),
        UpdateFileSystems = _fileSystems.Where(x => x.IsUpdated).Select(x => new PlannedFileSystemUpdate
        {
            Number = x.Number!.Value,
            DosType = x.IsDosTypeChanged ? x.DosType : null,
            Name = x.IsNameChanged ? x.Name : null,
            Path = x.IsDataReplaced ? x.Path : null
        }).ToList(),
        DeleteFileSystemNumbers = GetFileSystemsToDelete(_deletedFileSystems).Select(x => x.Number!.Value).ToList(),
        AddFileSystems = _fileSystems.Where(x => x.IsNew).Select(x => new PlannedFileSystem
        {
            Path = x.Path,
            DosType = x.DosType,
            Name = x.Name,
            IsImport = x.IsFromMedia,
            Version = x.RequiresManualVersion ? (int?)x.ManualVersion : null,
            Revision = x.RequiresManualVersion ? (int?)x.ManualRevision : null
        }).ToList()
    };

    // ─── File systems ─────────────────────────────────────────────────────────

    /// <summary>
    /// Set file systems of rigid disk block edited in file systems dialog.
    /// </summary>
    public void SetFileSystems(IEnumerable<RdbFileSystemEntry> fileSystems,
        IEnumerable<RdbFileSystemEntry> deletedFileSystems)
    {
        var updatedFileSystems = fileSystems.ToList();
        var updatedDeletedFileSystems = deletedFileSystems.Where(x => x.IsExisting).ToList();
        _fileSystems.Clear();
        _fileSystems.AddRange(updatedFileSystems);
        _deletedFileSystems.Clear();
        _deletedFileSystems.AddRange(updatedDeletedFileSystems);
        OnChanged();
    }

    /// <summary>
    /// Existing file systems deleted, which are not replaced by a new file system with same dos type. Adding a file
    /// system replaces existing file system with same dos type, so file systems used by partitions can be replaced.
    /// </summary>
    private IEnumerable<RdbFileSystemEntry> GetFileSystemsToDelete(IEnumerable<RdbFileSystemEntry> deletedFileSystems,
        IEnumerable<RdbFileSystemEntry>? fileSystems = null)
    {
        var newDosTypes = (fileSystems ?? _fileSystems).Where(x => x.IsNew).Select(x => x.DosType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return deletedFileSystems.Where(x => x.IsExisting && !newDosTypes.Contains(x.OriginalDosType));
    }

    /// <summary>
    /// Validate file systems of rigid disk block. File systems edited in file systems dialog are validated, if set.
    /// </summary>
    public List<string> ValidateFileSystems(IReadOnlyList<RdbFileSystemEntry>? fileSystems = null,
        IReadOnlyList<RdbFileSystemEntry>? deletedFileSystems = null)
    {
        fileSystems ??= _fileSystems;
        deletedFileSystems ??= _deletedFileSystems;
        var errors = new List<string>();

        foreach (var fileSystem in fileSystems.Where(x => x.IsNew || x.IsUpdated))
        {
            var title = FormatFileSystemTitle(fileSystem);
            if (fileSystem.DosType.Length != 4)
                errors.Add($"DOS type '{fileSystem.DosType}' of {title} must be 4 characters, e.g. PDS3 or DOS3");

            if (fileSystem.IsFromMedia && string.IsNullOrWhiteSpace(fileSystem.Name))
                errors.Add($"Name of {title} is required to find it in media");

            if (fileSystem.IsNew && string.IsNullOrWhiteSpace(fileSystem.Path))
                errors.Add(fileSystem.IsFromMedia
                    ? $"Path to media is required for {title}"
                    : $"Path to file system file is required for {title}");

            if (fileSystem.HasSourceError)
                errors.Add($"{title}: {fileSystem.SourceError}");

            if (fileSystem.IsFromFile && fileSystem.Size > RdbFileSystemEntry.MaxFileSystemSize)
                errors.Add(
                    $"File system file of {title} is larger than max size {MediaOptions.FormatBytes(RdbFileSystemEntry.MaxFileSystemSize)}");

            if (fileSystem.RequiresManualVersion &&
                (!fileSystem.ManualVersion.HasValue || !fileSystem.ManualRevision.HasValue))
                errors.Add($"Version and revision are required for {title}, as file system file has no version string");
        }

        foreach (var dosType in fileSystems
                     .Where(x => x.DosType.Length > 0)
                     .GroupBy(x => x.DosType, StringComparer.OrdinalIgnoreCase)
                     .Where(x => x.Count() > 1)
                     .Select(x => x.Key))
            errors.Add(
                $"DOS type '{dosType}' is used by more than one file system. Delete the existing file system to replace it");

        // existing partitions use updated dos type of file system, when dos type of existing file system is updated
        var updatedDosTypes = fileSystems.Where(x => x.IsDosTypeChanged)
            .GroupBy(x => x.OriginalDosType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().DosType, StringComparer.OrdinalIgnoreCase);
        var partitions = _partitions.Where(x => x.IsExisting && !string.IsNullOrEmpty(x.ExistingDosType)).ToList();
        foreach (var fileSystem in GetFileSystemsToDelete(deletedFileSystems, fileSystems))
        {
            var partition = partitions.FirstOrDefault(x => string.Equals(
                updatedDosTypes.GetValueOrDefault(x.ExistingDosType, x.ExistingDosType), fileSystem.OriginalDosType,
                StringComparison.OrdinalIgnoreCase));
            if (partition != null)
                errors.Add(
                    $"File system #{fileSystem.Number} ({fileSystem.OriginalDosType}) can't be deleted, as it's used by partition #{partition.Number} {partition.DeviceName}. Delete the partition or add a file system with DOS type {fileSystem.OriginalDosType} to replace it");
        }

        return errors.Select(x => $"{char.ToUpperInvariant(x[0])}{x[1..]}").ToList();
    }

    /// <summary>
    /// Format file system for messages, e.g. file system #1 (PDS3) or new file system (PDS3).
    /// </summary>
    public static string FormatFileSystemTitle(RdbFileSystemEntry fileSystem) => fileSystem.IsExisting
        ? $"file system #{fileSystem.Number} ({fileSystem.DosType})"
        : $"new file system ({fileSystem.DosType})";

    private static PlannedPartition ToPlannedPartition(PartitionEntryViewModel partition) => new()
    {
        StartOffset = partition.Start,
        Size = partition.Size,
        FileSystem = partition.FileSystem,
        Label = partition.Label,
        DeviceName = partition.DeviceName,
        Bootable = partition.Bootable,
        IsPiStorm = partition.IsPiStorm
    };

    private void SetRange(PartitionEntryViewModel partition, long start, long size)
    {
        if (partition.Start == start && partition.Size == size) return;
        partition.SetRange(start, size);
        _partitions.Sort((a, b) => a.Start.CompareTo(b.Start));
        OnChanged();
    }

    private void AddEntry(PartitionEntryViewModel entry)
    {
        _partitions.Add(entry);
        _partitions.Sort((a, b) => a.Start.CompareTo(b.Start));
        entry.PropertyChanged += OnPartitionPropertyChanged;
    }

    private string GetNextDeviceName()
    {
        var names = _partitions.Select(x => x.DeviceName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var number = 0;
        while (names.Contains($"DH{number}"))
            number++;
        return $"DH{number}";
    }

    private string GetNextWorkLabel()
    {
        var labels = _partitions.Select(x => x.Label).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!labels.Contains("Work"))
            return "Work";
        var number = 2;
        while (labels.Contains($"Work{number}"))
            number++;
        return $"Work{number}";
    }

    // range changes are raised by layout, so a resize raises changed once
    private void OnPartitionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PartitionEntryViewModel.Start) or nameof(PartitionEntryViewModel.Size)
            or nameof(PartitionEntryViewModel.End))
            return;
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Partition table of disk being partitioned. Disks can have multiple partition tables, which are edited as one,
/// like hybrid disks with a rigid disk block and a master boot record or PiStorm disks with rigid disk blocks in
/// master boot record partitions.
/// </summary>
public record DiskPartitionTable(PartitionLayout Layout, string Path)
{
    /// <summary>
    /// Master boot record partition partition table is in, e.g. PiStorm rigid disk block. Null for disk.
    /// </summary>
    public PartitionEntryViewModel? Container { get; init; }

    /// <summary>
    /// Layout of master boot record container partition is in.
    /// </summary>
    public PartitionLayout? ContainerLayout { get; init; }

    /// <summary>
    /// Partition table is for the whole disk, not a part of it like a PiStorm disk.
    /// </summary>
    public bool IsDiskPath => Container == null;

    /// <summary>
    /// Start of partition table on disk.
    /// </summary>
    public long Offset => Container?.Start ?? 0;

    /// <summary>
    /// Partition table in a partition is edited, unless partition is deleted or formatted.
    /// </summary>
    public bool IsActive => Container == null ||
                            (ContainerLayout!.Partitions.Contains(Container) && !Container.FormatRequested &&
                             (Container.IsExisting || Container.IsPiStorm));

    /// <summary>
    /// Partition table is in a new PiStorm partition, which is added before partition table is applied.
    /// </summary>
    public bool IsInNewPartition => Container is { IsNew: true };

    /// <summary>
    /// Name of partition partition table is in, e.g. Master Boot Record partition #2.
    /// </summary>
    public string ContainerName => Container == null
        ? string.Empty
        : Container.IsNew
            ? $"new {PartitionLayout.FormatTableTypeName(PartitionTableType.MasterBootRecord)} PiStorm partition"
            : $"{PartitionLayout.FormatTableTypeName(PartitionTableType.MasterBootRecord)} partition #{Container.Number}";
}

/// <summary>
/// Partition table of disk listed with its size and color used to show partitions belonging to it.
/// </summary>
public class PartitionTableSummary
{
    public string Name { get; init; } = string.Empty;
    public string Abbreviation { get; init; } = string.Empty;
    public string SizeText { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public IBrush ColorBrush => new SolidColorBrush(Avalonia.Media.Color.Parse(Color));
    public bool HasAbbreviation => !string.IsNullOrEmpty(Abbreviation);
}
