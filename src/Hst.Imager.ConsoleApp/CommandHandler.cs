namespace Hst.Imager.ConsoleApp
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Parsing;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Core;
    using Core.Commands;
    using Core.Extensions;
    using Core.Models;
    using Hst.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Presenters;
    using Serilog;

    public static class CommandHandler
    {
        private static readonly ServiceProvider ServiceProvider = new ServiceCollection()
            .AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true))
            .BuildServiceProvider();

        private static ILogger<T> GetLogger<T>()
        {
            var loggerFactory = ServiceProvider.GetService<ILoggerFactory>();
            return loggerFactory.CreateLogger<T>();
        }

        private static ICommandHelper GetCommandHelper()
        {
            return new CommandHelper(User.IsAdministrator);
        }

        private static async Task<IEnumerable<IPhysicalDrive>> GetPhysicalDrives()
        {
            if (!User.IsAdministrator)
            {
                return new List<IPhysicalDrive>();
            }

            var physicalDriveManager =
                new PhysicalDriveManagerFactory(ServiceProvider.GetService<ILoggerFactory>()).Create();

            return (await physicalDriveManager.GetPhysicalDrives()).ToList();
        }

        private static readonly Regex SizeUnitRegex =
            new("(\\d+)([\\.]{1}\\d+)?(kb|mb|gb|%)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Size ParseSize(string size)
        {
            if (string.IsNullOrWhiteSpace(size) || size.Equals("*"))
            {
                return new Size();
            }

            var sizeUnitMatch = SizeUnitRegex.Match(size);

            if (!sizeUnitMatch.Success)
            {
                throw new ArgumentException("Invalid size", nameof(size));
            }

            if (!double.TryParse(string.Concat(sizeUnitMatch.Groups[1].Value, sizeUnitMatch.Groups[2].Value),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out var sizeValue))
            {
                throw new ArgumentException("Invalid size", nameof(size));
            }

            return sizeUnitMatch.Groups[3].Value.ToLower() switch
            {
                "" => new Size(sizeValue, Unit.Bytes),
                "%" => new Size(sizeValue, Unit.Percent),
                "kb" => new Size(sizeValue * 1024, Unit.Bytes),
                "mb" => new Size(sizeValue * (long)Math.Pow(1024, 2), Unit.Bytes),
                "gb" => new Size(sizeValue * (long)Math.Pow(1024, 3), Unit.Bytes),
                _ => throw new ArgumentException("Invalid size", nameof(size))
            };
        }

        private static async Task Execute(CommandBase command, bool requiresAdministrator = false)
        {
            if (requiresAdministrator && !User.IsAdministrator)
            {
                Log.Logger.Error($"Command requires administrator privileges");
                Environment.Exit(1);
            }

            command.DebugMessage += (_, progressMessage) => { Log.Logger.Debug(progressMessage); };
            command.InformationMessage += (_, progressMessage) => { Log.Logger.Information(progressMessage); };

            var cancellationTokenSource = new CancellationTokenSource();
            Result result = null;
            try
            {
                result = await command.Execute(cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                Log.Logger.Error(e, $"Failed to execute command '{command.GetType()}'");
                Environment.Exit(1);
            }

            if (result.IsFaulted)
            {
                Log.Logger.Error($"{result.Error}");
                Environment.Exit(1);
            }

            Log.Logger.Information("Done");
        }

        public static async Task Script(string path)
        {
            var lines = await File.ReadAllLinesAsync(path);
            var scriptLines = lines.Where(x => !string.IsNullOrWhiteSpace(x) && !x.Trim().StartsWith("#"))
                .Select(x => CommandLineStringSplitter.Instance.Split(x)).ToList();

            var rootCommand = CommandFactory.CreateRootCommand();
            foreach (var scriptLine in scriptLines)
            {
                var args = scriptLine.ToArray();

                Log.Logger.Information($"[CMD] {string.Join(" ", args)}");

                if (await rootCommand.InvokeAsync(args) != 0)
                {
                    Environment.Exit(1);
                }
            }
        }

        public static async Task Info(string path)
        {
            var command = new InfoCommand(GetLogger<InfoCommand>(), GetCommandHelper(), await GetPhysicalDrives(),
                path);
            command.DiskInfoRead += (sender, args) =>
            {
                Log.Logger.Information(InfoPresenter.PresentInfo(args.DiskInfo));
            };
            await Execute(command);
        }

        public static async Task List()
        {
            var command = new ListCommand(GetLogger<ListCommand>(), GetCommandHelper(), await GetPhysicalDrives());
            command.ListRead += (_, args) => { Log.Logger.Information(InfoPresenter.PresentInfo(args.MediaInfos)); };
            await Execute(command, true);
        }

        public static async Task Convert(string sourcePath, string destinationPath, string size)
        {
            var command = new ConvertCommand(GetLogger<ConvertCommand>(), GetCommandHelper(), sourcePath,
                destinationPath, ParseSize(size));
            command.DataProcessed += WriteProcessMessage;
            await Execute(command);
        }

        public static async Task Optimize(string path, string size, bool rdb)
        {
            var command = new OptimizeCommand(GetLogger<OptimizeCommand>(), GetCommandHelper(), path, ParseSize(size), rdb);
            await Execute(command);
        }

        public static async Task Read(string sourcePath, string destinationPath, string size)
        {
            var command = new ReadCommand(GetLogger<ReadCommand>(), GetCommandHelper(), await GetPhysicalDrives(),
                sourcePath,
                destinationPath, ParseSize(size));
            command.DataProcessed += WriteProcessMessage;
            await Execute(command);
        }

        public static async Task Verify(string sourcePath, string destinationPath, string size)
        {
            var command = new VerifyCommand(GetLogger<VerifyCommand>(), GetCommandHelper(), await GetPhysicalDrives(),
                sourcePath,
                destinationPath, ParseSize(size));
            command.DataProcessed += WriteProcessMessage;
            await Execute(command);
        }

        public static async Task Write(string sourcePath, string destinationPath, string size)
        {
            var command = new WriteCommand(GetLogger<WriteCommand>(), GetCommandHelper(), await GetPhysicalDrives(),
                sourcePath,
                destinationPath, ParseSize(size));
            command.DataProcessed += WriteProcessMessage;
            await Execute(command);
        }

        public static async Task Blank(string path, string size, bool compatibleSize)
        {
            await Execute(new BlankCommand(GetLogger<BlankCommand>(), GetCommandHelper(), path, ParseSize(size),
                compatibleSize));
        }

        public static async Task MbrInfo(string path)
        {
            var command = new MbrInfoCommand(GetLogger<MbrInfoCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path);
            command.MbrInfoRead += (_, args) =>
            {
                Log.Logger.Information(MasterBootRecordPresenter.Present(args.MbrInfo));
            };
            await Execute(command);
        }

        public static async Task MbrInit(string path)
        {
            await Execute(new MbrInitCommand(GetLogger<MbrInitCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path));
        }

        public static async Task MbrPartAdd(string path, string type, string size, long? startSector, bool active)
        {
            await Execute(new MbrPartAddCommand(GetLogger<RdbInitCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, type, ParseSize(size), startSector, null, active));
        }

        public static async Task MbrPartDel(string path, int partitionNumber)
        {
            await Execute(new MbrPartDelCommand(GetLogger<MbrPartDelCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber));
        }

        public static async Task MbrPartFormat(string path, int partitionNumber, string name)
        {
            await Execute(new MbrPartFormatCommand(GetLogger<RdbInitCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber, name));
        }

        public static async Task RdbInfo(string path)
        {
            var command = new RdbInfoCommand(GetLogger<RdbInfoCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path);
            command.RdbInfoRead += (_, args) =>
            {
                Log.Logger.Information(RigidDiskBlockPresenter.Present(args.RdbInfo));
            };
            await Execute(command);
        }

        public static async Task RdbInit(string path, string size, string name, int rdbBlockLo)
        {
            await Execute(new RdbInitCommand(GetLogger<RdbInitCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, name, ParseSize(size), rdbBlockLo));
        }

        public static async Task RdbFsAdd(string path, string fileSystemPath, string dosType, string fileSystemName)
        {
            await Execute(new RdbFsAddCommand(GetLogger<RdbFsAddCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, fileSystemPath, dosType, fileSystemName));
        }

        public static async Task RdbFsDel(string path, int fileSystemNumber)
        {
            await Execute(new RdbFsDelCommand(GetLogger<RdbFsDelCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, fileSystemNumber));
        }

        public static async Task RdbFsImport(string path, string fileSystemPath, string dosType, string fileSystemName)
        {
            await Execute(new RdbFsImportCommand(GetLogger<RdbFsImportCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, fileSystemPath, dosType, fileSystemName));
        }

        public static async Task RdbFsExport(string path, int fileSystemNumber, string fileSystemPath)
        {
            await Execute(new RdbFsExportCommand(GetLogger<RdbFsExportCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, fileSystemNumber, fileSystemPath));
        }

        public static async Task RdbFsUpdate(string path, int fileSystemNumber, string dosType, string fileSystemName,
            string fileSystemPath)
        {
            await Execute(new RdbFsUpdateCommand(GetLogger<RdbFsUpdateCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, fileSystemNumber, dosType, fileSystemName, fileSystemPath));
        }

        public static async Task RdbPartAdd(string path, string name, string dosType, string size, int reserved,
            int preAlloc, int buffers, int maxTransfer, bool noMount, bool bootable, int priority, int blockSize)
        {
            await Execute(new RdbPartAddCommand(GetLogger<RdbPartAddCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, name, dosType, ParseSize(size), reserved, preAlloc, buffers,
                maxTransfer, noMount, bootable, priority, blockSize));
        }

        public static async Task RdbPartUpdate(string path, int partitionNumber, string name, string dosType,
            int? reserved,
            int? preAlloc, int? buffers, uint? maxTransfer, uint? mask, bool? noMount, bool? bootable,
            int? bootPriority, int? blockSize)
        {
            await Execute(new RdbPartUpdateCommand(GetLogger<RdbPartUpdateCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber, name, dosType, reserved, preAlloc, buffers,
                maxTransfer, mask, noMount, bootable, bootPriority, blockSize));
        }

        public static async Task RdbPartDel(string path, int partitionNumber)
        {
            await Execute(new RdbPartDelCommand(GetLogger<RdbInitCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber));
        }

        public static async Task RdbPartCopy(string sourcePath, int partitionNumber, string destinationPath,
            string name)
        {
            var command = new RdbPartCopyCommand(GetLogger<RdbPartCopyCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), sourcePath, partitionNumber, destinationPath, name);
            command.DataProcessed += WriteProcessMessage;
            await Execute(command);
        }

        public static async Task RdbPartExport(string sourcePath, int partitionNumber, string destinationPath)
        {
            var command = new RdbPartExportCommand(GetLogger<RdbPartExportCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), sourcePath, partitionNumber, destinationPath);
            command.DataProcessed += WriteProcessMessage;
            await Execute(command);
        }

        public static async Task RdbPartImport(string sourcePath, string destinationPath,
            string name, string dosType, bool bootable)
        {
            var command = new RdbPartImportCommand(GetLogger<RdbPartImportCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), sourcePath, destinationPath, name, dosType, bootable);
            command.DataProcessed += WriteProcessMessage;
            await Execute(command);
        }

        public static async Task RdbPartFormat(string path, int partitionNumber, string name)
        {
            await Execute(new RdbPartFormatCommand(GetLogger<RdbInitCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber, name));
        }

        public static async Task RdbPartKill(string path, int partitionNumber, string hexBootBytes)
        {
            await Execute(new RdbPartKillCommand(GetLogger<RdbPartKillCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber, hexBootBytes));
        }

        public static async Task BlockRead(string path, string outputPath, int blockSize, bool used, long? start, long? end)
        {
            await Execute(new BlockReadCommand(GetLogger<BlockReadCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, outputPath, blockSize, used, start, end));
        }

        private static void WriteProcessMessage(object sender, DataProcessedEventArgs args)
        {
            var progressMessage =
                $"{args.PercentComplete:0.0}% [{args.BytesPerSecond.FormatBytes(format: "0.0")}/s] [{args.BytesProcessed.FormatBytes()} / {args.BytesTotal.FormatBytes()}] [{args.TimeElapsed.FormatElapsed()} / {args.TimeTotal.FormatElapsed()}]   \r";
            Console.Write(args.PercentComplete >= 100
                ? string.Concat(new string(' ', progressMessage.Length + 3), "\r")
                : progressMessage);
        }
    }
}