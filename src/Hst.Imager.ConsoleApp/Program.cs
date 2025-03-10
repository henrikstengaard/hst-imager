using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;

namespace Hst.Imager.ConsoleApp
{
    using System.CommandLine.Builder;
    using System.CommandLine.Parsing;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amiga.FileSystems.Pfs3;
    using Core.Commands;
    using Hst.Core;
    using Serilog;
    using Serilog.Core;
    using Serilog.Events;
    using Serilog.Sinks.SystemConsole.Themes;

    public static class Program
    {
        private static LoggerConfiguration loggerConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(AppState.Instance.LoggingLevelSwitch)
            .WriteTo.Console(theme: ConsoleTheme.None);

        private static void AddLogFile(FileInfo fileInfo)
        {
            if (fileInfo == null)
            {
                return;
            }

            loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink((ILogEventSink)Log.Logger) // passing in the old logger
                .WriteTo.File(fileInfo.FullName);
            
            Log.Logger = loggerConfig
                .CreateLogger();
        }

        static async Task<int> Main(string[] args)
        {
            Log.Logger = loggerConfig
                .CreateLogger();

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            if ((args.Any() && args[0].Equals("list", System.StringComparison.OrdinalIgnoreCase)) ||
                (!User.IsAdministrator && ArgsHelper.HasPhysicalDrivePaths(args)))
            {
                Log.Logger.Warning("Command or arguments used requires administrator privileges to access physical drives!");
                Log.Logger.Warning(OperatingSystem.IsWindows()
                    ? "Run Command Prompt or PowerShell as Administrator and run Hst Imager with same arguments."
                    : "Run Hst Imager with sudo and same arguments.");
            }

            AppState.Instance.Settings = await ApplicationDataHelper.ReadSettings<Settings>(Core.Models.Constants.AppName);

            var rootCommand = CommandFactory.CreateRootCommand();

            // global handler for verbose and log file options
            var parser = new CommandLineBuilder(rootCommand).AddMiddleware(async (context, next) =>
            {
                var verbose = context.ParseResult.GetValueForOption(CommandFactory.VerboseOption);
                var logFile = context.ParseResult.GetValueForOption(CommandFactory.LogFileOption);
                var format = context.ParseResult.GetValueForOption(CommandFactory.FormatOption);

                AppState.Instance.LoggingLevelSwitch.MinimumLevel =
                    verbose ? LogEventLevel.Debug : LogEventLevel.Information;

                if (format == FormatEnum.Json)
                {
                    AppState.Instance.LoggingLevelSwitch.MinimumLevel = LogEventLevel.Error;
                }

                AddLogFile(logFile);

                if (verbose)
                {
                    Pfs3Logger.Instance.RegisterLogger(new SerilogPfs3Logger());
                }

                var appState = AppState.Instance;
                var app =
                    $"Hst Imager v{appState.Version.Major}.{appState.Version.Minor}.{appState.Version.Build} ({appState.BuildDate})";
                var author = "Henrik Nørfjand Stengaard";

                Log.Logger.Information(app);
                Log.Logger.Information(author);
                Log.Logger.Information($"[CMD] {string.Join(" ", args)}");

                await next(context);
            }).UseDefaults().Build();

            return await parser.InvokeAsync(args);
        }
    }
}