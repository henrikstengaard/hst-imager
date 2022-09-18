namespace Hst.Imager.ConsoleApp
{
    using System;
    using System.CommandLine.Builder;
    using System.CommandLine.Parsing;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using Serilog.Core;
    using Serilog.Events;

    public static class Program
    {
        private static LoggerConfiguration loggerConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(AppState.Instance.LoggingLevelSwitch)
            .WriteTo.Console();

        public static void AddLogFile(FileInfo fileInfo)
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

            var rootCommand = CommandFactory.CreateRootCommand();

            // global handler for verbose and log file options
            var parser = new CommandLineBuilder(rootCommand).AddMiddleware(async (context, next) =>
            {
                var verbose = context.ParseResult.GetValueForOption(CommandFactory.VerboseOption);
                var logFile = context.ParseResult.GetValueForOption(CommandFactory.LogFileOption);

                AppState.Instance.LoggingLevelSwitch.MinimumLevel =
                    verbose ? LogEventLevel.Debug : LogEventLevel.Information;
                
                AddLogFile(logFile);

                var appState = AppState.Instance;
                var app = $"Hst Imager v{appState.Version.Major}.{appState.Version.Minor}.{appState.Version.Build} ({appState.BuildDate})";
                var author = "Henrik Noerfjand Stengaard";
            
                Log.Logger.Information(app);
                Log.Logger.Information(author);
                Log.Logger.Information($"[CMD] {string.Join(" ", args)}");
                
                await next(context);
            }).Build();

            return await parser.InvokeAsync(args);
        }
        
        //     switch (arguments.Command)
        //     {
        //         case Arguments.CommandEnum.List:
        //             var listCommand = new ListCommand(loggerFactory.CreateLogger<ListCommand>(), commandHelper, physicalDrives);
        //             listCommand.ListRead += (_, args) =>
        //             {
        //                 //
        //                 // await Task.Run(() =>
        //                 // {
        //                 //     Console.WriteLine(JsonSerializer.Serialize(physicalDrivesList, JsonSerializerOptions));
        //                 // });
        //                 InfoPresenter.PresentInfo(args.MediaInfos);
        //             };
        //             var listResult = await listCommand.Execute(cancellationTokenSource.Token);
        //             Console.WriteLine(listResult.IsSuccess ? "Done" : $"ERROR: Read failed, {listResult.Error}");
        //             break;
        //         case Arguments.CommandEnum.Info:
        //             var infoCommand = new InfoCommand(loggerFactory.CreateLogger<InfoCommand>(), commandHelper, physicalDrives, arguments.SourcePath);
        //             infoCommand.DiskInfoRead += (_, args) => { InfoPresenter.PresentInfo(args.MediaInfo); };
        //             var infoResult = await infoCommand.Execute(cancellationTokenSource.Token);
        //             Console.WriteLine(infoResult.IsSuccess ? "Done" : $"ERROR: Read failed, {infoResult.Error}");
        //             break;
        //         case Arguments.CommandEnum.Verify:
        //             Console.WriteLine("Verifying source image to destination");
        //
        //             GenericPresenter.PresentPaths(arguments);
        //
        //             var verifyCommand = new VerifyCommand(loggerFactory.CreateLogger<VerifyCommand>(), commandHelper, physicalDrives, arguments.SourcePath,
        //                 arguments.DestinationPath,
        //                 arguments.Size);
        //             verifyCommand.DataProcessed += (_, args) => { GenericPresenter.Present(args); };
        //             var verifyResult = await verifyCommand.Execute(cancellationTokenSource.Token);
        //             Console.WriteLine(verifyResult.IsSuccess ? "Done" : $"ERROR: Verify failed, {verifyResult.Error}");
        //             break;
        //         case Arguments.CommandEnum.Optimize:
        //             Console.WriteLine("Optimizing image file");
        //             Console.WriteLine($"Path: {arguments.SourcePath}");
        //             var optimizeCommand = new OptimizeCommand(loggerFactory.CreateLogger<OptimizeCommand>(), commandHelper, arguments.SourcePath);
        //             var optimizeResult = await optimizeCommand.Execute(cancellationTokenSource.Token);
        //             Console.WriteLine(optimizeResult.IsSuccess
        //                 ? "Done"
        //                 : $"ERROR: Optimize failed, {optimizeResult.Error}");
        //             break;
        //     }
        // }
    }
}