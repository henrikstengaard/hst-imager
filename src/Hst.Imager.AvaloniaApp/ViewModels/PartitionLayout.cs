using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
    /// <summary>
    /// File system value for new master boot record and guid partitions, which are added without being formatted.
    /// </summary>
    public const string None = "none";

    public static readonly List<SelectOption> BasicOptions =
    [
        new() { Title = "FAT32", Value = "fat32" },
        new() { Title = "exFAT", Value = "exfat" },
        new() { Title = "NTFS", Value = "ntfs" }
    ];

    /// <summary>
    /// File systems for new master boot record and guid partitions, which can also be added without formatting.
    /// </summary>
    public static readonly List<SelectOption> NewBasicOptions =
        [..BasicOptions, new() { Title = "None (not formatted)", Value = None }];

    public static readonly List<SelectOption> RdbOptions =
    [
        new() { Title = "PDS\\3 (direct scsi)", Value = "pds3" },
        new() { Title = "PFS\\3", Value = "pfs3" },
        new() { Title = "DOS\\3", Value = "dos3" },
        new() { Title = "DOS\\7 (long filename)", Value = "dos7" }
    ];

    public static List<SelectOption> GetOptions(PartitionTableType tableType, bool isNew) =>
        tableType == PartitionTableType.RigidDiskBlock ? RdbOptions : isNew ? NewBasicOptions : BasicOptions;

    public static string GetTitle(string fileSystem) => fileSystem switch
    {
        "fat32" => "FAT32",
        "exfat" => "exFAT",
        "ntfs" => "NTFS",
        None => "Not formatted",
        _ => FormatDosType(fileSystem)
    };

    /// <summary>
    /// Format dos type with backslash before its number, e.g. PFS\3 for pfs3. Other values are returned as is.
    /// </summary>
    public static string FormatDosType(string dosType) =>
        dosType.Length == 4 && char.IsDigit(dosType[3])
            ? $"{dosType[..3].ToUpperInvariant()}\\{dosType[3]}"
            : dosType;

    public static bool IsPfs3(string fileSystem) => fileSystem is "pfs3" or "pds3";

    /// <summary>
    /// Bios type of master boot record partition added with file system, FAT32 LBA (0xc) or NTFS/exFAT (0x7).
    /// </summary>
    public static int GetMbrBiosType(string fileSystem) => fileSystem == "fat32" ? 0xc : 0x7;

    /// <summary>
    /// Fast file system is used by dos types DOS\0 to DOS\7.
    /// </summary>
    public static bool IsFastFileSystem(string fileSystem) =>
        fileSystem.Length == 4 && fileSystem.StartsWith("dos", StringComparison.OrdinalIgnoreCase) &&
        fileSystem[3] is >= '0' and <= '7';

    /// <summary>
    /// Rigid disk block partitions can be formatted with fast file system and pfs3.
    /// </summary>
    public static bool IsRdbFormattable(string fileSystem) =>
        IsPfs3(fileSystem.ToLowerInvariant()) || IsFastFileSystem(fileSystem);

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
        if (normalized.Contains("sfs"))
            return "#D9C21A";
        return "#7A8FA6";
    }
}

/// <summary>
/// Partition type of new partition, which is a bios type for master boot record, a guid for guid partition table and
/// a dos type for rigid disk block.
/// </summary>
public class PartitionTypeOption : SelectOption
{
    /// <summary>
    /// Value of option to enter partition type manually.
    /// </summary>
    public const string CustomValue = "custom";

    /// <summary>
    /// File systems partitions of type can be formatted with. Empty, if partitions of type aren't formatted.
    /// </summary>
    public IReadOnlyList<string> FileSystems { get; init; } = [];

    public bool IsCustom => Value == CustomValue;
}

/// <summary>
/// Common partition types of new partitions, which can be selected or entered manually.
/// </summary>
public static class PartitionTypes
{
    public const int PiStormBiosType = 0x76;

    private static readonly string[] Fat32 = ["fat32"];
    private static readonly string[] BasicData = ["fat32", "exfat", "ntfs"];

    public static readonly List<PartitionTypeOption> MbrOptions =
    [
        new() { Title = "FAT32 LBA (0x0C)", Value = "0x0c", FileSystems = Fat32 },
        new() { Title = "FAT32 CHS (0x0B)", Value = "0x0b", FileSystems = Fat32 },
        new() { Title = "NTFS / exFAT (0x07)", Value = "0x07", FileSystems = ["ntfs", "exfat"] },
        new() { Title = "FAT16 LBA (0x0E)", Value = "0x0e" },
        new() { Title = "FAT16 (0x06)", Value = "0x06" },
        new() { Title = "EFI System (0xEF)", Value = "0xef", FileSystems = Fat32 },
        new() { Title = "Linux (0x83)", Value = "0x83" },
        new() { Title = "Linux swap (0x82)", Value = "0x82" },
        new() { Title = "Linux LVM (0x8E)", Value = "0x8e" },
        new() { Title = "PiStorm Rigid Disk Block (0x76)", Value = "0x76" },
        new() { Title = "Non-file system data (0xDA)", Value = "0xda" },
        new() { Title = "Enter bios type manually", Value = PartitionTypeOption.CustomValue }
    ];

