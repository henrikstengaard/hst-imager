using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.AvaloniaApp.ViewModels;
using Hst.Imager.AvaloniaApp.Views;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Hst.Imager.AvaloniaApp;

public partial class App : Application
{
    private readonly string _appDataPath;
    private IServiceProvider? _services;

    public App(string appDataPath)
    {
        _appDataPath = appDataPath;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _services = ConfigureServices(_appDataPath);

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

    private static IServiceProvider ConfigureServices(string appDataPath)
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

        // ViewModels
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
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
