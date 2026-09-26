using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Hst.Imager.Core.Models;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.AvaloniaApp.ViewModels;
using Hst.Imager.AvaloniaApp.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Hst.Imager.AvaloniaApp;

public partial class App : Application
{
    private readonly string _appDataPath;
    private readonly string[] _startupArgs;
    private IServiceProvider? _services;

    public App(string appDataPath, string[] startupArgs)
    {
        _appDataPath = appDataPath;
        _startupArgs = startupArgs;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _services = ConfigureServices(_appDataPath, _startupArgs);

        // load saved settings before any page lists or uses physical drives
        try
        {
            var settingsService = _services.GetRequiredService<ISettingsService>();
            var settings = Task.Run(settingsService.GetSettingsAsync).GetAwaiter().GetResult();
            ApplyColorMode(settings.ColorMode);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to read settings, using defaults");
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowVm = _services.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow(mainWindowVm);
            desktop.MainWindow = mainWindow;

            var dialogService = _services.GetRequiredService<IDialogService>() as DialogService;
            dialogService?.SetWindow(mainWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Apply color mode to app. System uses default theme variant, which follows operating system light or dark mode
    /// and changes along with it while app is running.
    /// </summary>
    public static void ApplyColorMode(Settings.ColorModeEnum colorMode)
    {
        if (Current == null)
        {
            return;
        }

        Current.RequestedThemeVariant = colorMode switch
        {
            Settings.ColorModeEnum.Light => ThemeVariant.Light,
            Settings.ColorModeEnum.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private static IServiceProvider ConfigureServices(string appDataPath, string[] startupArgs)
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: false);
        });

        // App state
        var appState = new AppStateModel
        {
            AppDataPath = appDataPath,
            LogsPath = Path.Combine(appDataPath, "logs"),
            IsAdministrator = User.IsAdministrator(),
            IsWindows = Hst.Core.OperatingSystem.IsWindows(),
            IsMacOs = Hst.Core.OperatingSystem.IsMacOs(),
            IsLinux = Hst.Core.OperatingSystem.IsLinux()
        };

        services.AddSingleton(appState);
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IMediaService, MediaService>();
        services.AddSingleton<IImagingService, ImagingService>();
        services.AddSingleton<IAppStateService, AppStateService>();
        services.AddSingleton<INavigationService, NavigationService>();

        // ViewModels
        services.AddSingleton<ProgressViewModel>();
        services.AddTransient<StartViewModel>();
        services.AddTransient<ReadViewModel>();
        services.AddTransient<WriteViewModel>();
        services.AddTransient<InfoViewModel>();
        services.AddTransient<TransferViewModel>();
        services.AddTransient<CompareViewModel>();
        services.AddTransient<BlankViewModel>();
        services.AddTransient<OptimizeViewModel>();
        services.AddTransient<FormatViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();
        services.AddSingleton<MainWindowViewModel>(sp => new MainWindowViewModel(sp, startupArgs));

        return services.BuildServiceProvider();
    }
}
