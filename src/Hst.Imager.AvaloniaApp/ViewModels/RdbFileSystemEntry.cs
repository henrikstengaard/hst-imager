using System;
using System.Linq;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Amiga.VersionStrings;
using Hst.Imager.Core.Commands;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

/// <summary>
/// Source of file system in rigid disk block.
/// </summary>
public enum RdbFileSystemSource
{
    /// <summary>
    /// Existing file system read from rigid disk block.
    /// </summary>
    Existing,

    /// <summary>
    /// New file system added from a file system file (rdb fs add).
    /// </summary>
    File,

    /// <summary>
    /// New file system imported from media like lha, adf or iso (rdb fs import).
    /// </summary>
    Media,

    /// <summary>
    /// New file system cloned from existing file system in rigid disk block, which is exported and added as a new
    /// file system (rdb fs export and rdb fs add).
    /// </summary>
    Clone
}

/// <summary>
/// File system in rigid disk block, either an existing file system read from disk, which can be updated or deleted,
/// or a new file system added from a file or imported from media.
/// </summary>
public class RdbFileSystemEntry : ReactiveObject
{
    /// <summary>
    /// Max size of file system added from a file, same as rdb fs add.
    /// </summary>
    public const long MaxFileSystemSize = 512 * 1024;

    private string _dosType = string.Empty;
    private string _dosTypeText = string.Empty;
    private string _name = string.Empty;
    private string _path = string.Empty;
    private long? _size;
    private bool _hasVersionString = true;
    private decimal? _versionNumber;
    private decimal? _revisionNumber;
    private string _sourceError = string.Empty;

    public RdbFileSystemEntry(RdbFileSystemSource source)
    {
        Source = source;
    }

    public RdbFileSystemSource Source { get; }
    public bool IsExisting => Source == RdbFileSystemSource.Existing;
    public bool IsNew => !IsExisting;
    public bool IsFromFile => Source == RdbFileSystemSource.File;
    public bool IsFromMedia => Source == RdbFileSystemSource.Media;
    public bool IsClone => Source == RdbFileSystemSource.Clone;

    /// <summary>
    /// File system number of existing file system in rigid disk block.
    /// </summary>
    public int? Number { get; init; }

    /// <summary>
    /// Number of existing file system in rigid disk block, which data of cloned file system is copied from.
    /// </summary>
    public int? CloneNumber { get; init; }

    /// <summary>
    /// Dos type of existing file system read from disk, e.g. PFS3.
    /// </summary>
    public string OriginalDosType { get; init; } = string.Empty;

    /// <summary>
    /// Name of existing file system read from disk.
    /// </summary>
    public string OriginalName { get; init; } = string.Empty;

    /// <summary>
    /// Version and revision of existing file system read from disk.
    /// </summary>
    public int? OriginalVersionNumber { get; init; }
    public int? OriginalRevisionNumber { get; init; }

    /// <summary>
    /// Dos type of file system without backslash, e.g. PFS3 or DOS3.
    /// </summary>
    public string DosType
    {
        get => _dosType;
        set
        {
            _dosTypeText = value;
            SetDosType(value);
        }
    }

    /// <summary>
    /// Dos type entered in file systems dialog. Text is kept as entered, so it isn't changed while typing, and dos
    /// type is set to normalized value.
    /// </summary>
    public string DosTypeText
    {
        get => _dosTypeText;
        set
        {
            this.RaiseAndSetIfChanged(ref _dosTypeText, value);
            SetDosType(value);
        }
    }

    /// <summary>
    /// Error for invalid dos type entered or null, if it's valid.
    /// </summary>
    public string? DosTypeError => PartitionTypes.Validate(PartitionTableType.RigidDiskBlock, _dosTypeText);

    public bool HasDosTypeError => DosTypeError != null;

