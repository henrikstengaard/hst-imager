using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Hst.Amiga.RigidDiskBlocks.Recovery;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Commands.RecoveryCommands;
using Hst.Imager.Core.UaeMetadatas;
using ReactiveUI;
using ReactiveUI.Reactive;
using Unit = System.Reactive.Unit;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class RecoveryOperationOption
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public RecoveryOperation Value { get; init; }
}

public class RecoveryModeOption
{
    public string Title { get; init; } = string.Empty;
    public RecoveryMode Value { get; init; }
}

public class UaeMetadataOption
{
    public string Title { get; init; } = string.Empty;
    public UaeMetadata Value { get; init; }
}

public class RecoveryViewModel : ViewModelBase
{
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    private RecoveryOperationOption _selectedOperation;
    private List<SelectOption> _targetOptions = [];
    private SelectOption? _selectedTarget;
    private RecoveryModeOption _selectedRdbMode;
    private UaeMetadataOption _selectedUaeMetadata;
    private bool _salvageOrphans = true;
    private string _outputPath = string.Empty;
    private string _logPath = string.Empty;
    private bool _logPathEdited;
    private bool _updatingLogPath;

    public RecoveryViewModel(IMediaService mediaService, IImagingService imagingService,
        IDialogService dialogService, INavigationService navigationService, ProgressViewModel progress)
    {
        _imagingService = imagingService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        Progress = progress;
        Source = new MediaSelectionViewModel(mediaService, dialogService, new MediaSelectionOptions
        {
            Title = "Disk",
            AllowImageFile = true,
            AllowPhysicalDisk = true,
            FileFilters =
            [
                new FileFilterItem { Name = "Hard disk and floppy image files", Extensions = ["img", "hdf", "vhd", "adf"] },
                new FileFilterItem { Name = "All files", Extensions = ["*"] }
            ],
            ShowByteswap = true
        });
        SourceLayout = new MediaPartitionLayoutViewModel(mediaService, Source);
        Source.Committed += (_, _) => UpdateDefaultLogPath();

        OperationOptions =
        [
            new RecoveryOperationOption
            {
                Title = "Scan partition table", Value = RecoveryOperation.PartitionTableScan,
                Description = "Scan Rigid Disk Block, Master Boot Record or Guid Partition Table for errors without writing any changes."
            },
            new RecoveryOperationOption
            {
                Title = "Repair partition table", Value = RecoveryOperation.PartitionTableRepair,
                Description = "Repair Rigid Disk Block using file systems found on disk or restore Guid Partition Table from backup."
            },
            new RecoveryOperationOption
            {
                Title = "Scan file system", Value = RecoveryOperation.FileSystemScan,
                Description = "Scan Amiga file system (FFS, PFS3 or SFS) for errors without writing any changes."
            },
            new RecoveryOperationOption
            {
                Title = "Repair file system", Value = RecoveryOperation.FileSystemRepair,
                Description = "Repair errors in Amiga file system (FFS, PFS3 or SFS)."
            },
            new RecoveryOperationOption
            {
                Title = "Salvage file system", Value = RecoveryOperation.FileSystemSalvage,
                Description = "Salvage directories and files from Amiga file system (FFS, PFS3 or SFS) to a directory. The file system is never written to."
            }
        ];
        _selectedOperation = OperationOptions[0];

        RdbModeOptions =
        [
            new RecoveryModeOption { Title = "Auto (fix if found, otherwise rebuild)", Value = RecoveryMode.Auto },
            new RecoveryModeOption { Title = "Fix existing rigid disk block", Value = RecoveryMode.Fix },
            new RecoveryModeOption { Title = "Rebuild from file systems found on disk", Value = RecoveryMode.Rebuild }
        ];
        _selectedRdbMode = RdbModeOptions[0];

        UaeMetadataOptions =
        [
            new UaeMetadataOption { Title = "UAEFSDB (WinUAE, FS-UAE, Amiberry)", Value = UaeMetadata.UaeFsDb },
            new UaeMetadataOption { Title = "UAE metafile (FS-UAE)", Value = UaeMetadata.UaeMetafile }
        ];
        _selectedUaeMetadata = UaeMetadataOptions[0];

        BrowseOutputPathCommand = ReactiveCommand.CreateFromTask(BrowseOutputPathAsync);
        BrowseLogPathCommand = ReactiveCommand.CreateFromTask(BrowseLogPathAsync);
        ResetLogPathCommand = ReactiveCommand.Create(ResetLogPath);
        StartCommand = ReactiveCommand.CreateFromTask(StartAsync,
            this.WhenAnyValue(x => x.SelectedTarget, x => x.LogPath, x => x.OutputPath, x => x.SelectedOperation,
                x => x.Progress.IsRunning,
                (target, log, output, operation, running) => !running && target != null &&
                                                             !string.IsNullOrWhiteSpace(log) &&
                                                             (operation.Value != RecoveryOperation.FileSystemSalvage ||
                                                              !string.IsNullOrWhiteSpace(output))));
        ResetCommand = ReactiveCommand.Create(() => _navigationService.NavigateTo("Recovery"),
            this.WhenAnyValue(x => x.Progress.IsRunning, running => !running));

        // update targets available for operation, when media is loaded
        this.WhenAnyValue(x => x.Source.Media).Subscribe(_ => UpdateTargetOptions());

        _ = Source.InitializeAsync();
    }

