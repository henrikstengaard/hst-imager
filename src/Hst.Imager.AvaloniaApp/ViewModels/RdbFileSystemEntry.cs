using System;
using System.Linq;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Amiga.VersionStrings;
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
    Media
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
    private string _name = string.Empty;
    private string _path = string.Empty;
    private string _version = string.Empty;
    private long? _size;
    private bool _hasVersionString = true;
    private decimal? _manualVersion;
    private decimal? _manualRevision;
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

    /// <summary>
    /// File system number of existing file system in rigid disk block.
    /// </summary>
    public int? Number { get; init; }

    /// <summary>
    /// Dos type of existing file system read from disk, e.g. PFS3.
    /// </summary>
    public string OriginalDosType { get; init; } = string.Empty;

    /// <summary>
    /// Name of existing file system read from disk.
    /// </summary>
    public string OriginalName { get; init; } = string.Empty;

    /// <summary>
    /// Dos type of file system without backslash, e.g. PFS3 or DOS3.
    /// </summary>
    public string DosType
    {
        get => _dosType;
        set
        {
            this.RaiseAndSetIfChanged(ref _dosType, NormalizeDosType(value));
            RaiseStatusChanged();
        }
    }

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
            this.RaisePropertyChanged(nameof(SourceText));
            RaiseStatusChanged();
        }
    }

    /// <summary>
    /// Version of file system, e.g. 19.2.
    /// </summary>
    public string Version
    {
        get => _version;
        set => this.RaiseAndSetIfChanged(ref _version, value);
    }

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
    /// File system file has a version string. Version and revision must be set manually for file system added from a
    /// file without a version string.
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

    public decimal? ManualVersion
    {
        get => _manualVersion;
        set => this.RaiseAndSetIfChanged(ref _manualVersion, value);
    }

    public decimal? ManualRevision
    {
        get => _manualRevision;
        set => this.RaiseAndSetIfChanged(ref _manualRevision, value);
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
    public bool IsUpdated => IsDosTypeChanged || IsNameChanged || IsDataReplaced;

    public string NumberText => Number?.ToString() ?? string.Empty;

    public string StatusText => Source switch
    {
        RdbFileSystemSource.File => "Add",
        RdbFileSystemSource.Media => "Import",
        _ => IsUpdated ? "Update" : string.Empty
    };

    /// <summary>
    /// File or media file system is added or imported from or file replacing data of existing file system.
    /// </summary>
    public string SourceText => string.IsNullOrWhiteSpace(_path)
        ? string.Empty
        : IsUrl(_path)
            ? _path
            : System.IO.Path.GetFileName(_path);

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
            DosType = dosType,
            Name = name,
            Version = headerBlock.VersionFormatted ?? string.Empty,
            Size = headerBlock.LoadSegBlocks?.Sum(x => (long)(x.Data?.Length ?? 0)) ?? 0
        };
    }

    public RdbFileSystemEntry Clone() => new(Source)
    {
        Number = Number,
        OriginalDosType = OriginalDosType,
        OriginalName = OriginalName,
        DosType = _dosType,
        Name = _name,
        Path = _path,
        Version = _version,
        Size = _size,
        HasVersionString = _hasVersionString,
        ManualVersion = _manualVersion,
        ManualRevision = _manualRevision,
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

    public static bool IsUrl(string path) => path.StartsWith("http", StringComparison.OrdinalIgnoreCase);

    private void RaiseStatusChanged()
    {
        this.RaisePropertyChanged(nameof(IsUpdated));
        this.RaisePropertyChanged(nameof(StatusText));
    }
}