    public static readonly List<PartitionTypeOption> GptOptions =
    [
        new() { Title = "Microsoft basic data", Value = "ebd0a0a2-b9e5-4433-87c0-68b6b72699c7", FileSystems = BasicData },
        new() { Title = "EFI System", Value = "c12a7328-f81f-11d2-ba4b-00a0c93ec93b", FileSystems = Fat32 },
        new() { Title = "Microsoft reserved", Value = "e3c9e316-0b5c-4db8-817d-f92df00215ae" },
        new() { Title = "Windows recovery", Value = "de94bba4-06d1-4d40-a16a-bfd50179d6ac", FileSystems = ["ntfs"] },
        new() { Title = "Linux file system", Value = "0fc63daf-8483-4772-8e79-3d69d8477de4" },
        new() { Title = "Linux root (x86-64)", Value = "4f68bce3-e8cd-4db1-96e7-fbcaf984b709" },
        new() { Title = "Linux swap", Value = "0657fd6d-a4ab-43c4-84e5-0933c84b4f4f" },
        new() { Title = "Linux LVM", Value = "e6d6d379-f507-44c2-a23c-238f2a3df928" },
        new() { Title = "Apple HFS+", Value = "48465300-0000-11aa-aa11-00306543ecac" },
        new() { Title = "Apple APFS", Value = "7c3457ef-0000-11aa-aa11-00306543ecac" },
        new() { Title = "Enter partition type guid manually", Value = PartitionTypeOption.CustomValue }
    ];

    public static readonly List<PartitionTypeOption> RdbOptions =
    [
        new() { Title = "PDS\\3 (pfs3aio direct scsi)", Value = "PDS3" },
        new() { Title = "PFS\\3 (pfs3aio)", Value = "PFS3" },
        new() { Title = "DOS\\3 (FFS international)", Value = "DOS3" },
        new() { Title = "DOS\\7 (FFS long filenames)", Value = "DOS7" },
        new() { Title = "DOS\\0 (OFS)", Value = "DOS0" },
        new() { Title = "DOS\\1 (FFS)", Value = "DOS1" },
        new() { Title = "DOS\\2 (OFS international)", Value = "DOS2" },
        new() { Title = "DOS\\4 (OFS dircache)", Value = "DOS4" },
        new() { Title = "DOS\\5 (FFS dircache)", Value = "DOS5" },
        new() { Title = "DOS\\6 (OFS long filenames)", Value = "DOS6" },
        new() { Title = "SFS\\0 (SmartFileSystem)", Value = "SFS0" },
        new() { Title = "SFS\\2 (SmartFileSystem large partitions)", Value = "SFS2" },
        new() { Title = "Enter DOS type manually", Value = PartitionTypeOption.CustomValue }
    ];

    public static List<PartitionTypeOption> GetOptions(PartitionTableType tableType) => tableType switch
    {
        PartitionTableType.MasterBootRecord => MbrOptions,
        PartitionTableType.GuidPartitionTable => GptOptions,
        PartitionTableType.RigidDiskBlock => RdbOptions,
        _ => []
    };

    /// <summary>
    /// Default partition type of new partition with file system, e.g. FAT32 LBA (0x0C) for FAT32 in master boot
    /// record.
    /// </summary>
    public static string GetDefault(PartitionTableType tableType, string fileSystem) => tableType switch
    {
        PartitionTableType.MasterBootRecord => $"0x{PartitionFileSystems.GetMbrBiosType(fileSystem):x2}",
        PartitionTableType.GuidPartitionTable => GptOptions[0].Value,
        _ => fileSystem.ToUpperInvariant()
    };

    /// <summary>
    /// Parse bios type in hex with or without 0x prefix, e.g. 0x0c, 0c or c.
    /// </summary>
    public static bool TryParseBiosType(string value, out int biosType)
    {
        biosType = 0;
        var hex = value.Trim();
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex[2..];
        return hex.Length is > 0 and <= 2 && int.TryParse(hex, NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out biosType);
    }

    /// <summary>
    /// Normalize partition type to format used by partition commands, e.g. 0x0c for bios type, guid in lowercase
    /// and dos type in uppercase without backslash.
    /// </summary>
    public static string Normalize(PartitionTableType tableType, string value) => tableType switch
    {
        PartitionTableType.MasterBootRecord when TryParseBiosType(value, out var biosType) => $"0x{biosType:x2}",
        PartitionTableType.GuidPartitionTable when Guid.TryParse(value, out var guid) => guid.ToString("D"),
        PartitionTableType.RigidDiskBlock => RdbFileSystemEntry.NormalizeDosType(value),
        _ => value.Trim()
    };

    /// <summary>
    /// Validate partition type of new partition. Returns error or null, if partition type is valid.
    /// </summary>
    public static string? Validate(PartitionTableType tableType, string value) => tableType switch
    {
        PartitionTableType.MasterBootRecord when string.IsNullOrWhiteSpace(value) =>
            "Bios type is required, e.g. 0x0C",
        PartitionTableType.GuidPartitionTable when string.IsNullOrWhiteSpace(value) =>
            "Partition type guid is required, e.g. ebd0a0a2-b9e5-4433-87c0-68b6b72699c7",
        PartitionTableType.RigidDiskBlock when string.IsNullOrWhiteSpace(value) =>
            "DOS type is required, e.g. PDS3 or DOS3",
        PartitionTableType.MasterBootRecord => !TryParseBiosType(value, out var biosType) || biosType == 0
            ? $"Bios type '{value}' must be a hex value between 0x01 and 0xFF, e.g. 0x0C"
            : biosType is 0x05 or 0x0f
                ? $"Bios type '{value}' is an extended partition, which isn't supported"
                : null,
        PartitionTableType.GuidPartitionTable => Guid.TryParse(value, out var guid) && guid != Guid.Empty
            ? null
            : $"Partition type '{value}' must be a guid, e.g. ebd0a0a2-b9e5-4433-87c0-68b6b72699c7",
        PartitionTableType.RigidDiskBlock => RdbFileSystemEntry.NormalizeDosType(value).Length == 4
            ? null
            : $"DOS type '{value}' must be 4 characters, e.g. PDS3 or DOS3",
        _ => null
    };

