namespace Hst.Imager.ConsoleApp
{
    using System.CommandLine;
    using System.Threading.Tasks;

    static class Program
    {
        static async Task<int> Main(string[] args)
        {
            //args = new[] { "rdb", "fs", "add", "hst.img", "pds3", @"d:\Temp\pfs3aio_3.1\pfs3aio" };
            // var mbrTest = new MbrTest();
            // mbrTest.Create();
            // mbrTest.Read();

            // var listOption = new Option<bool>(
            //     new[] { "--list", "-l" },
            //     "List physical drives.");
            // var infoOption = new Option<string>(
            //     new[] { "--info", "-i" },
            //     "Display information about physical drive or image file.")
            // {
            //     Arity = ArgumentArity.ExactlyOne
            // };
            // var readOption = new Option<string[]>(
            //     new[] { "--read", "-r" },
            //     "Read physical drive to image file.")
            // {
            //     AllowMultipleArgumentsPerToken = true,
            //     Arity = new ArgumentArity(2, 2)
            // };
            // var writeOption = new Option<string[]>(
            //     new[] { "--write", "-w" },
            //     "Write image file to physical drive.")
            // {
            //     AllowMultipleArgumentsPerToken = true,
            //     Arity = new ArgumentArity(2, 2)
            // };
            // var convertOption = new Option<string[]>(
            //     new[] { "--convert", "-c" },
            //     "Convert image file.")
            // {
            //     AllowMultipleArgumentsPerToken = true,
            //     Arity = new ArgumentArity(2, 2)
            // };
            // var verifyOption = new Option<string[]>(
            //     new[] { "--verify", "-v" },
            //     "Verify image file.")
            // {
            //     AllowMultipleArgumentsPerToken = true,
            //     Arity = new ArgumentArity(2, 2)
            // };
            // var blankOption = new Option<string>(
            //     new[] { "--blank", "-b" },
            //     "Create blank image file.")
            // {
            //     Arity = ArgumentArity.ExactlyOne
            // };
            // var optimizeOption = new Option<string>(
            //     new[] { "--optimize", "-o" },
            //     "Optimize image file.")
            // {
            //     Arity = ArgumentArity.ExactlyOne
            // };
            // var sizeOption = new Option<long>(
            //     new[] { "--size", "-s" },
            //     "Size of source image file or physical drive.");
            // var fakeOption = new Option<bool>(
            //     new[] { "--fake", "-f" },
            //     "Fake source paths (debug only).")
            // {
            //     IsHidden = true
            // };
            //
            // var pathOption = new Option<string>(
            //     new[] { "--path", "-p" },
            //     "Path to physical drive or image file.")
            // {
            //     IsRequired = true
            // };
            
            var rootCommand = CommandFactory.CreateRootCommand();
            return await rootCommand.InvokeAsync(args);
        }

        // static async Task<IEnumerable<IPhysicalDrive>> GetPhysicalDrives(Arguments arguments)
        // {
        //     if (arguments.Fake)
        //     {
        //         var drives = new List<FakePhysicalDrive>();
        //
        //         if (!string.IsNullOrWhiteSpace(arguments.SourcePath))
        //         {
        //             drives.Add(new FakePhysicalDrive(arguments.SourcePath, "Fake", "Fake",
        //                 arguments.Size ?? 1024 * 1024));
        //         }
        //
        //         if (!string.IsNullOrWhiteSpace(arguments.DestinationPath))
        //         {
        //             drives.Add(new FakePhysicalDrive(arguments.DestinationPath, "Fake", "Fake",
        //                 arguments.Size ?? 1024 * 1024));
        //         }
        //
        //         return drives;
        //     }
        //
        //     var physicalDriveManager = new PhysicalDriveManagerFactory(new NullLoggerFactory()).Create();
        //
        //     return (await physicalDriveManager.GetPhysicalDrives()).ToList();
        // }


        // static async Task Run(Arguments arguments)
        // {
        //     Log.Logger = new LoggerConfiguration()
        //         .Enrich.FromLogContext()
        //         .WriteTo.Console()
        //         .CreateLogger();
        //     
        //     var serviceProvider = new ServiceCollection()
        //         .AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true))
        //         .BuildServiceProvider();
        //
        //     var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        //
        //     // move to commands and let them check if is administrator is required
        //     // if (!isAdministrator)
        //     // {
        //     //     Console.WriteLine("Requires administrator rights!");
        //     // }
        //
        //     var commandHelper = new CommandHelper();
        //     var physicalDrives = IsAdministrator ? (await GetPhysicalDrives(arguments)).ToList() : new List<IPhysicalDrive>();
        //     var cancellationTokenSource = new CancellationTokenSource();
        //
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
        //         case Arguments.CommandEnum.Read:
        //             Console.WriteLine("Reading physical drive to image file");
        //
        //             GenericPresenter.PresentPaths(arguments);
        //
        //             var readCommand = new ReadCommand(loggerFactory.CreateLogger<ReadCommand>(), commandHelper, physicalDrives, arguments.SourcePath,
        //                 arguments.DestinationPath,
        //                 arguments.Size);
        //             readCommand.DataProcessed += (_, args) => { GenericPresenter.Present(args); };
        //             var readResult = await readCommand.Execute(cancellationTokenSource.Token);
        //             Console.WriteLine(readResult.IsSuccess ? "Done" : $"ERROR: Read failed, {readResult.Error}");
        //             break;
        //         case Arguments.CommandEnum.Convert:
        //             Console.WriteLine("Converting source image to destination image file");
        //
        //             GenericPresenter.PresentPaths(arguments);
        //
        //             var convertCommand = new ConvertCommand(loggerFactory.CreateLogger<ConvertCommand>(), commandHelper, arguments.SourcePath,
        //                 arguments.DestinationPath,
        //                 arguments.Size);
        //             convertCommand.DataProcessed += (_, args) => { GenericPresenter.Present(args); };
        //             var convertResult = await convertCommand.Execute(cancellationTokenSource.Token);
        //             Console.WriteLine(
        //                 convertResult.IsSuccess ? "Done" : $"ERROR: Convert failed, {convertResult.Error}");
        //             break;
        //         case Arguments.CommandEnum.Write:
        //             Console.WriteLine("Writing source image file to physical drive");
        //
        //             GenericPresenter.PresentPaths(arguments);
        //
        //             var writeCommand = new WriteCommand(loggerFactory.CreateLogger<WriteCommand>(), commandHelper, physicalDrives, arguments.SourcePath,
        //                 arguments.DestinationPath,
        //                 arguments.Size);
        //             writeCommand.DataProcessed += (_, args) => { GenericPresenter.Present(args); };
        //             var writeResult = await writeCommand.Execute(cancellationTokenSource.Token);
        //             Console.WriteLine(writeResult.IsSuccess ? "Done" : $"ERROR: Write failed, {writeResult.Error}");
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
        //         case Arguments.CommandEnum.Blank:
        //             Console.WriteLine("Creating blank image");
        //             Console.WriteLine($"Path: {arguments.SourcePath}");
        //             var blankCommand = new BlankCommand(loggerFactory.CreateLogger<BlankCommand>(), commandHelper, arguments.SourcePath, arguments.Size);
        //             var blankResult = await blankCommand.Execute(cancellationTokenSource.Token);
        //             Console.WriteLine(blankResult.IsSuccess ? "Done" : $"ERROR: Blank failed, {blankResult.Error}");
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