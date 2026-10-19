using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Hst.Amiga.VersionStrings;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;
using ReactiveUI.Reactive;
using Unit = System.Reactive.Unit;

namespace Hst.Imager.AvaloniaApp.ViewModels;

/// <summary>
/// File systems dialog to add, import, clone, update, export and delete file systems in a rigid disk block. File systems
/// are edited as copies, which are set as pending operations of rigid disk block layout, when OK is clicked.
/// Exporting a file system reads it from disk, so only existing file systems can be exported.
/// </summary>
public class RdbFileSystemsViewModel : ViewModelBase
{
    private static readonly FileFilterItem[] FileSystemFileFilters =
    [
        new() { Name = "All files", Extensions = ["*"] }
    ];

    private static readonly FileFilterItem[] MediaFileFilters =
    [
        new() { Name = "Media files", Extensions = ["lha", "adf", "iso", "hdf", "img"] },
        new() { Name = "All files", Extensions = ["*"] }
    ];

    private readonly PartitionLayout _layout;
    private readonly IDialogService _dialogService;
    private readonly IImagingService _imagingService;
    private readonly Func<int, string, Task>? _export;
    private readonly List<RdbFileSystemEntry> _deletedFileSystems;
    private RdbFileSystemEntry? _selectedFileSystem;
    private IReadOnlyList<string> _validationErrors = [];
    private string _message = string.Empty;
    private bool _isMessageError;

    /// <param name="title">Title of dialog, e.g. File systems in Rigid Disk Block.</param>
    /// <param name="layout">Layout of rigid disk block with file systems to edit.</param>
    /// <param name="dialogService">Dialog service to select files.</param>
    /// <param name="imagingService">Imaging service to find file systems in media.</param>
    /// <param name="export">Export existing file system with number to path. Null, if rigid disk block doesn't exist
    /// on disk yet.</param>
    public RdbFileSystemsViewModel(string title, PartitionLayout layout, IDialogService dialogService,
        IImagingService imagingService, Func<int, string, Task>? export)
    {
        Title = title;
        _layout = layout;
        _dialogService = dialogService;
        _imagingService = imagingService;
        _export = export;
        _deletedFileSystems = layout.DeletedFileSystems.Select(x => x.Clone()).ToList();
        FileSystems = new ObservableCollection<RdbFileSystemEntry>(layout.FileSystems.Select(x => x.Clone()));
        foreach (var fileSystem in FileSystems)
            fileSystem.PropertyChanged += OnFileSystemPropertyChanged;
        _selectedFileSystem = FileSystems.FirstOrDefault();

        var isSelected = this.WhenAnyValue(x => x.SelectedFileSystem).Select(x => x != null);
        AddFromFileCommand = ReactiveCommand.CreateFromTask(AddFromFileAsync);
        ImportFromMediaCommand = ReactiveCommand.CreateFromTask(ImportFromMediaAsync);
        CloneCommand = ReactiveCommand.Create(Clone, isSelected);
        ExportCommand = ReactiveCommand.CreateFromTask(ExportAsync, this.WhenAnyValue(x => x.CanExport));
        DeleteCommand = ReactiveCommand.Create(Delete, isSelected);
        BrowsePathCommand = ReactiveCommand.CreateFromTask(BrowsePathAsync, isSelected);
        Validate();
    }

    public string Title { get; }

    public ObservableCollection<RdbFileSystemEntry> FileSystems { get; }