    public ProgressViewModel Progress { get; }
    public MediaSelectionViewModel Source { get; }
    public MediaPartitionLayoutViewModel SourceLayout { get; }

    public List<RecoveryOperationOption> OperationOptions { get; }
    public List<RecoveryModeOption> RdbModeOptions { get; }
    public List<UaeMetadataOption> UaeMetadataOptions { get; }

    public RecoveryOperationOption SelectedOperation
    {
        get => _selectedOperation;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedOperation, value);
            this.RaisePropertyChanged(nameof(IsPartitionTableOperation));
            this.RaisePropertyChanged(nameof(IsFileSystemOperation));
            this.RaisePropertyChanged(nameof(IsRepair));
            this.RaisePropertyChanged(nameof(IsPartitionTableRepair));
            this.RaisePropertyChanged(nameof(IsSalvage));
            this.RaisePropertyChanged(nameof(StartTitle));
            this.RaisePropertyChanged(nameof(StartIcon));
            this.RaisePropertyChanged(nameof(TargetHint));
            UpdateTargetOptions();
            UpdateDefaultLogPath();
        }
    }

    public bool IsPartitionTableOperation => _selectedOperation.Value is RecoveryOperation.PartitionTableScan
        or RecoveryOperation.PartitionTableRepair;

    public bool IsFileSystemOperation => !IsPartitionTableOperation;

    public bool IsRepair => _selectedOperation.Value is RecoveryOperation.PartitionTableRepair
        or RecoveryOperation.FileSystemRepair;

    public bool IsPartitionTableRepair => _selectedOperation.Value == RecoveryOperation.PartitionTableRepair;

    public bool IsSalvage => _selectedOperation.Value == RecoveryOperation.FileSystemSalvage;

    public string StartTitle => IsRepair ? "Start repair" : IsSalvage ? "Start salvage" : "Start scan";

    public string StartIcon => IsRepair ? "fa-screwdriver-wrench" : IsSalvage ? "fa-life-ring" : "fa-stethoscope";

    public string TargetHint => IsPartitionTableOperation
        ? "Partition tables present are detected, when disk is selected. Select Rigid Disk Block to scan or repair a Rigid Disk Block, which is damaged and not detected."
        : "Only partitions with Amiga file systems FFS, PFS3 and SFS are listed. If partitions are missing due to a damaged Rigid Disk Block, repair partition table first.";

    /// <summary>
    /// Targets available for selected operation and disk with path as value.
    /// </summary>
    public List<SelectOption> TargetOptions
    {
        get => _targetOptions;
        private set => this.RaiseAndSetIfChanged(ref _targetOptions, value);
    }

    public SelectOption? SelectedTarget
    {
        get => _selectedTarget;
        set => this.RaiseAndSetIfChanged(ref _selectedTarget, value);
    }

    public string TargetPlaceholder => !Source.IsSelected
        ? "Select disk first"
        : IsPartitionTableOperation ? "No partition tables found" : "No partitions with supported file systems found";

    public RecoveryModeOption SelectedRdbMode
    {
        get => _selectedRdbMode;
        set => this.RaiseAndSetIfChanged(ref _selectedRdbMode, value);
    }

    public UaeMetadataOption SelectedUaeMetadata
    {
        get => _selectedUaeMetadata;
        set => this.RaiseAndSetIfChanged(ref _selectedUaeMetadata, value);
    }

    public bool SalvageOrphans
    {
        get => _salvageOrphans;
        set => this.RaiseAndSetIfChanged(ref _salvageOrphans, value);
    }

    /// <summary>
    /// Update targets available for selected operation from disk info of selected disk. Selects previously
    /// selected target, if available. Otherwise first target is selected.
    /// </summary>
    private void UpdateTargetOptions()
    {
        var previous = _selectedTarget?.Value;
        var options = Source.Media == null
            ? []
            : IsPartitionTableOperation
                ? RecoveryTargetOptions.GetPartitionTableOptions(Source.Media)
                : RecoveryTargetOptions.GetFileSystemOptions(Source.Media);

        TargetOptions = options;
        SelectedTarget = options.FirstOrDefault(x => x.Value == previous) ?? options.FirstOrDefault();
        this.RaisePropertyChanged(nameof(TargetPlaceholder));
    }

    public string OutputPath
    {
        get => _outputPath;
        set => this.RaiseAndSetIfChanged(ref _outputPath, value);
    }

    /// <summary>
    /// Path to log file. Default is next to image file or in home directory for physical disks, until edited.
    /// </summary>
    public string LogPath
    {
        get => _logPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _logPath, value);
            if (!_updatingLogPath)
            {
                _logPathEdited = !string.IsNullOrWhiteSpace(value);
            }
        }
    }

    public ReactiveCommand<Unit, Unit> BrowseOutputPathCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseLogPathCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetLogPathCommand { get; }
    public ReactiveCommand<Unit, Unit> StartCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }

    private void ResetLogPath()
    {
        _logPathEdited = false;
        UpdateDefaultLogPath();
    }

    private void UpdateDefaultLogPath()
    {
        if (_logPathEdited)
        {
            return;
        }

        _updatingLogPath = true;
        try
        {
            LogPath = string.IsNullOrWhiteSpace(Source.Path)
                ? string.Empty
                : RecoveryLogPath.GetDefault(Source.Path, Source.IsPhysicalDisk, _selectedOperation.Value);
        }
        finally
        {
            _updatingLogPath = false;
        }
    }

    private async Task BrowseOutputPathAsync()
    {
        var path = await _dialogService.ShowOpenFolderDialogAsync("Select directory to write salvaged files to");
        if (path != null) OutputPath = path;
    }

    private async Task BrowseLogPathAsync()
    {
        var path = await _dialogService.ShowSaveFileDialogAsync("Select log file",
        [
            new FileFilterItem { Name = "Log files", Extensions = ["log", "txt"] },
            new FileFilterItem { Name = "All files", Extensions = ["*"] }
        ], System.IO.Path.GetFileName(LogPath));
        if (path != null) LogPath = path;
    }

    private async Task StartAsync()
    {
        var operation = _selectedOperation.Value;
        var targetPath = _selectedTarget!.Value;
        var targetTitle = _selectedTarget.Title;
        var logPath = LogPath.Trim();
        var outputPath = OutputPath.Trim();
        var byteswap = Source.Byteswap;
        var rdbMode = _selectedRdbMode.Value;
        var uaeMetadata = _selectedUaeMetadata.Value;
        var salvageOrphans = _salvageOrphans;
        var sourceType = Source.IsPhysicalDisk ? "physical disk" : "image file";
        var target = IsPartitionTableOperation ? "partition table" : "file system";
        var description = $"'{targetTitle}' in {sourceType} '{Source.Media?.Name ?? Source.Path}'";

        var confirmed = operation switch
        {
            RecoveryOperation.PartitionTableRepair or RecoveryOperation.FileSystemRepair =>
                await _dialogService.ShowConfirmDialogAsync($"Repair {target}",
                    $"WARNING: Repair writes changes to {description} and can potentially corrupt the {target} further.{Environment.NewLine}{Environment.NewLine}Repair should only be done on a backup of the disk or image file. Make sure you have a backup before continuing.{Environment.NewLine}{Environment.NewLine}Do you understand the risk and want to repair the {target}?"),
            RecoveryOperation.FileSystemSalvage =>
                await _dialogService.ShowConfirmDialogAsync("Salvage file system",
                    $"Do you want to salvage directories and files from {description} to directory '{outputPath}'?"),
            _ => await _dialogService.ShowConfirmDialogAsync($"Scan {target}",
                $"Do you want to scan {description}?")
        };

        if (!confirmed)
            return;

        await Progress.RunAsync($"{_selectedOperation.Title} {description}", async (progress, token) =>
        {
            var report = operation switch
            {
                RecoveryOperation.PartitionTableScan => await _imagingService.ScanPartitionTableAsync(targetPath,
                    null, byteswap, logPath, progress, token),
                RecoveryOperation.PartitionTableRepair => await _imagingService.RepairPartitionTableAsync(
                    targetPath, null, rdbMode, byteswap, logPath, progress, token),
                RecoveryOperation.FileSystemScan => await _imagingService.ScanFileSystemAsync(targetPath, byteswap,
                    logPath, progress, token),
                RecoveryOperation.FileSystemRepair => await _imagingService.RepairFileSystemAsync(targetPath,
                    byteswap, logPath, progress, token),
                RecoveryOperation.FileSystemSalvage => await _imagingService.SalvageFileSystemAsync(targetPath,
                    outputPath, uaeMetadata, salvageOrphans, byteswap, logPath, progress, token),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            return FormatReport(report);
        });
    }

    /// <summary>
    /// Format recovery report as result message.
    /// </summary>
    public static string FormatReport(RecoveryReport report)
    {
        var builder = new StringBuilder();
        var type = string.IsNullOrWhiteSpace(report.TargetType) ? string.Empty : $"{report.TargetType}: ";

        if (report.Aborted)
        {
            builder.AppendLine($"{type}Aborted.");
        }
        else if (report.Salvage != null)
        {
            var salvage = report.Salvage;
            builder.AppendLine(
                $"{type}Salvaged {salvage.Directories} directories and {salvage.Files} files ({MediaOptions.FormatBytes(salvage.Bytes)}).");
            if (salvage.PartialFiles > 0 || salvage.Skipped > 0 || salvage.OrphanedDirectories > 0 ||
                salvage.Errors > 0)
            {
                builder.AppendLine(
                    $"{salvage.PartialFiles} partial files, {salvage.OrphanedDirectories} orphaned directories, {salvage.Skipped} skipped, {salvage.Errors} errors.");
            }
        }
        else if (report.Operation is RecoveryOperation.PartitionTableRepair or RecoveryOperation.FileSystemRepair)
        {
            builder.AppendLine(report.IsValid
                ? report.Written ? $"{type}Repaired, {report.Fixed} issue(s) fixed." : $"{type}Valid, no repair needed."
                : $"{type}{report.Errors - report.Fixed} error(s) couldn't be repaired, {report.Fixed} issue(s) fixed.");
        }
        else
        {
            builder.AppendLine(report.IsValid
                ? $"{type}Valid, no errors found{(report.Warnings > 0 ? $", {report.Warnings} warning(s)" : string.Empty)}."
                : $"{type}{report.Errors} error(s) and {report.Warnings} warning(s) found.");
        }

        var issues = report.Issues
            .Where(x => x.Level != RecoveryIssueLevel.Info)
            .Take(10)
            .ToList();
        foreach (var issue in issues)
        {
            builder.AppendLine($"- {issue}");
        }

        var moreIssues = report.Issues.Count(x => x.Level != RecoveryIssueLevel.Info) - issues.Count;
        if (moreIssues > 0)
        {
            builder.AppendLine($"- ... and {moreIssues} more");
        }

        builder.Append($"Detailed log written to '{report.LogPath}'.");

        return builder.ToString();
    }
}
