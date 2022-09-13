namespace Hst.Imager.ConsoleApp
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Parsing;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using HstWbInstaller.Imager.Core;
    using HstWbInstaller.Imager.Core.Commands;
    using HstWbInstaller.Imager.Core.Models;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Presenters;
    using Serilog;

    public static class CommandHandler
    {
        private static ILogger<T> GetLogger<T>()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
            
            var serviceProvider = new ServiceCollection()
                .AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true))
                .BuildServiceProvider();

            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            return loggerFactory.CreateLogger<T>();
        }

        private static async Task<IEnumerable<IPhysicalDrive>> GetPhysicalDrives()
        {
            if (!User.IsAdministrator)
            {
                return new List<IPhysicalDrive>();
            }
            
            var physicalDriveManager = new PhysicalDriveManagerFactory(new NullLoggerFactory()).Create();

            return (await physicalDriveManager.GetPhysicalDrives()).ToList();
        }

        private static readonly Regex SizeUnitRegex =
            new("(\\d+)(kb|mb|gb|%)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
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
            
            if (!long.TryParse(sizeUnitMatch.Groups[1].Value, out var sizeValue))
            {
                throw new ArgumentException("Invalid size", nameof(size));
            }

            return sizeUnitMatch.Groups[2].Value.ToLower() switch
            {
                "" => new Size(sizeValue, Unit.Bytes),
                "%" => new Size(sizeValue, Unit.Percent),
                "kb" => new Size(sizeValue * 1024, Unit.Bytes),
                "mb" => new Size(sizeValue * (long)Math.Pow(1024, 2), Unit.Bytes),
                "gb" => new Size(sizeValue * (long)Math.Pow(1024, 3), Unit.Bytes),
                _ => throw new ArgumentException("Invalid size", nameof(size))
            };
        }

        private static async Task Execute(CommandBase command)
        {
            command.ProgressMessage += (_, progressMessage) =>
            {
                Console.WriteLine(progressMessage);
            };
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await command.Execute(cancellationTokenSource.Token);

            if (result.IsFaulted)
            {
                Log.Logger.Error($"ERROR: {result.Error}");
                Environment.Exit(1);
            }
            
            Console.WriteLine("Done");
        }

        public static async Task Script(string path)
        {
            var scriptLines = new List<IEnumerable<string>>();
            
            var lines = await File.ReadAllLinesAsync(path);
            foreach (var line in lines)
            {
                scriptLines.Add(CommandLineStringSplitter.Instance.Split(line));
            }

            var rootCommand = CommandFactory.CreateRootCommand();
            foreach (var scriptLine in scriptLines)
            {
                if (await rootCommand.InvokeAsync(scriptLine.ToArray()) != 0)
                {
                    Environment.Exit(1);
                }
            }
        }
        
        public static async Task Blank(string path, string size, bool compatibleSize)
        {
            await Execute(new BlankCommand(GetLogger<BlankCommand>(), new CommandHelper(), path, ParseSize(size), compatibleSize));
        }

        public static async Task RdbInfo(string path)
        {
            var command = new RdbInfoCommand(GetLogger<RdbInitCommand>(), new CommandHelper(),
                await GetPhysicalDrives(), path);
            command.RdbInfoRead += (_, args) =>
            {
                RigidDiskBlockPresenter.Present(args.RdbInfo.RigidDiskBlock);
            };
            await Execute(command);
        }
        
        public static async Task RdbInit(string path, string name, string size, int rdbBlockLo)
        {
            await Execute(new RdbInitCommand(GetLogger<RdbInitCommand>(), new CommandHelper(),
                await GetPhysicalDrives(), path, name, ParseSize(size), rdbBlockLo));
        }
        
        public static async Task RdbFsAdd(string path, string fileSystemPath, string dosType, string fileSystemName)
        {
            await Execute(new RdbFsAddCommand(GetLogger<RdbInitCommand>(), new CommandHelper(),
                await GetPhysicalDrives(), path, fileSystemPath, dosType, fileSystemName));
        }
        
        public static async Task RdbFsDel(string path, int fileSystemNumber)
        {
            await Execute(new RdbFsDelCommand(GetLogger<RdbInitCommand>(), new CommandHelper(),
                await GetPhysicalDrives(), path, fileSystemNumber));
        }
        
        public static async Task RdbPartAdd(string path, string name, string dosType, string size, bool autoMount, bool bootable, int priority, int blockSize)
        {
            await Execute(new RdbPartAddCommand(GetLogger<RdbInitCommand>(), new CommandHelper(),
                await GetPhysicalDrives(), path, name, dosType, ParseSize(size), autoMount, bootable, priority, blockSize));
        }

        public static async Task RdbPartDel(string path, int partitionNumber)
        {
            await Execute(new RdbPartDelCommand(GetLogger<RdbInitCommand>(), new CommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber));
        }
        
        public static async Task RdbPartFormat(string path, int partitionNumber, string name)
        {
            await Execute(new RdbPartFormatCommand(GetLogger<RdbInitCommand>(), new CommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber, name));
        }
    }
}