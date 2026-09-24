using System;
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

    private bool _allPhysicalDrives;
    private bool _verify;
    private bool _force;
    private int _retries = 5;
    private bool _skipUnusedSectors;
    private bool _sparseFiles = true;
    private bool _debugMode;
    private bool _useCache = true;
    private bool _isMacOs;
    private bool _isSaved;

    public SettingsViewModel(ISettingsService settingsService, AppStateModel appState)
    {
        _settingsService = settingsService;
        _appState = appState;
        _isMacOs = appState.IsMacOs;

        LogsPath = appState.LogsPath;

        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);

        // Load settings on creation
        _ = LoadSettingsAsync();

        // Auto-save on any property change (debounced)
        this.Changed
            .Throttle(TimeSpan.FromMilliseconds(800))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(e => { _ = SaveAsync(); });
    }

    public bool AllPhysicalDrives { get => _allPhysicalDrives; set => this.RaiseAndSetIfChanged(ref _allPhysicalDrives, value); }
    public bool Verify { get => _verify; set => this.RaiseAndSetIfChanged(ref _verify, value); }
    public bool Force { get => _force; set => this.RaiseAndSetIfChanged(ref _force, value); }
    public int Retries { get => _retries; set => this.RaiseAndSetIfChanged(ref _retries, value); }
    public bool SkipUnusedSectors { get => _skipUnusedSectors; set => this.RaiseAndSetIfChanged(ref _skipUnusedSectors, value); }
    public bool SparseFiles { get => _sparseFiles; set => this.RaiseAndSetIfChanged(ref _sparseFiles, value); }
    public bool DebugMode { get => _debugMode; set => this.RaiseAndSetIfChanged(ref _debugMode, value); }
    public bool UseCache { get => _useCache; set => this.RaiseAndSetIfChanged(ref _useCache, value); }
    public bool IsMacOs { get => _isMacOs; set => this.RaiseAndSetIfChanged(ref _isMacOs, value); }
    public bool IsSaved { get => _isSaved; set => this.RaiseAndSetIfChanged(ref _isSaved, value); }
    public string LogsPath { get; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            AllPhysicalDrives = settings.AllPhysicalDrives;
            Verify = settings.Verify;
            Force = settings.Force;
            Retries = settings.Retries;
            SkipUnusedSectors = settings.SkipUnusedSectors;
            SparseFiles = settings.SparseFiles;
            DebugMode = settings.DebugMode;
            UseCache = settings.UseCache;
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
                Verify = Verify,
                Force = Force,
                Retries = Retries,
                SkipUnusedSectors = SkipUnusedSectors,
                SparseFiles = SparseFiles,
                DebugMode = DebugMode,
                UseCache = UseCache
            };
            _appState.Settings = settings;
            await _settingsService.SaveSettingsAsync(settings);
            IsSaved = true;
        }
        catch { /* ignore save errors */ }
    }
}