    /// <summary>
    /// Name of file system. Name of file system imported from media is also used to find file system in media.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            this.RaiseAndSetIfChanged(ref _name, value);
            RaiseStatusChanged();
        }
    }

    /// <summary>
    /// Path to file system file for new file system added from file or to replace data of existing file system with
    /// (empty keeps data) or path or url to media for file system imported from media.
    /// </summary>
    public string Path
    {
        get => _path;
        set
        {
            this.RaiseAndSetIfChanged(ref _path, value);
            RaiseStatusChanged();
        }
    }

    /// <summary>
    /// Version of file system, e.g. 19.2. Empty, if version or revision isn't set.
    /// </summary>
    public string Version => _versionNumber.HasValue && _revisionNumber.HasValue
        ? $"{_versionNumber:0}.{_revisionNumber:0}"
        : string.Empty;

    public long? Size
    {
        get => _size;
        set
        {
            this.RaiseAndSetIfChanged(ref _size, value);
            this.RaisePropertyChanged(nameof(SizeText));
        }
    }

    public string SizeText => _size.HasValue ? MediaOptions.FormatBytes(_size.Value) : string.Empty;

    /// <summary>
    /// File system file has a version string. Version and revision must be set for file system added from a file
    /// without a version string.
    /// </summary>
    public bool HasVersionString
    {
        get => _hasVersionString;
        set
        {
            this.RaiseAndSetIfChanged(ref _hasVersionString, value);
            this.RaisePropertyChanged(nameof(RequiresManualVersion));
        }
    }

    public bool RequiresManualVersion => IsFromFile && !_hasVersionString;

    /// <summary>
    /// Version of file system (number before . in version). Read from disk for existing file systems and from file
    /// system file or media for new file systems, which can be changed.
    /// </summary>
    public decimal? VersionNumber
    {
        get => _versionNumber;
        set
        {
            this.RaiseAndSetIfChanged(ref _versionNumber, value);
            RaiseVersionChanged();
        }
    }

    /// <summary>
    /// Revision of file system (number after . in version).
    /// </summary>
    public decimal? RevisionNumber
    {
        get => _revisionNumber;
        set
        {
            this.RaiseAndSetIfChanged(ref _revisionNumber, value);
            RaiseVersionChanged();
        }
    }

    /// <summary>
    /// Error reading file system file or finding file system in media, e.g. file system not found in media.
    /// </summary>
    public string SourceError
    {
        get => _sourceError;
        set
        {
            this.RaiseAndSetIfChanged(ref _sourceError, value);
            this.RaisePropertyChanged(nameof(HasSourceError));
        }
    }

    public bool HasSourceError => !string.IsNullOrEmpty(_sourceError);

    /// <summary>
    /// Existing file system has changes to dos type, name or data.
    /// </summary>
    public bool IsDosTypeChanged => IsExisting && !string.Equals(_dosType, OriginalDosType, StringComparison.OrdinalIgnoreCase);
    public bool IsNameChanged => IsExisting && _name != OriginalName;
    public bool IsDataReplaced => IsExisting && !string.IsNullOrWhiteSpace(_path);
    public bool IsVersionChanged => IsExisting &&
                                    (_versionNumber != OriginalVersionNumber || _revisionNumber != OriginalRevisionNumber);
    public bool IsUpdated => IsDosTypeChanged || IsNameChanged || IsDataReplaced || IsVersionChanged;

    public string NumberText => Number?.ToString() ?? string.Empty;

    /// <summary>
    /// Create file system entry for existing file system in rigid disk block.
    /// </summary>
    public static RdbFileSystemEntry FromHeaderBlock(FileSystemHeaderBlock headerBlock, int number)
    {
        var dosType = NormalizeDosType(headerBlock.DosTypeFormatted ?? string.Empty);
        var name = headerBlock.FileSystemName ?? string.Empty;
        return new RdbFileSystemEntry(RdbFileSystemSource.Existing)
        {
            Number = number,
            OriginalDosType = dosType,
            OriginalName = name,
            OriginalVersionNumber = headerBlock.Version,
            OriginalRevisionNumber = headerBlock.Revision,
            DosType = dosType,
            Name = name,
            VersionNumber = headerBlock.Version,
            RevisionNumber = headerBlock.Revision,
            Size = headerBlock.LoadSegBlocks?.Sum(x => (long)(x.Data?.Length ?? 0)) ?? 0
        };
    }

    public RdbFileSystemEntry Clone() => new(Source)
    {
        Number = Number,
        CloneNumber = CloneNumber,
        OriginalDosType = OriginalDosType,
        OriginalName = OriginalName,
        OriginalVersionNumber = OriginalVersionNumber,
        OriginalRevisionNumber = OriginalRevisionNumber,
        DosType = _dosType,
        Name = _name,
        Path = _path,
        Size = _size,
        HasVersionString = _hasVersionString,
        VersionNumber = _versionNumber,
        RevisionNumber = _revisionNumber,
        SourceError = _sourceError
    };

    /// <summary>
    /// Create new file system with data of file system. Existing file systems are cloned from rigid disk block, new
    /// file systems are added from same file or imported from same media.
    /// </summary>
    public RdbFileSystemEntry CreateClone() => new(IsExisting ? RdbFileSystemSource.Clone : Source)
    {
        CloneNumber = IsExisting ? Number : CloneNumber,
        DosType = _dosType,
        Name = _name,
        Path = IsExisting ? string.Empty : _path,
        Size = _size,
        HasVersionString = _hasVersionString,
        VersionNumber = _versionNumber,
        RevisionNumber = _revisionNumber,
        SourceError = _sourceError
    };

    /// <summary>
    /// Normalize dos type to format used by rdb commands, e.g. PFS\3 to PFS3.
    /// </summary>
    public static string NormalizeDosType(string dosType) =>
        dosType.Replace("\\", string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// Guess dos type of file system from its name, e.g. PDS3 for pfs3aio and DOS3 for FastFileSystem.
    /// </summary>
    public static string GuessDosType(string name)
    {
        var normalized = name.ToLowerInvariant();
        if (normalized.Contains("pfs3"))
            return "PDS3";
        if (normalized.Contains("fastfilesystem") || normalized.Contains("ffs"))
            return "DOS3";
        return string.Empty;
    }

    /// <summary>
    /// Format version from version string of file system, e.g. 19.2 for "$VER: pfs3aio 19.2 (...)".
    /// </summary>
    public static string FormatVersion(string? versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return string.Empty;
        var version = VersionStringReader.Parse(versionString);
        return version == null ? string.Empty : $"{version.Version}.{version.Revision}";
    }

    /// <summary>
    /// Set version and revision from version read from file system file or media, e.g. 19.2. Version and revision are
    /// cleared, if version is empty or invalid.
    /// </summary>
    public void SetVersion(string version)
    {
        var parts = version.Split('.');
        if (parts.Length == 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
        {
            VersionNumber = major;
            RevisionNumber = minor;
            return;
        }

        VersionNumber = null;
        RevisionNumber = null;
    }

    public static bool IsUrl(string path) => path.StartsWith("http", StringComparison.OrdinalIgnoreCase);

    private void SetDosType(string value)
    {
        this.RaiseAndSetIfChanged(ref _dosType, NormalizeDosType(value), nameof(DosType));
        this.RaisePropertyChanged(nameof(DosTypeText));
        this.RaisePropertyChanged(nameof(DosTypeError));
        this.RaisePropertyChanged(nameof(HasDosTypeError));
        RaiseStatusChanged();
    }

    private void RaiseVersionChanged()
    {
        this.RaisePropertyChanged(nameof(Version));
        RaiseStatusChanged();
    }

    private void RaiseStatusChanged()
    {
        this.RaisePropertyChanged(nameof(IsUpdated));
    }
}
