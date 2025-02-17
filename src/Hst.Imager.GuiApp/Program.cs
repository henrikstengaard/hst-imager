namespace Hst.Imager.GuiApp
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Bootstrappers;
#if (BACKEND == false)
    using ElectronNET.API;
#endif
    using Helpers;
    using Hst.Imager.Core.Helpers;
    using Hst.Imager.Core.Models;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Hosting;
    using Models;
    using Serilog;
    using Serilog.Events;
    using OperatingSystem = Hst.Core.OperatingSystem;
#if WINDOWS
    using Squirrel;
    using Squirrel.Sources;
#endif

    public class Program
    {
        public static async Task Main(string[] args)
        {
#if WINDOWS
            using (var mgr = new UpdateManager(new GithubSource(
                       "https://github.com/henrikstengaard/hst-imager",
                       string.Empty,
                       false)))
            {
                SquirrelAwareApp.HandleEvents(
                    onInitialInstall: (_, _) => CreateShortcutsForExecutable(mgr),
                    onAppUpdate: (_, _) => CreateShortcutsForExecutable(mgr),
                    onAppUninstall: (_, _) => RemoveShortcutsForExecutable(mgr));
            }
#endif
            var worker = false;
            var baseUrl = string.Empty;
            int processId = 0;
            
            for (var i = 0; i < args.Length; i++)
            {
#if WINDOWS
                // stop application, if any argument starts with "--squirrel"
                if (args[i].IndexOf("--squirrel", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }
#endif

                if (args[i].Equals("--worker", StringComparison.OrdinalIgnoreCase))
                {
                    worker = true;
                }

                if (args[i].Equals("--baseurl", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    baseUrl = args[i + 1];
                }
                
                if (args[i].Equals("--process-id", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (!int.TryParse(args[i + 1], out processId))
                    {
                        processId = 0;
                    }
                }
            }

            var debugMode = (await ApplicationDataHelper.ReadSettings<Settings>(Constants.AppName))?.DebugMode ?? false;
            var hasDebugEnabled = ApplicationDataHelper.HasDebugEnabled(Constants.AppName) || debugMode;
            
            if (worker &&
                !string.IsNullOrWhiteSpace(baseUrl))
            {
                await WorkerBootstrapper.Start(baseUrl, processId, hasDebugEnabled);
                return;
            }

#if RELEASE
            SetupReleaseLogging(hasDebugEnabled);
#else
            SetupDebugLogging();
#endif
            Log.Information("Imager starting");
            Log.Information($"OS: {OperatingSystem.OsDescription}");

            try
            {
                await CreateHostBuilder(args).Build().RunAsync();
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Host terminated unexpectedly");
                throw;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        private static string GetArgument(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (!args[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    continue;
                }

                return args[i + 1];
            }

            return null;
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder =>
                {
#if (BACKEND == false)
                    Log.Information("Electron");
                    webBuilder.UseElectron(args);
#endif
                    webBuilder.UseStartup<Startup>();

                    var portArg = GetArgument(args, "--port");
                    if (!string.IsNullOrWhiteSpace(portArg) && int.TryParse(portArg, out var port))
                    {
                        webBuilder.UseUrls($"http://localhost:{port}/");
                    }
                });

        private static void SetupReleaseLogging(bool hasDebugEnabled)
        {
            var logFilePath = Path.Combine(ApplicationDataHelper.GetApplicationDataDir(Constants.AppName), "logs",
                "log-imager.txt");
            if (hasDebugEnabled)
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .WriteTo.File(
                        logFilePath,
                        rollingInterval: RollingInterval.Day,
                        outputTemplate:
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] ({SourceContext}) {Message}{NewLine}{Exception}")
                    .CreateLogger();
            }
            else
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Error()
                    .WriteTo.File(
                        logFilePath,
                        rollingInterval: RollingInterval.Day,
                        outputTemplate:
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] ({SourceContext}) {Message}{NewLine}{Exception}")
                    .CreateLogger();
            }
        }

        private static void SetupDebugLogging()
        {
            var logFilePath = Path.Combine(Path.GetDirectoryName(WorkerHelper.GetExecutingFile()), "logs",
                "log-imager.txt");
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] ({SourceContext}) {Message}{NewLine}{Exception}")
                .CreateLogger();
        }
#if WINDOWS
        private static void CreateShortcutsForExecutable(UpdateManager manager)
        {
            var exeName = "Hst.Imager.exe";
            var location = ShortcutLocation.StartMenu | ShortcutLocation.Desktop;
            var updateOnly = !Environment.CommandLine.Contains("squirrel-install");
            
            manager.CreateShortcutsForExecutable(exeName, location, updateOnly);
        }

        private static void RemoveShortcutsForExecutable(UpdateManager manager)
        {
            var exeName = "Hst.Imager.exe";
            var location = ShortcutLocation.StartMenu | ShortcutLocation.Desktop;
            
            manager.RemoveShortcutsForExecutable(exeName, location);
        }
#endif
    }
}