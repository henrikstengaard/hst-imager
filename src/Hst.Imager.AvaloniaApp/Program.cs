using System;
using System.IO;
using Avalonia;
using Avalonia.ReactiveUI;
using Hst.Imager.Core.Helpers;
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

        try
        {
            BuildAvaloniaApp(appDataPath).StartWithClassicDesktopLifetime(args);
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

    public static AppBuilder BuildAvaloniaApp(string? appDataPath = null)
    {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>();

        return AppBuilder.Configure<App>(() => new App(appDataPath ?? ApplicationDataHelper.GetApplicationDataDir("HstImager")))
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();
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