    /// <summary>
    /// Format partition type for pending operations, e.g. FAT32 LBA (0x0C), Microsoft basic data (guid) or the
    /// partition type itself, if it's not a common partition type.
    /// </summary>
    public static string GetTitle(PartitionTableType tableType, string value)
    {
        var option = GetOptions(tableType).FirstOrDefault(x =>
            !x.IsCustom && string.Equals(x.Value, value, StringComparison.OrdinalIgnoreCase));
        return tableType switch
        {
            PartitionTableType.GuidPartitionTable => option == null ? value : $"{option.Title} ({value})",
            PartitionTableType.RigidDiskBlock => PartitionFileSystems.FormatDosType(value),
            _ => option?.Title ?? value
        };
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
    private string _partitionType = string.Empty;
    private bool _isCustomPartitionType;
    private string _customPartitionTypeText = string.Empty;
    private string _label = string.Empty;
    private string _deviceName = string.Empty;
    private bool _bootable;
    private bool _formatRequested;
    private List<PartitionTypeOption> _partitionTypeOptions;
    private RdbPropertiesText _rdb = RdbPropertiesText.From(RdbPartitionProperties.Default);

    public PartitionEntryViewModel(PartitionTableType tableType, bool isNew)
    {
        TableType = tableType;
        IsNew = isNew;
        FileSystemOptions = PartitionFileSystems.GetOptions(tableType, isNew);
        _partitionTypeOptions = PartitionTypes.GetOptions(tableType);
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
    /// Partition type of existing partition in format used for new partitions, e.g. 0x0c for master boot record,
    /// guid for guid partition table and dos type for rigid disk block.
    /// </summary>
    public string ExistingPartitionType { get; init; } = string.Empty;

    /// <summary>
    /// Device name of existing rigid disk block partition read from disk.
    /// </summary>
    public string ExistingDeviceName { get; init; } = string.Empty;

    /// <summary>
    /// Bootable of existing rigid disk block partition read from disk.
    /// </summary>
    public bool ExistingBootable { get; init; }

    /// <summary>
    /// Properties of existing rigid disk block partition read from disk.
    /// </summary>
    public RdbPartitionProperties? ExistingRdbProperties { get; init; }

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

    /// <summary>
    /// File system partition is formatted with. Dos type in lowercase for rigid disk block partitions, e.g. pds3.
    /// </summary>
    public string FileSystem
    {
        get => _fileSystem;
        set
        {
            this.RaiseAndSetIfChanged(ref _fileSystem, value);
            this.RaisePropertyChanged(nameof(FileSystemOption));
            if (IsRdb)
            {
                this.RaisePropertyChanged(nameof(PartitionType));
                this.RaisePropertyChanged(nameof(PartitionTypeOption));
                this.RaisePropertyChanged(nameof(CustomPartitionType));
            }
            RaiseTypeChanged();
        }
    }

    /// <summary>
    /// File system selected in partition dialog. Partition type of new master boot record and guid partition is
    /// changed to default partition type of file system, if selected partition type doesn't support file system.
    /// </summary>
    public SelectOption? FileSystemOption
    {
        get => FileSystemOptions.FirstOrDefault(x => x.Value == _fileSystem);
        set
        {
            if (value == null || value.Value == _fileSystem)
                return;
            FileSystem = value.Value;
            if (!IsNew || IsRdb || _isCustomPartitionType || value.Value == PartitionFileSystems.None)
                return;
            var option = _partitionTypeOptions.FirstOrDefault(x => !x.IsCustom && x.Value == _partitionType);
            if (option == null || !option.FileSystems.Contains(value.Value))
                PartitionType = PartitionTypes.GetDefault(TableType, value.Value);
        }
    }

    /// <summary>
    /// Partition type of new partition, which is bios type for master boot record, e.g. 0x0c, guid for guid partition
    /// table and dos type for rigid disk block, e.g. PDS3. Dos type is file system of rigid disk block partitions.
    /// </summary>
    public string PartitionType
    {
        get => IsRdb ? _fileSystem.ToUpperInvariant() : _partitionType;
        set
        {
            if (IsRdb)
            {
                FileSystem = value.ToLowerInvariant();
                return;
            }

            this.RaiseAndSetIfChanged(ref _partitionType, value);
            this.RaisePropertyChanged(nameof(PartitionTypeOption));
            this.RaisePropertyChanged(nameof(CustomPartitionType));
            RaiseTypeChanged();
        }
    }

    /// <summary>
    /// Common partition types to select. Rigid disk block partitions also list dos types of file systems in rigid
    /// disk block.
    /// </summary>
    public List<PartitionTypeOption> PartitionTypeOptions
    {
        get => _partitionTypeOptions;
        set
        {
            this.RaiseAndSetIfChanged(ref _partitionTypeOptions, value);
            this.RaisePropertyChanged(nameof(PartitionTypeOption));
        }
    }

    /// <summary>
    /// Partition type selected in partition dialog. File system of new master boot record and guid partition is
    /// changed to first file system supported by partition type or none, if partition type isn't formatted.
    /// </summary>
    public PartitionTypeOption? PartitionTypeOption
    {
        get => _isCustomPartitionType
            ? _partitionTypeOptions.FirstOrDefault(x => x.IsCustom)
            : _partitionTypeOptions.FirstOrDefault(x => !x.IsCustom && string.Equals(x.Value, PartitionType,
                  StringComparison.OrdinalIgnoreCase))
              ?? _partitionTypeOptions.FirstOrDefault(x => x.IsCustom);
        set
        {
            if (value == null)
                return;

            // partition type is kept, when switching to enter it manually
            if (value.IsCustom && !_isCustomPartitionType)
                _customPartitionTypeText = PartitionType;
            IsCustomPartitionType = value.IsCustom;
            if (value.IsCustom)
            {
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(CustomPartitionType));
                return;
            }

            PartitionType = value.Value;
            if (!IsRdb && !value.FileSystems.Contains(_fileSystem))
                FileSystem = value.FileSystems.FirstOrDefault() ?? PartitionFileSystems.None;
        }
    }

    /// <summary>
    /// Partition type is entered manually instead of selected from common partition types.
    /// </summary>
    public bool IsCustomPartitionType
    {
        get => _isCustomPartitionType;
        private set => this.RaiseAndSetIfChanged(ref _isCustomPartitionType, value);
    }

    /// <summary>
    /// Partition type entered manually. Text is kept as entered, so it isn't changed while typing, and partition
    /// type is set to normalized value, when it's valid.
    /// </summary>
    public string CustomPartitionType
    {
        get => _isCustomPartitionType ? _customPartitionTypeText : PartitionType;
        set
        {
            _customPartitionTypeText = value;
            PartitionType = PartitionTypes.Validate(TableType, value) == null
                ? PartitionTypes.Normalize(TableType, value)
                : value.Trim();
        }
    }

    public string PartitionTypeLabel => IsRdb ? "DOS type" : "Partition type";

    public string CustomPartitionTypeLabel => TableType switch
    {
        PartitionTableType.MasterBootRecord => "Bios type (hex)",
        PartitionTableType.GuidPartitionTable => "Partition type guid",
        _ => "DOS type (4 characters)"
    };

    /// <summary>
    /// Error for invalid partition type of new partition or null, if it's valid.
    /// </summary>
    public string? PartitionTypeError => IsNew ? PartitionTypes.Validate(TableType, PartitionType) : null;

    public bool HasPartitionTypeError => PartitionTypeError != null;

    /// <summary>
    /// File system displayed, which is the new file system for new or formatted partitions and partition type for new
    /// partitions, which aren't formatted.
    /// </summary>
    public string FileSystemDisplay => IsPiStorm
        ? "PiStorm RDB"
        : IsNew && !IsFormattable
            ? PartitionTypes.GetTitle(TableType, PartitionType)
            : IsNew || (_formatRequested && !IsRdb)
                ? PartitionFileSystems.GetTitle(_fileSystem)
                : ExistingFileSystem;

    /// <summary>
    /// Partition type can be selected for new partitions.
    /// </summary>
    public bool CanEditPartitionType => IsNew;

    /// <summary>
    /// New master boot record partition with bios type 0x76 is a PiStorm partition containing a rigid disk block,
    /// which isn't formatted.
    /// </summary>
    public bool IsPiStorm => IsNew && IsMbr && PartitionTypes.TryParseBiosType(_partitionType, out var biosType) &&
                             biosType == PartitionTypes.PiStormBiosType;

    /// <summary>
    /// New partition is formatted with a file system. Rigid disk block partitions can only be formatted with fast file
    /// system and pfs3, master boot record and guid partitions are formatted, unless no file system is selected.
    /// </summary>
    public bool IsFormattable => IsRdb
        ? PartitionFileSystems.IsRdbFormattable(_fileSystem)
        : !IsPiStorm && _fileSystem != PartitionFileSystems.None;

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
            this.RaisePropertyChanged(nameof(CanEditRdbFileSystemProperties));

            // file system properties of existing partition can only be changed, when it's formatted
            if (!value && ExistingRdbProperties is { } existing)
            {
                FileSystemBlockSize = existing.FileSystemBlockSize;
                Reserved = existing.Reserved.ToString();
                PreAlloc = existing.PreAlloc.ToString();
            }
        }
    }

    // ─── Rigid disk block properties ──────────────────────────────────────────

    /// <summary>
    /// File system block sizes, which can be selected for rigid disk block partitions.
    /// </summary>
    public static IReadOnlyList<int> FileSystemBlockSizes { get; } = [512, 1024, 2048, 4096, 8192, 16384, 32768];

    /// <summary>
    /// Number of buffers used by file system.
    /// </summary>
    public string Buffers
    {
        get => _rdb.Buffers;
        set => SetRdb(_rdb with { Buffers = value });
    }

    /// <summary>
    /// Max transfer in bytes as hex, e.g. 0x1FE00, or integer value.
    /// </summary>
    public string MaxTransfer
    {
        get => _rdb.MaxTransfer;
        set => SetRdb(_rdb with { MaxTransfer = value });
    }

    /// <summary>
    /// Address mask as hex, e.g. 0x7FFFFFFE, or integer value.
    /// </summary>
    public string Mask
    {
        get => _rdb.Mask;
        set => SetRdb(_rdb with { Mask = value });
    }

    public string BootPriority
    {
        get => _rdb.BootPriority;
        set => SetRdb(_rdb with { BootPriority = value });
    }

    public bool NoMount
    {
        get => _rdb.NoMount;
        set => SetRdb(_rdb with { NoMount = value });
    }

    /// <summary>
    /// Blocks reserved at start of partition.
    /// </summary>
    public string Reserved
    {
        get => _rdb.Reserved;
        set => SetRdb(_rdb with { Reserved = value });
    }

    /// <summary>
    /// Blocks reserved at end of partition.
    /// </summary>
    public string PreAlloc
    {
        get => _rdb.PreAlloc;
        set => SetRdb(_rdb with { PreAlloc = value });
    }

    public int FileSystemBlockSize
    {
        get => _rdb.FileSystemBlockSize;
        set => SetRdb(_rdb with { FileSystemBlockSize = value });
    }

    /// <summary>
    /// Properties of rigid disk block partition or null, if a property entered is invalid.
    /// </summary>
    public RdbPartitionProperties? RdbProperties => _rdb.Parse();

    /// <summary>
    /// Error for invalid rigid disk block property or null, if they are valid.
    /// </summary>
    public string? RdbPropertiesError => IsRdb ? _rdb.Validate() : null;

    public bool HasRdbPropertiesError => RdbPropertiesError != null;

    /// <summary>
    /// Partition has errors preventing partition dialog from being closed with OK.
    /// </summary>
    public bool HasError => HasPartitionTypeError || HasRdbPropertiesError;

    /// <summary>
    /// Existing rigid disk block partition has changed device name, bootable or properties. File system properties
    /// block size, reserved and pre alloc are only changed, when partition is formatted.
    /// </summary>
    public bool HasRdbChanges => GetRdbUpdate() != null;

    /// <summary>
    /// Get update of existing rigid disk block partition with changed properties only or null, if partition isn't
    /// changed.
    /// </summary>
    public PlannedPartitionUpdate? GetRdbUpdate()
    {
        if (!IsRdb || IsNew || Number == null || ExistingRdbProperties is not { } existing ||
            RdbProperties is not { } properties)
            return null;

        var update = new PlannedPartitionUpdate
        {
            Number = Number.Value,
            DeviceName = !string.Equals(_deviceName, ExistingDeviceName, StringComparison.OrdinalIgnoreCase)
                ? _deviceName
                : null,
            Bootable = _bootable != ExistingBootable ? _bootable : null,
            BootPriority = properties.BootPriority != existing.BootPriority ? properties.BootPriority : null,
            NoMount = properties.NoMount != existing.NoMount ? properties.NoMount : null,
            Buffers = properties.Buffers != existing.Buffers ? properties.Buffers : null,
            MaxTransfer = properties.MaxTransfer != existing.MaxTransfer ? properties.MaxTransfer : null,
            Mask = properties.Mask != existing.Mask ? properties.Mask : null,
            Reserved = _formatRequested && properties.Reserved != existing.Reserved ? properties.Reserved : null,
            PreAlloc = _formatRequested && properties.PreAlloc != existing.PreAlloc ? properties.PreAlloc : null,
            FileSystemBlockSize = _formatRequested && properties.FileSystemBlockSize != existing.FileSystemBlockSize
                ? properties.FileSystemBlockSize
                : null
        };

        return update is
        {
            DeviceName: null, Bootable: null, BootPriority: null, NoMount: null, Buffers: null, MaxTransfer: null,
            Mask: null, Reserved: null, PreAlloc: null, FileSystemBlockSize: null
        }
            ? null
            : update;
    }

    /// <summary>
    /// Set rigid disk block properties, e.g. read from partition block of existing partition.
    /// </summary>
    public void SetRdbProperties(RdbPartitionProperties properties) => SetRdb(RdbPropertiesText.From(properties));

    private void SetRdb(RdbPropertiesText rdb)
    {
        if (rdb == _rdb)
            return;
        _rdb = rdb;
        this.RaisePropertyChanged(nameof(Buffers));
        this.RaisePropertyChanged(nameof(MaxTransfer));
        this.RaisePropertyChanged(nameof(Mask));
        this.RaisePropertyChanged(nameof(BootPriority));
        this.RaisePropertyChanged(nameof(NoMount));
        this.RaisePropertyChanged(nameof(Reserved));
        this.RaisePropertyChanged(nameof(PreAlloc));
        this.RaisePropertyChanged(nameof(FileSystemBlockSize));
        this.RaisePropertyChanged(nameof(RdbProperties));
        this.RaisePropertyChanged(nameof(RdbPropertiesError));
        this.RaisePropertyChanged(nameof(HasRdbPropertiesError));
        this.RaisePropertyChanged(nameof(HasError));
    }

    /// <summary>
    /// File system can be changed for new and formatted master boot record and guid partitions. File system of new
    /// rigid disk block partitions is their dos type and formatting rigid disk block partitions uses dos type of the
    /// partition.
    /// </summary>
    public bool CanEditFileSystem => !IsRdb && ((IsNew && !IsPiStorm) || _formatRequested);

    /// <summary>
    /// File system is shown for master boot record and guid partitions, except PiStorm partitions, and for existing
    /// rigid disk block partitions. Dos type is shown as partition type for new rigid disk block partitions.
    /// </summary>
    public bool ShowFileSystem => !IsPiStorm && !(IsNew && IsRdb);

    public bool CanEditLabel => (IsNew && IsFormattable) || _formatRequested;

    /// <summary>
    /// Device name, bootable and properties not affecting file system can be changed for new and existing rigid disk
    /// block partitions.
    /// </summary>
    public bool CanEditRdbProperties => IsRdb;

    /// <summary>
    /// File system block size, reserved and pre alloc blocks can be changed for new rigid disk block partitions and
    /// existing partitions, which are formatted.
    /// </summary>
    public bool CanEditRdbFileSystemProperties => IsRdb && (IsNew || _formatRequested);

    public bool CanEditActive => IsNew && IsMbr;

    /// <summary>
    /// Get editable state of partition, which can be restored when editing partition is cancelled.
    /// </summary>
    public PartitionEntryState GetState() =>
        new(_start, _size, _fileSystem, _partitionType, _isCustomPartitionType, _label, _deviceName, _bootable,
            _formatRequested, _rdb);

    /// <summary>
    /// Restore editable state of partition except range, which is restored by partition layout.
    /// Label is restored after format requested, as requesting format sets an empty label.
    /// </summary>
    internal void RestoreState(PartitionEntryState state)
    {
        FormatRequested = state.FormatRequested;
        FileSystem = state.FileSystem;
        IsCustomPartitionType = state.IsCustomPartitionType;
        if (!IsRdb)
            PartitionType = state.PartitionType;
        _customPartitionTypeText = PartitionType;
        this.RaisePropertyChanged(nameof(CustomPartitionType));
        Label = state.Label;
        DeviceName = state.DeviceName;
        Bootable = state.Bootable;
        SetRdb(state.Rdb);
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

    private void RaiseTypeChanged()
    {
        this.RaisePropertyChanged(nameof(IsPiStorm));
        this.RaisePropertyChanged(nameof(IsFormattable));
        this.RaisePropertyChanged(nameof(PartitionTypeError));
        this.RaisePropertyChanged(nameof(HasPartitionTypeError));
        this.RaisePropertyChanged(nameof(HasError));
        this.RaisePropertyChanged(nameof(CanEditFileSystem));
        this.RaisePropertyChanged(nameof(ShowFileSystem));
        this.RaisePropertyChanged(nameof(CanEditLabel));
        this.RaisePropertyChanged(nameof(FileSystemDisplay));
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
public record PartitionEntryState(long Start, long Size, string FileSystem, string PartitionType,
    bool IsCustomPartitionType, string Label, string DeviceName, bool Bootable, bool FormatRequested,
    RdbPropertiesText Rdb);

/// <summary>
/// Rigid disk block partition properties as entered in partition dialog, which are parsed and validated.
/// Max transfer and mask are hex, e.g. 0x1FE00, or integer values.
/// </summary>
public record RdbPropertiesText(string Buffers, string MaxTransfer, string Mask, string BootPriority, bool NoMount,
    string Reserved, string PreAlloc, int FileSystemBlockSize)
{
    public static RdbPropertiesText From(RdbPartitionProperties properties) => new(
        properties.Buffers.ToString(), $"0x{properties.MaxTransfer:X}", $"0x{properties.Mask:X}",
        properties.BootPriority.ToString(), properties.NoMount, properties.Reserved.ToString(),
        properties.PreAlloc.ToString(), properties.FileSystemBlockSize);

    public RdbPartitionProperties? Parse() => Validate() == null
        ? new RdbPartitionProperties(ParseUInt(Buffers)!.Value, ParseUInt(MaxTransfer)!.Value, ParseUInt(Mask)!.Value,
            int.Parse(BootPriority.Trim()), NoMount, ParseUInt(Reserved)!.Value, ParseUInt(PreAlloc)!.Value,
            FileSystemBlockSize)
        : null;

    /// <summary>
    /// Validate properties and return error for first invalid property or null, if they are valid.
    /// </summary>
    public string? Validate()
    {
        if (ParseUInt(Buffers) is not > 0)
            return "Buffers must be a number larger than 0";
        if (ParseUInt(MaxTransfer) is not > 0)
            return "Max transfer must be a hex value, e.g. 0x1FE00, or a number larger than 0";
        if (ParseUInt(Mask) is not > 0)
            return "Mask must be a hex value, e.g. 0x7FFFFFFE, or a number larger than 0";
        if (!int.TryParse(BootPriority.Trim(), out var bootPriority) || bootPriority is < -128 or > 127)
            return "Boot priority must be a number between -128 and 127";
        if (ParseUInt(Reserved) == null)
            return "Reserved blocks must be a number";
        if (ParseUInt(PreAlloc) == null)
            return "Pre alloc blocks must be a number";
        if (FileSystemBlockSize < 512 || FileSystemBlockSize % 512 != 0)
            return "File system block size must be dividable by 512";
        return null;
    }

    /// <summary>
    /// Parse hex value prefixed with 0x or $ or integer value.
    /// </summary>
    private static uint? ParseUInt(string value)
    {
        value = value.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || value.StartsWith('$'))
            return uint.TryParse(value[(value[0] == '$' ? 1 : 2)..], NumberStyles.HexNumber,
                null, out var hex)
                ? hex
                : null;
        return uint.TryParse(value, out var integer) ? integer : null;
    }
}

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
            StatusIcon = IsReserved ? "fa-lock" : string.Empty;
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
                    : partition.IsNew
                        ? "New partition"
                        : $"Partition #{partition.Number}";
        FileSystem = partition.FileSystemDisplay;
        Color = PartitionFileSystems.GetColor(partition.IsNew || (partition.FormatRequested && !partition.IsRdb)
            ? partition.FileSystem
            : partition.ExistingFileSystem);
        UsedText = partition.UsedSize.HasValue && !partition.FormatRequested
            ? MediaOptions.FormatBytes(partition.UsedSize.Value)
            : string.Empty;
        Flags = partition.Bootable ? layout.IsRdb ? "bootable" : "active" : string.Empty;
        Status = partition.IsNew ? "New" : partition.FormatRequested ? "Format" : string.Empty;
        StatusIcon = partition.IsNew ? "fa-circle-plus" : partition.FormatRequested ? "fa-eraser" : string.Empty;
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
    /// <summary>
    /// Partition table is shown for partitions, reserved areas and partition tables, not for unallocated space.
    /// </summary>
    public bool HasTableType => !string.IsNullOrEmpty(TableType) && !IsUnallocated;
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

    /// <summary>
    /// Icon shown next to name for status, e.g. new or formatted partition.
    /// </summary>
    public string StatusIcon { get; } = string.Empty;

    public bool HasStatus => !string.IsNullOrEmpty(StatusIcon);
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
    /// Start of area used by partition table itself. Rigid disk block in a hybrid disk starts at its rdb block lo
    /// sector after master boot record in sector 0. Otherwise 0.
    /// </summary>
    public long TableStart { get; private init; }

    /// <summary>
    /// Layout has changes to apply.
    /// </summary>
    public bool HasChanges => IsInitialize || _deletedPartitions.Count > 0 ||
                              _partitions.Any(x => x.IsNew || x.FormatRequested || x.HasRdbChanges) ||
                              HasFileSystemChanges;

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
        var keepMasterBootRecord = keptMasterBootRecordPartitions != null;
        var layout = CreateRdbLayout(RigidDiskBlock.Create(size / 512 * 512), diskSize, true,
            keepMasterBootRecord, rdbBlockLo, rdbSize, keepMasterBootRecord ? rdbBlockLo * 512L : 0);
        layout._reservedAreas.AddRange(keptMasterBootRecordPartitions ?? []);
        return layout;
    }

    /// <summary>
    /// Reserve area used by rigid disk block initialized at sector rdb block lo keeping master boot record for a hybrid
    /// disk, so master boot record partitions are added after rigid disk block. Replaces rigid disk block area reserved
    /// before.
    /// </summary>
    public void ReserveRigidDiskBlock(long rdbSize, int rdbBlockLo)
    {
        var rdbStart = rdbBlockLo * 512L;
        _reservedAreas.RemoveAll(x => x.TableType == PartitionTableType.RigidDiskBlock);
        _reservedAreas.Add(new ReservedArea(rdbStart, AlignUp(rdbSize + RdbMbrGap, MiB) - rdbStart,
            "Rigid Disk Block", PartitionTableType.RigidDiskBlock));
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
        bool keepMasterBootRecord = false, int rdbBlockLo = 0, long rdbSize = 0, long tableStart = 0)
    {
        var cylinderSize = (long)rigidDiskBlock.Heads * rigidDiskBlock.Sectors * rigidDiskBlock.BlockSize;
        return new PartitionLayout(PartitionTableType.RigidDiskBlock, diskSize,
            rigidDiskBlock.LoCylinder * cylinderSize, ((long)rigidDiskBlock.HiCylinder + 1) * cylinderSize,
            cylinderSize, 128, isInitialize)
        {
            KeepMasterBootRecord = keepMasterBootRecord,
            RdbBlockLo = rdbBlockLo,
            RdbSize = rdbSize,
            TableStart = tableStart
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
        {
            var rdbStart = Math.Min(diskInfo.RigidDiskBlock!.RdbBlockLo * 512L, rdbSize);
            layout._reservedAreas.Add(new ReservedArea(rdbStart, rdbSize - rdbStart, "Rigid Disk Block",
                PartitionTableType.RigidDiskBlock));
        }
        foreach (var part in (partitionTablePart.Parts ?? []).Where(x => x.PartType == PartType.Partition))
        {
            var existingBiosType = int.TryParse(part.BiosType, out var biosType) ? biosType : (int?)null;
            var entry = new PartitionEntryViewModel(tableType, false)
            {
                Number = part.PartitionNumber,
                ExistingFileSystem = FormatExistingFileSystem(part),
                UsedSize = GetUsedSize(part),
                ExistingBiosType = existingBiosType,
                ExistingPartitionType = tableType == PartitionTableType.MasterBootRecord
                    ? existingBiosType.HasValue ? $"0x{existingBiosType.Value:x2}" : string.Empty
                    : PartitionTypes.Normalize(tableType, part.GuidType ?? string.Empty)
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
        // rigid disk block in hybrid disk starts after master boot record in sector 0
        var layout = CreateRdbLayout(rigidDiskBlock, diskSize, false,
            tableStart: diskInfo.MbrPartitionTablePart != null ? rigidDiskBlock.RdbBlockLo * 512L : 0);
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
            var dosType = RdbFileSystemEntry.NormalizeDosType(partitionBlock?.DosTypeFormatted ?? string.Empty);
            var entry = new PartitionEntryViewModel(PartitionTableType.RigidDiskBlock, false)
            {
                Number = part.PartitionNumber,
                ExistingFileSystem = part.FileSystem ?? part.PartitionType ?? string.Empty,
                ExistingDosType = dosType,
                ExistingPartitionType = dosType,
                ExistingDeviceName = partitionBlock?.DriveName ?? string.Empty,
                ExistingBootable = partitionBlock?.Bootable ?? false,
                ExistingRdbProperties = partitionBlock == null ? null : GetRdbProperties(partitionBlock),
                UsedSize = GetUsedSize(part)
            };
            entry.SetRange(part.StartOffset, part.EndOffset - part.StartOffset + 1);
            entry.DeviceName = entry.ExistingDeviceName;
            entry.Bootable = entry.ExistingBootable;
            if (entry.ExistingRdbProperties != null)
                entry.SetRdbProperties(entry.ExistingRdbProperties);
            entry.Label = entry.DeviceName.StartsWith("DH0", StringComparison.OrdinalIgnoreCase) ? "Workbench" : "Work";
            layout.AddEntry(entry);
        }

        return layout;
    }

    private static RdbPartitionProperties GetRdbProperties(PartitionBlock partitionBlock) => new(
        partitionBlock.NumBuffer, partitionBlock.MaxTransfer, partitionBlock.Mask, partitionBlock.BootPriority,
        partitionBlock.NoMount, partitionBlock.Reserved, partitionBlock.PreAlloc,
        (int)partitionBlock.FileSystemBlockSize);

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

        // area used by partition table before usable area. master boot record in a hybrid disk only uses sectors before
        // rigid disk block, which shows its own area. existing partitions can start before usable area
        var tableEnd = _partitions.Select(x => x.Start)
            .Concat(_reservedAreas.Select(x => x.Start).Where(x => x < UsableStart))
            .Append(UsableStart)
            .Min();
        if (tableEnd > TableStart)
            segments.Add(new PartitionSegmentViewModel(this, TableStart, tableEnd - TableStart, null, null, null,
                offset, depth, true));

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
            entry.PartitionTypeOptions = GetDosTypeOptions();
            var isFirst = _partitions.Count == 0;
            entry.FileSystem = PartitionFileSystems.RdbOptions[0].Value;
            entry.DeviceName = GetNextDeviceName();
            entry.Label = isFirst ? "Workbench" : GetNextWorkLabel();
            entry.Bootable = isFirst;
        }
        else
        {
            entry.FileSystem = PartitionFileSystems.BasicOptions[0].Value;
            entry.PartitionType = PartitionTypes.GetDefault(TableType, entry.FileSystem);
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
    /// Add new partition with partition type, file system, size and flags of a partition in first unallocated space
    /// with room for it, preferring unallocated space after partition. Only layout details are cloned, not data, so
    /// new partition is added and formatted like other new partitions.
    /// </summary>
    public PartitionEntryViewModel? ClonePartition(PartitionEntryViewModel source)
    {
        if (!CanClonePartition(source) || FindCloneStart(source) is not { } start)
            return null;

        var partitionType = source.IsNew ? source.PartitionType : source.ExistingPartitionType;
        if (string.IsNullOrWhiteSpace(partitionType))
            partitionType = PartitionTypes.GetDefault(TableType, source.FileSystem);

        var entry = new PartitionEntryViewModel(TableType, true);
        if (IsRdb)
        {
            entry.PartitionTypeOptions = GetDosTypeOptions();
            entry.FileSystem = partitionType.ToLowerInvariant();
            entry.DeviceName = GetNextDeviceName();
            entry.Label = GetNextWorkLabel();
            entry.Bootable = source.Bootable;
            if (source.RdbProperties is { } properties)
                entry.SetRdbProperties(properties);
        }
        else
        {
            entry.FileSystem = source.FileSystem;
            entry.PartitionType = partitionType;

            // file system of existing partition is guessed, so partition types without a supported file system, e.g.
            // a linux partition, aren't formatted
            var option = entry.PartitionTypeOptions.FirstOrDefault(x => !x.IsCustom && string.Equals(x.Value,
                partitionType, StringComparison.OrdinalIgnoreCase));
            if (source.IsExisting && (option == null || !option.FileSystems.Contains(entry.FileSystem)))
                entry.FileSystem = option?.FileSystems.FirstOrDefault() ?? PartitionFileSystems.None;
            entry.Label = string.IsNullOrWhiteSpace(source.Label) ? "Empty" : source.Label;
        }

        entry.SetRange(start, source.Size);
        AddEntry(entry);
        OnChanged();
        return entry;
    }

    /// <summary>
    /// Partition can be cloned, if unallocated space has room for a partition of same size.
    /// </summary>
    public bool CanClonePartition(PartitionEntryViewModel? source) =>
        CanAddPartition && source != null && _partitions.Contains(source) && FindCloneStart(source) != null;

    private long? FindCloneStart(PartitionEntryViewModel source) => BuildSegments()
        .Where(x => x.IsUnallocated)
        .OrderBy(x => x.Start >= source.End ? 0 : 1)
        .Select(x => GetAddRange(x.Start, x.End, AddPartitionPlacement.All))
        .FirstOrDefault(x => x is { } range && range.End - range.Start >= source.Size)?.Start;

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
        UpdatePartitions = _partitions.Select(x => x.GetRdbUpdate()).OfType<PlannedPartitionUpdate>().ToList(),
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
            CloneNumber = x.IsClone ? x.CloneNumber : null,
            Version = x.RequiresManualVersion ? (int?)x.ManualVersion : x.IsClone ? ParseVersion(x.Version).Version : null,
            Revision = x.RequiresManualVersion ? (int?)x.ManualRevision : x.IsClone ? ParseVersion(x.Version).Revision : null
        }).ToList()
    };

    /// <summary>
    /// Parse version and revision of file system, e.g. 19.2. Version of cloned file system is used, if exported file
    /// system data doesn't have a version string.
    /// </summary>
    private static (int? Version, int? Revision) ParseVersion(string version)
    {
        var parts = version.Split('.');
        return parts.Length == 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor)
            ? (major, minor)
            : (null, null);
    }

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
    /// Get file system in rigid disk block with dos type, e.g. PDS3. Returns null, if rigid disk block doesn't have a
    /// file system with dos type.
    /// </summary>
    public RdbFileSystemEntry? FindFileSystem(string dosType) => _fileSystems.FirstOrDefault(x =>
        string.Equals(x.DosType, dosType, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Add new file system to rigid disk block, e.g. imported for a new partition in partition dialog.
    /// </summary>
    public void AddFileSystem(RdbFileSystemEntry fileSystem)
    {
        _fileSystems.Add(fileSystem);
        OnChanged();
    }

    /// <summary>
    /// Remove new file system from rigid disk block, which hasn't been added yet.
    /// </summary>
    public void RemoveFileSystem(RdbFileSystemEntry fileSystem)
    {
        if (fileSystem.IsNew && _fileSystems.Remove(fileSystem))
            OnChanged();
    }

    /// <summary>
    /// Dos types for new rigid disk block partitions, which are common dos types and dos types of file systems in
    /// rigid disk block. Dos types with a file system in rigid disk block show its name.
    /// </summary>
    public List<PartitionTypeOption> GetDosTypeOptions()
    {
        var common = PartitionTypes.RdbOptions.Where(x => !x.IsCustom).Select(x =>
        {
            var fileSystem = FindFileSystem(x.Value);
            return fileSystem == null
                ? x
                : new PartitionTypeOption { Title = $"{x.Title}, {FormatFileSystemName(fileSystem)}", Value = x.Value };
        });
        var other = _fileSystems
            .Where(x => x.DosType.Length == 4 &&
                        PartitionTypes.RdbOptions.All(o => !string.Equals(o.Value, x.DosType,
                            StringComparison.OrdinalIgnoreCase)))
            .GroupBy(x => x.DosType.ToUpperInvariant())
            .Select(x => new PartitionTypeOption
            {
                Title = $"{PartitionFileSystems.FormatDosType(x.Key)}, {FormatFileSystemName(x.First())}",
                Value = x.Key
            });
        return common.Concat(other).Append(PartitionTypes.RdbOptions.Last()).ToList();
    }

    /// <summary>
    /// Format name of file system with version and whether it's new, e.g. pfs3aio 19.2 in Rigid Disk Block.
    /// </summary>
    public static string FormatFileSystemName(RdbFileSystemEntry fileSystem)
    {
        var name = string.IsNullOrWhiteSpace(fileSystem.Name) ? "file system" : fileSystem.Name;
        var version = string.IsNullOrWhiteSpace(fileSystem.Version) ? string.Empty : $" {fileSystem.Version}";
        return fileSystem.IsExisting
            ? $"{name}{version} in Rigid Disk Block"
            : $"{name}{version} (new)";
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

            if (fileSystem.IsNew && !fileSystem.IsClone && string.IsNullOrWhiteSpace(fileSystem.Path))
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
                $"DOS type '{dosType}' is used by more than one file system. Change DOS type of a file system or delete the existing file system to replace it");

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
        IsPiStorm = partition.IsPiStorm,
        PartitionType = partition.IsNew && !partition.IsRdb ? partition.PartitionType : string.Empty,
        Format = partition.IsNew ? partition.IsFormattable : partition.FormatRequested,
        RdbProperties = partition.RdbProperties ?? RdbPartitionProperties.Default
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
