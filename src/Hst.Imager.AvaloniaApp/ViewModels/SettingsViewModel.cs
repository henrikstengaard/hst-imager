using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.Core.Models;
using ReactiveUI;
using Unit = System.Reactive.Unit;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly AppStateModel _appState;

    private Settings _settings = new();
    private bool _allPhysicalDrives;
    private SelectOption _macOsElevateMethod;
    private bool _verify;
    private bool _force;
    private int _retries = 5;
    private bool _skipUnusedSectors;
    private bool _sparseFiles = true;
    private bool _debugMode;
    private bool _isMacOs;
    private bool _isSaved;

    public SettingsViewModel(ISettingsService settingsService, AppStateModel appState, IDialogService dialogService)
    {
        _settingsService = settingsService;
        _appState = appState;
        _isMacOs = appState.IsMacOs;

        LogsPath = appState.LogsPath;

        MacOsElevateMethodOptions =
        [
            new SelectOption { Title = "Osascript sudo", Value = nameof(Settings.MacOsElevateMethodEnum.OsascriptSudo) },
            new SelectOption { Title = "Osascript administrator privileges", Value = nameof(Settings.MacOsElevateMethodEnum.OsascriptAdministrator) }
        ];
        _macOsElevateMethod = MacOsElevateMethodOptions[1];

        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
        ViewLogsCommand = ReactiveCommand.CreateFromTask(() => dialogService.OpenExternalAsync(LogsPath));

        // Load settings on creation
        _ = LoadSettingsAsync();

        // Auto-save on any property change (debounced)
        this.Changed
            .Where(e => e.PropertyName != nameof(IsSaved))
            .Throttle(TimeSpan.FromMilliseconds(800))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(e => { _ = SaveAsync(); });
    }

    public List<SelectOption> MacOsElevateMethodOptions { get; }

    public bool AllPhysicalDrives { get => _allPhysicalDrives; set => this.RaiseAndSetIfChanged(ref _allPhysicalDrives, value); }

    public SelectOption MacOsElevateMethod
    {
        get => _macOsElevateMethod;
        set
        {
            this.RaiseAndSetIfChanged(ref _macOsElevateMethod, value);
            if (IsDebugModeDisabled)
                DebugMode = false;
            this.RaisePropertyChanged(nameof(IsDebugModeDisabled));
            this.RaisePropertyChanged(nameof(IsDebugModeEnabled));
        }
    }

    public bool Verify { get => _verify; set => this.RaiseAndSetIfChanged(ref _verify, value); }
    public bool Force { get => _force; set => this.RaiseAndSetIfChanged(ref _force, value); }
    public int Retries { get => _retries; set => this.RaiseAndSetIfChanged(ref _retries, value); }
    public bool SkipUnusedSectors { get => _skipUnusedSectors; set => this.RaiseAndSetIfChanged(ref _skipUnusedSectors, value); }
    public bool SparseFiles { get => _sparseFiles; set => this.RaiseAndSetIfChanged(ref _sparseFiles, value); }
    public bool DebugMode { get => _debugMode; set => this.RaiseAndSetIfChanged(ref _debugMode, value); }
    public bool IsMacOs { get => _isMacOs; set => this.RaiseAndSetIfChanged(ref _isMacOs, value); }
    public bool IsSaved { get => _isSaved; set => this.RaiseAndSetIfChanged(ref _isSaved, value); }
    public string LogsPath { get; }

    public bool IsDebugModeDisabled => IsMacOs &&
        _macOsElevateMethod.Value == nameof(Settings.MacOsElevateMethodEnum.OsascriptAdministrator);
    public bool IsDebugModeEnabled => !IsDebugModeDisabled;

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> ViewLogsCommand { get; }

    private async Task LoadSettingsAsync()
    {
        try
        {
            _settings = await _settingsService.GetSettingsAsync();
            AllPhysicalDrives = _settings.AllPhysicalDrives;
            MacOsElevateMethod = MacOsElevateMethodOptions.FirstOrDefault(x => x.Value == _settings.MacOsElevateMethod.ToString())
                                 ?? MacOsElevateMethodOptions[1];
            Verify = _settings.Verify;
            Force = _settings.Force;
            Retries = _settings.Retries;
            SkipUnusedSectors = _settings.SkipUnusedSectors;
            SparseFiles = _settings.SparseFiles;
            DebugMode = _settings.DebugMode;
        }
        catch { /* use defaults */ }
    }

    private async Task SaveAsync()
    {
        try
        {
            var settings = new Settings
            {
                AllPhysicalDrives = AllPhysicalDrives,
                MacOsElevateMethod = Enum.TryParse<Settings.MacOsElevateMethodEnum>(MacOsElevateMethod.Value, out var method)
                    ? method
                    : _settings.MacOsElevateMethod,
                Verify = Verify,
                Force = Force,
                Retries = Retries,
                SkipUnusedSectors = SkipUnusedSectors,
                SparseFiles = SparseFiles,
                DebugMode = DebugMode,
                UseCache = _settings.UseCache,
                CacheType = _settings.CacheType
            };
            _appState.Settings = settings;
            await _settingsService.SaveSettingsAsync(settings);
            IsSaved = true;
        }
        catch { /* ignore save errors */ }
    }
}
