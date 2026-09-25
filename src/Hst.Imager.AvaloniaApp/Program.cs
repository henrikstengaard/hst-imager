using System;
using System.IO;
using Avalonia;
using Avalonia.ReactiveUI;
using Hst.Imager.AvaloniaApp.Services;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using Serilog;
using Serilog.Events;
using Velopack;

namespace Hst.Imager.AvaloniaApp;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var appDataPath = ApplicationDataHelper.GetApplicationDataDir("HstImager");

        SetupLogging(appDataPath);

        StartAsAdministratorIfEnabled(appDataPath, args);

        try
        {
            BuildAvaloniaApp(appDataPath, args).StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Application terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp(string? appDataPath = null, string[]? startupArgs = null)
    {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>();

        return AppBuilder.Configure<App>(() => new App(appDataPath ?? ApplicationDataHelper.GetApplicationDataDir("HstImager"), startupArgs ?? []))
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();
    }

    /// <summary>
    /// Restart app with administrator privileges, if enabled in settings and not already running as administrator.
    /// Continues without administrator privileges, if elevation fails or is declined.
    /// </summary>
    private static void StartAsAdministratorIfEnabled(string appDataPath, string[] args)
    {
        try
        {
            var settings = ApplicationDataHelper.ReadSettings<Settings>(appDataPath, Constants.AppName)
                .GetAwaiter().GetResult();
            if (settings is not { StartAsAdministrator: true } || User.IsAdministrator())
            {
                return;
            }

            Log.Information("Start as administrator is enabled, restarting with administrator privileges");

            // exits current process, if elevated process is started
            if (!User.Elevate(args, settings))
            {
                Log.Warning("Failed to start as administrator, continuing without administrator privileges");
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to start as administrator, continuing without administrator privileges");
        }
    }

    private static void SetupLogging(string appDataPath)
    {
        var logFilePath = Path.Combine(appDataPath, "logs", "log-imager.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] ({SourceContext}) {Message}{NewLine}{Exception}")
            .CreateLogger();
    }
}