    public RdbFileSystemEntry? SelectedFileSystem
    {
        get => _selectedFileSystem;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedFileSystem, value);
            this.RaisePropertyChanged(nameof(IsFileSystemSelected));
            this.RaisePropertyChanged(nameof(CanExport));
            this.RaisePropertyChanged(nameof(ShowPath));
            this.RaisePropertyChanged(nameof(PathLabel));
            this.RaisePropertyChanged(nameof(NameLabel));
            this.RaisePropertyChanged(nameof(PathHelp));
        }
    }

    public bool IsFileSystemSelected => _selectedFileSystem != null;
    public bool HasFileSystems => FileSystems.Count > 0;

    /// <summary>
    /// Existing file systems can be exported, when rigid disk block exists on disk.
    /// </summary>
    public bool CanExport => _export != null && _selectedFileSystem is { IsExisting: true };

    public string ExportHint => _export == null
        ? "File systems can be exported, when Rigid Disk Block is written to disk"
        : "Export selected file system to a file";

    public string NameLabel => _selectedFileSystem is { IsFromMedia: true }
        ? "Name of file system to find in media"
        : "Name";

    /// <summary>
    /// Path is shown for file systems added from a file, imported from media or existing file systems, which data can
    /// be replaced. Cloned file systems use data of file system cloned.
    /// </summary>
    public bool ShowPath => _selectedFileSystem is { IsClone: false };

    public string PathLabel => _selectedFileSystem?.Source switch
    {
        RdbFileSystemSource.File => "File system file",
        RdbFileSystemSource.Media => "Media (lha, adf, iso, hdf) or url",
        _ => "Replace with file system file (optional)"
    };

    public string PathHelp => _selectedFileSystem?.Source switch
    {
        RdbFileSystemSource.File =>
            "File system is added to Rigid Disk Block and replaces an existing file system with same DOS type.",
        RdbFileSystemSource.Media =>
            "File system with highest version matching name is imported from media and replaces an existing file system with same DOS type.",
        RdbFileSystemSource.Clone =>
            "File system is cloned from existing file system before file systems are updated or deleted. Change DOS type, as it must be unique.",
        _ =>
            "Changing DOS type also changes DOS type of partitions using the file system. Leave file empty to keep file system data."
    };

    public IReadOnlyList<string> ValidationErrors
    {
        get => _validationErrors;
        private set
        {
            this.RaiseAndSetIfChanged(ref _validationErrors, value);
            this.RaisePropertyChanged(nameof(HasValidationErrors));
        }
    }

    public bool HasValidationErrors => _validationErrors.Count > 0;

    /// <summary>
    /// Message shown after exporting a file system.
    /// </summary>
    public string Message
    {
        get => _message;
        private set
        {
            this.RaiseAndSetIfChanged(ref _message, value);
            this.RaisePropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => !string.IsNullOrEmpty(_message);

    public bool IsMessageError
    {
        get => _isMessageError;
        private set => this.RaiseAndSetIfChanged(ref _isMessageError, value);
    }

    public ReactiveCommand<Unit, Unit> AddFromFileCommand { get; }
    public ReactiveCommand<Unit, Unit> ImportFromMediaCommand { get; }

    /// <summary>
    /// Clone selected file system to a new file system with same data, which needs another dos type.
    /// </summary>
    public ReactiveCommand<Unit, Unit> CloneCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowsePathCommand { get; }

    /// <summary>
    /// Set edited file systems as pending operations of rigid disk block layout.
    /// </summary>
    public void Apply() => _layout.SetFileSystems(FileSystems, _deletedFileSystems);

    private async Task AddFromFileAsync()
    {
        var path = await _dialogService.ShowOpenFileDialogAsync("Select file system file", FileSystemFileFilters);
        if (path == null)
            return;

        var fileName = System.IO.Path.GetFileName(path);
        var fileSystem = new RdbFileSystemEntry(RdbFileSystemSource.File)
        {
            DosType = RdbFileSystemEntry.GuessDosType(fileName),
            Name = fileName
        };
        AddFileSystem(fileSystem);
        fileSystem.Path = path;
    }

    private async Task ImportFromMediaAsync()
    {
        var path = await _dialogService.ShowOpenFileDialogAsync("Select media with file system", MediaFileFilters);
        if (path == null)
            return;

        // pfs3aio is imported by default from media named pfs3, otherwise FastFileSystem
        var isPfs3 = System.IO.Path.GetFileName(path).Contains("pfs3", StringComparison.OrdinalIgnoreCase);
        var fileSystem = new RdbFileSystemEntry(RdbFileSystemSource.Media)
        {
            DosType = isPfs3 ? "PDS3" : "DOS3",
            Name = isPfs3 ? "pfs3aio" : "FastFileSystem"
        };
        AddFileSystem(fileSystem);
        fileSystem.Path = path;
    }

    private void AddFileSystem(RdbFileSystemEntry fileSystem)
    {
        fileSystem.PropertyChanged += OnFileSystemPropertyChanged;
        FileSystems.Add(fileSystem);
        this.RaisePropertyChanged(nameof(HasFileSystems));
        SelectedFileSystem = fileSystem;
        Validate();
    }

    private void Clone()
    {
        if (_selectedFileSystem is { } fileSystem)
            AddFileSystem(fileSystem.CreateClone());
    }

    private void Delete()
    {
        if (_selectedFileSystem is not { } fileSystem)
            return;

        var index = FileSystems.IndexOf(fileSystem);
        fileSystem.PropertyChanged -= OnFileSystemPropertyChanged;
        FileSystems.Remove(fileSystem);
        if (fileSystem.IsExisting)
            _deletedFileSystems.Add(fileSystem);
        this.RaisePropertyChanged(nameof(HasFileSystems));
        SelectedFileSystem = FileSystems.ElementAtOrDefault(Math.Min(index, FileSystems.Count - 1));
        Validate();
    }

    private async Task ExportAsync()
    {
        if (_export == null || _selectedFileSystem is not { IsExisting: true, Number: { } number } fileSystem)
            return;

        var defaultFileName = System.IO.Path.GetFileName(fileSystem.OriginalName.Replace(':', '/'));
        var path = await _dialogService.ShowSaveFileDialogAsync("Export file system", FileSystemFileFilters,
            string.IsNullOrWhiteSpace(defaultFileName) ? fileSystem.OriginalDosType : defaultFileName);
        if (path == null)
            return;

        try
        {
            await _export(number, path);
            IsMessageError = false;
            Message = $"Exported file system #{number} ({fileSystem.OriginalDosType}) to '{path}'";
        }
        catch (Exception ex)
        {
            IsMessageError = true;
            Message = $"Failed to export file system #{number}: {ex.Message}";
        }
    }

    private async Task BrowsePathAsync()
    {
        if (_selectedFileSystem is not { } fileSystem)
            return;

        var path = fileSystem.IsFromMedia
            ? await _dialogService.ShowOpenFileDialogAsync("Select media with file system", MediaFileFilters)
            : await _dialogService.ShowOpenFileDialogAsync("Select file system file", FileSystemFileFilters);
        if (path != null)
            fileSystem.Path = path;
    }

    private void OnFileSystemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not RdbFileSystemEntry fileSystem)
            return;

        if (e.PropertyName == nameof(RdbFileSystemEntry.Path) ||
            (e.PropertyName == nameof(RdbFileSystemEntry.Name) && fileSystem.IsFromMedia))
            _ = ReadSourceAsync(fileSystem);

        if (e.PropertyName is nameof(RdbFileSystemEntry.DosType) or nameof(RdbFileSystemEntry.Name)
            or nameof(RdbFileSystemEntry.Path) or nameof(RdbFileSystemEntry.Size)
            or nameof(RdbFileSystemEntry.SourceError) or nameof(RdbFileSystemEntry.HasVersionString)
            or nameof(RdbFileSystemEntry.VersionNumber) or nameof(RdbFileSystemEntry.RevisionNumber))
            Validate();
    }

    /// <summary>
    /// Read version and size of file system from file system file or by finding it in media, so errors are shown
    /// before changes are applied. File system data of existing file systems isn't shown for replacement files, as
    /// version isn't updated when replacing data.
    /// </summary>
    private async Task ReadSourceAsync(RdbFileSystemEntry fileSystem)
    {
        var path = fileSystem.Path;
        var name = fileSystem.Name;
        string error;
        try
        {
            error = fileSystem.Source switch
            {
                RdbFileSystemSource.File => await ReadFileSystemFileAsync(fileSystem, path),
                RdbFileSystemSource.Media => await FindFileSystemInMediaAsync(fileSystem, path, name),
                _ => string.IsNullOrWhiteSpace(path) || File.Exists(path) ? string.Empty : $"File '{path}' not found"
            };
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        // ignore result, if path or name was changed while reading
        if (fileSystem.Path == path && fileSystem.Name == name)
            fileSystem.SourceError = error;
    }

    private static async Task<string> ReadFileSystemFileAsync(RdbFileSystemEntry fileSystem, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        if (!File.Exists(path))
            return $"File '{path}' not found";

        var size = new FileInfo(path).Length;
        var data = size <= RdbFileSystemEntry.MaxFileSystemSize ? await File.ReadAllBytesAsync(path) : [];
        if (fileSystem.Path != path)
            return string.Empty;

        var version = RdbFileSystemEntry.FormatVersion(VersionStringReader.Read(data));
        fileSystem.Size = size;
        fileSystem.SetVersion(version);
        fileSystem.UpdateFastFileSystemDosType();
        fileSystem.HasVersionString = data.Length == 0 || !string.IsNullOrEmpty(version);
        return string.Empty;
    }

    private async Task<string> FindFileSystemInMediaAsync(RdbFileSystemEntry fileSystem, string path, string name)
    {
        // media from urls is downloaded, when file system is imported
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(name) || RdbFileSystemEntry.IsUrl(path))
        {
            fileSystem.Size = null;
            fileSystem.SetVersion(string.Empty);
            return string.Empty;
        }

        if (!File.Exists(path))
            return $"Media '{path}' not found";

        var info = await Task.Run(() => _imagingService.FindRdbFileSystemAsync(path, name));
        if (fileSystem.Path != path || fileSystem.Name != name)
            return string.Empty;

        fileSystem.Size = info?.Size;
        fileSystem.SetVersion(info?.Version ?? string.Empty);
        fileSystem.UpdateFastFileSystemDosType();
        return info == null ? $"File system '{name}' not found in media '{System.IO.Path.GetFileName(path)}'" : string.Empty;
    }

    private void Validate() => ValidationErrors = _layout.ValidateFileSystems(FileSystems, _deletedFileSystems);
}
