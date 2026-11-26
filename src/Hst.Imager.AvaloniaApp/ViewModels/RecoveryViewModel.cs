using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Hst.Amiga.RigidDiskBlocks.Recovery;
using Hst.Imager.AvaloniaApp.Models;
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
            },
            new RecoveryOperationOption
            {
                Title = "Full repair", Value = RecoveryOperation.FullRepair,
                Description = "Repair partition tables, read partitions again and repair Amiga file systems (FFS, PFS3 and SFS) in all Rigid Disk Block partitions."
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

    /// <summary>
    /// Operation targets disk or partition table, which includes full repair.
    /// </summary>
    public bool IsPartitionTableOperation => _selectedOperation.Value is RecoveryOperation.PartitionTableScan
        or RecoveryOperation.PartitionTableRepair or RecoveryOperation.FullRepair;

    public bool IsFileSystemOperation => !IsPartitionTableOperation;

    public bool IsRepair => _selectedOperation.Value is RecoveryOperation.PartitionTableRepair
        or RecoveryOperation.FileSystemRepair or RecoveryOperation.FullRepair;

    /// <summary>
    /// Operation repairs partition table, so rigid disk block repair mode can be selected.
    /// </summary>
    public bool IsPartitionTableRepair => _selectedOperation.Value is RecoveryOperation.PartitionTableRepair
        or RecoveryOperation.FullRepair;

    public bool IsSalvage => _selectedOperation.Value == RecoveryOperation.FileSystemSalvage;

    public string StartTitle => IsRepair ? "Start repair" : IsSalvage ? "Start salvage" : "Start scan";

    public string StartIcon => IsRepair ? "fa-screwdriver-wrench" : IsSalvage ? "fa-life-ring" : "fa-stethoscope";

    public string TargetHint => _selectedOperation.Value == RecoveryOperation.FullRepair
        ? "Partition tables are repaired first, then partitions are read again and file systems in all Rigid Disk Block partitions are repaired."
        : IsPartitionTableOperation
        ?"Partition tables present are detected, when disk is selected. Select Rigid Disk Block to scan or repair a Rigid Disk Block, which is damaged and not detected."
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
        var target = operation switch
        {
            RecoveryOperation.FullRepair => "partition tables and file systems",
            RecoveryOperation.PartitionTableScan or RecoveryOperation.PartitionTableRepair => "partition table",
            _ => "file system"
        };
        var description = $"'{targetTitle}' in {sourceType} '{Source.Media?.Name ?? Source.Path}'";

        var confirmed = operation switch
        {
            RecoveryOperation.PartitionTableRepair or RecoveryOperation.FileSystemRepair
                or RecoveryOperation.FullRepair =>
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

        await Progress.RunAsync($"{_selectedOperation.Title} {description}", async (progress, details, token) =>
        {
            var report = operation switch
            {
                RecoveryOperation.PartitionTableScan => await _imagingService.ScanPartitionTableAsync(targetPath,
                    null, byteswap, logPath, progress, token, details),
                RecoveryOperation.PartitionTableRepair => await _imagingService.RepairPartitionTableAsync(
                    targetPath, null, rdbMode, byteswap, logPath, progress, token, details),
                RecoveryOperation.FileSystemScan => await _imagingService.ScanFileSystemAsync(targetPath, byteswap,
                    logPath, progress, token, details),
                RecoveryOperation.FileSystemRepair => await _imagingService.RepairFileSystemAsync(targetPath,
                    byteswap, logPath, progress, token, details),
                RecoveryOperation.FileSystemSalvage => await _imagingService.SalvageFileSystemAsync(targetPath,
                    outputPath, uaeMetadata, salvageOrphans, byteswap, logPath, progress, token, details),
                RecoveryOperation.FullRepair => await _imagingService.FullRepairAsync(targetPath, rdbMode, byteswap,
                    logPath, progress, token, details),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            return new ProgressResult(FormatReport(report), GetStatus(report), IsWarning(report));
        });

        // read disk again after repair to refresh partition tables and partitions available as targets
        if (operation is RecoveryOperation.PartitionTableRepair or RecoveryOperation.FileSystemRepair
            or RecoveryOperation.FullRepair)
        {
            await Source.ReloadMediaAsync();
        }
    }

    /// <summary>
    /// Format recovery report as result message.
    /// </summary>
    public static string FormatReport(RecoveryReport report)
    {
        var builder = new StringBuilder();

        if (report.PartitionTablesFound.HasValue)
        {
            builder.AppendLine(Count(report.PartitionTablesFound.Value, "partition table", "partition tables", "found"));
        }

        if (report.PartitionsFound.HasValue)
        {
            builder.AppendLine(Count(report.PartitionsFound.Value, "partition", "partitions", "found"));
        }

        if (report.FileSystemsFound.HasValue)
        {
            builder.AppendLine(Count(report.FileSystemsFound.Value, "file system", "file systems", "found on disk"));
        }

        if (report.DirectoriesFound.HasValue)
        {
            builder.AppendLine(Count(report.DirectoriesFound.Value, "directory", "directories", "found"));
        }

        if (report.FileSystemsChecked.HasValue)
        {
            builder.AppendLine(Count(report.FileSystemsChecked.Value, "file system", "file systems", "checked"));
        }

        if (report.FilesFound.HasValue)
        {
            builder.AppendLine(Count(report.FilesFound.Value, "file", "files", "found"));
        }

        if (report.Salvage != null)
        {
            var salvage = report.Salvage;
            builder.AppendLine(Count(salvage.Directories, "directory", "directories", "salvaged"));
            builder.AppendLine(Count(salvage.Files, "file", "files", "salvaged"));
            builder.AppendLine($"{MediaOptions.FormatBytes(salvage.Bytes)} salvaged");
            if (salvage.PartialFiles > 0)
            {
                builder.AppendLine(Count(salvage.PartialFiles, "partial file", "partial files", "salvaged"));
            }

            if (salvage.OrphanedDirectories > 0)
            {
                builder.AppendLine(Count(salvage.OrphanedDirectories, "orphaned directory", "orphaned directories",
                    "salvaged"));
            }

            if (salvage.Skipped > 0)
            {
                builder.AppendLine(Count(salvage.Skipped, "entry", "entries", "skipped"));
            }

            if (salvage.Errors > 0)
            {
                builder.AppendLine(Count(salvage.Errors, "error", "errors", "during salvage"));
            }
        }

        builder.AppendLine(Count(report.IssuesFound, "issue", "issues", "found"));
        if (report.Operation is RecoveryOperation.PartitionTableRepair or RecoveryOperation.FileSystemRepair
            or RecoveryOperation.FullRepair)
        {
            builder.AppendLine(Count(report.Fixed, "issue", "issues", "fixed"));
            builder.AppendLine(Count(report.IssuesNotFixed, "issue", "issues", "not fixed"));
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Get total status of recovery operation, e.g. "Partition table valid" or "File system repaired".
    /// </summary>
    public static string GetStatus(RecoveryReport report)
    {
        if (report.Aborted)
        {
            return "Aborted";
        }

        switch (report.Operation)
        {
            case RecoveryOperation.PartitionTableScan:
            case RecoveryOperation.PartitionTableRepair:
                if (report.PartitionTablesFound == 0)
                {
                    return "No partition table found";
                }

                var multiple = report.PartitionTablesFound > 1;
                var partitionTable = multiple ? "Partition tables" : "Partition table";
                return GetStatus(report, partitionTable, multiple);
            case RecoveryOperation.FileSystemSalvage:
                return report.Errors == 0 ? "Files salvaged" : "Files salvaged with errors";
            case RecoveryOperation.FullRepair:
                return GetStatus(report, "Disk", false);
            default:
                return GetStatus(report, "File system", false);
        }
    }

    private static string GetStatus(RecoveryReport report, string target, bool multiple)
    {
        if (report.Operation is RecoveryOperation.PartitionTableRepair or RecoveryOperation.FileSystemRepair
            or RecoveryOperation.FullRepair)
        {
            return report.IsValid
                ? report.Written ? $"{target} repaired" : $"{target} valid, no repair needed"
                : $"{target} not repaired";
        }

        return report.IsValid ? $"{target} valid" : $"{target} {(multiple ? "have" : "has")} errors";
    }

    /// <summary>
    /// Status is a warning, if operation was aborted, errors were found or not repaired, or no partition table was
    /// found.
    /// </summary>
    public static bool IsWarning(RecoveryReport report) =>
        report.Aborted ||
        report.PartitionTablesFound == 0 ||
        (report.Operation == RecoveryOperation.FileSystemSalvage ? report.Errors > 0 : !report.IsValid);

    private static string Count(int count, string singular, string plural, string suffix) =>
        $"{count} {(count == 1 ? singular : plural)} {suffix}";
}
