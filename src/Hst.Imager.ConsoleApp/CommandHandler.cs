﻿using Hst.Imager.Core.Commands.GptCommands;
using Hst.Imager.Core.Helpers;

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
    using Core.Commands.MbrCommands;
    using Core.Commands.RdbCommands;
    using Core.UaeMetadatas;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Presenters;
    using Serilog;

    public static class CommandHandler
    {
        private static readonly ServiceProvider ServiceProvider = new ServiceCollection()
            .AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true))
            .BuildServiceProvider();

        private static readonly IList<IoError> SrcIoErrors = new List<IoError>();
        private static readonly IList<IoError> DestIoErrors = new List<IoError>();

        private static ILogger<T> GetLogger<T>()
        {
            var loggerFactory = ServiceProvider.GetService<ILoggerFactory>();
            return loggerFactory.CreateLogger<T>();
        }

        private static ICommandHelper GetCommandHelper()
        {
            return new CommandHelper(GetLogger<CommandHelper>(), User.IsAdministrator);
        }

        private static async Task<IEnumerable<IPhysicalDrive>> GetPhysicalDrives()
        {
            if (!User.IsAdministrator)
            {
                return new List<IPhysicalDrive>();
            }

            var physicalDriveManager =
                new PhysicalDriveManagerFactory(ServiceProvider.GetService<ILoggerFactory>()).Create();
            return (await physicalDriveManager.GetPhysicalDrives(AppState.Instance.Settings.AllPhysicalDrives)).ToList();
        }

        private static readonly Regex IntRegex =
            new("^(\\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex HexRegex =
            new("^0x([\\da-f])+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static uint? ParseHexOrIntegerValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            
            if (HexRegex.IsMatch(value))
            {
                return System.Convert.ToUInt32(value, 16);
            }

            if (IntRegex.IsMatch(value) && uint.TryParse(value, out var uintValue))
            {
                return uintValue;
            }
            
            throw new ArgumentException("Invalid hex or integer value", nameof(value));
        }
        
        private static readonly Regex SizeUnitRegex =
            new("^(\\d+)([\\.]{1}\\d+)?(kb|mb|gb|%)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
                "tb" => new Size(sizeValue * (long)Math.Pow(1024, 4), Unit.Bytes),
                _ => throw new ArgumentException("Invalid size", nameof(size))
            };
        }

        private static async Task Execute(CommandBase command)
        {
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

        public static Task SettingsList()
        {
            Log.Logger.Information(SettingsPresenter.PresentSettings(AppState.Instance.Settings));

            return Task.CompletedTask;
        }

        public static async Task SettingsUpdate(bool? allPhysicalDrives, int? retries, bool? force, bool? verify, bool? skipUnusedSectors)
        {
            var settingsUpdated = false;
            
            if (allPhysicalDrives.HasValue)
            {
                AppState.Instance.Settings.AllPhysicalDrives = allPhysicalDrives.Value;
                settingsUpdated = true;
            }

            if (retries.HasValue)
            {
                AppState.Instance.Settings.Retries = retries.Value;
                settingsUpdated = true;
            }

            if (force.HasValue)
            {
                AppState.Instance.Settings.Force = force.Value;
                settingsUpdated = true;
            }

            if (verify.HasValue)
            {
                AppState.Instance.Settings.Verify = verify.Value;
                settingsUpdated = true;
            }

            if (skipUnusedSectors.HasValue)
            {
                AppState.Instance.Settings.SkipUnusedSectors = skipUnusedSectors.Value;
                settingsUpdated = true;
            }
            
            Log.Logger.Information(SettingsPresenter.PresentSettings(AppState.Instance.Settings));
            
            if (!settingsUpdated)
            {
                return;
            }

            await ApplicationDataHelper.WriteSettings(Core.Models.Constants.AppName, AppState.Instance.Settings);
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

        public static async Task Info(string path, bool showUnallocated)
        {
            var command = new InfoCommand(GetLogger<InfoCommand>(), GetCommandHelper(), await GetPhysicalDrives(), path);
            command.DiskInfoRead += (_, args) =>
            {
                Log.Logger.Information(InfoPresenter.PresentInfo(args.MediaInfo.DiskInfo, showUnallocated));
            };
            await Execute(command);
        }

        public static async Task List()
        {
            var command = new ListCommand(GetLogger<ListCommand>(), GetCommandHelper(), await GetPhysicalDrives());
            command.ListRead += (_, args) => { Log.Logger.Information(InfoPresenter.PresentInfo(args.MediaInfos)); };
            await Execute(command);
        }

        public static async Task Convert(string sourcePath, string destinationPath, string size, bool verify)
        {
            SrcIoErrors.Clear();
            DestIoErrors.Clear();
            var command = new ConvertCommand(GetLogger<ConvertCommand>(), GetCommandHelper(), sourcePath,
                destinationPath, ParseSize(size), verify);
            command.DataProcessed += WriteProcessMessage;
            command.SrcError += (_, args) => SrcIoErrors.Add(args.IoError);
            command.DestError += (_, args) => DestIoErrors.Add(args.IoError);
            await Execute(command);
            WriteIoErrors("Source", SrcIoErrors);
            WriteIoErrors("Destination", DestIoErrors);
        }

        public static async Task Optimize(string path, string size, PartitionTable partitionTable)
        {
            var command = new OptimizeCommand(GetLogger<OptimizeCommand>(), GetCommandHelper(), path, ParseSize(size),
                partitionTable);
            await Execute(command);
        }

        public static async Task Read(string sourcePath, string destinationPath, string size, int? retries, bool? verify,
            bool? force, long? start)
        {
            SrcIoErrors.Clear();
            DestIoErrors.Clear();
            var command = new ReadCommand(GetLogger<ReadCommand>(), GetCommandHelper(), await GetPhysicalDrives(),
                sourcePath,
                destinationPath,
                ParseSize(size),
                retries ?? AppState.Instance.Settings.Retries,
                verify ?? AppState.Instance.Settings.Verify,
                force ?? AppState.Instance.Settings.Force,
                start);
            command.DataProcessed += WriteProcessMessage;
            command.SrcError += (_, args) => SrcIoErrors.Add(args.IoError);
            command.DestError += (_, args) => DestIoErrors.Add(args.IoError);
            await Execute(command);
            WriteIoErrors("Source", SrcIoErrors);
            WriteIoErrors("Destination", DestIoErrors);

        }

        public static async Task Compare(string sourcePath, string destinationPath, long? srcStartOffset,
            long? destStartOffset, string size, bool? skipUnusedSectors, int? retries, bool? force)
        {
            SrcIoErrors.Clear();
            DestIoErrors.Clear();
            var command = new CompareCommand(GetLogger<CompareCommand>(), GetCommandHelper(), await GetPhysicalDrives(),
                sourcePath,
                srcStartOffset ?? 0,
                destinationPath,
                destStartOffset ?? 0,
                ParseSize(size),
                retries ?? AppState.Instance.Settings.Retries,
                force ?? AppState.Instance.Settings.Force,
                skipUnusedSectors ?? AppState.Instance.Settings.SkipUnusedSectors);
            command.DataProcessed += WriteProcessMessage;
            command.SrcError += (_, args) => SrcIoErrors.Add(args.IoError);
            command.DestError += (_, args) => DestIoErrors.Add(args.IoError);
            await Execute(command);
            WriteIoErrors("Source", SrcIoErrors);
            WriteIoErrors("Destination", DestIoErrors);
        }

        public static async Task Write(string sourcePath, string destinationPath, string size, int? retries, bool? verify,
            bool? force, bool? skipUnusedSectors, long? start)
        {
            SrcIoErrors.Clear();
            DestIoErrors.Clear();
            var command = new WriteCommand(GetLogger<WriteCommand>(), GetCommandHelper(), await GetPhysicalDrives(),
                sourcePath,
                destinationPath, ParseSize(size),
                retries ?? AppState.Instance.Settings.Retries,
                verify ?? AppState.Instance.Settings.Verify,
                force ?? AppState.Instance.Settings.Force,
                skipUnusedSectors ?? AppState.Instance.Settings.SkipUnusedSectors,
                start);
            command.DataProcessed += WriteProcessMessage;
            command.SrcError += (_, args) => SrcIoErrors.Add(args.IoError);
            command.DestError += (_, args) => DestIoErrors.Add(args.IoError);
            await Execute(command);
            WriteIoErrors("Source", SrcIoErrors);
            WriteIoErrors("Destination", DestIoErrors);
        }

        public static async Task Blank(string path, string size, bool compatibleSize)
        {
            await Execute(new BlankCommand(GetLogger<BlankCommand>(), GetCommandHelper(), path, ParseSize(size),
                compatibleSize));
        }

        public static async Task Format(string path, FormatType formatType, string fileSystem,
            string fileSystemPath, string size)
        {
            await Execute(new FormatCommand(GetLogger<FormatCommand>(), ServiceProvider.GetService<ILoggerFactory>(),
                GetCommandHelper(), await GetPhysicalDrives(), path,
                formatType, fileSystem, fileSystemPath,
                AppState.Instance.AppPath, ParseSize(size)));
        }

        public static async Task GptInfo(string path, bool showUnallocated)
        {
            var command = new GptInfoCommand(GetLogger<GptInfoCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path);
            command.GptInfoRead += (_, args) =>
            {
                Log.Logger.Information(GuidPartitionTablePresenter.Present(args.MediaInfo, showUnallocated));
            };
            await Execute(command);
        }

        public static async Task GptInit(string path)
        {
            await Execute(new GptInitCommand(GetLogger<GptInitCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path));
        }

        public static async Task GptPartAdd(string path, string type, string name, string size, long? startSector)
        {
            await Execute(new GptPartAddCommand(GetLogger<GptPartAddCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, type, name, ParseSize(size), startSector, null));
        }

        public static async Task GptPartDel(string path, int partitionNumber)
        {
            await Execute(new GptPartDelCommand(GetLogger<GptPartDelCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber));
        }

        public static async Task GptPartFormat(string path, int partitionNumber, GptPartType type, string name)
        {
            await Execute(new GptPartFormatCommand(GetLogger<GptPartFormatCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber, type, name));
        }

        public static async Task MbrInfo(string path, bool showUnallocated)
        {
            var command = new MbrInfoCommand(GetLogger<MbrInfoCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path);
            command.MbrInfoRead += (_, args) =>
            {
                Log.Logger.Information(MasterBootRecordPresenter.Present(args.MediaInfo, showUnallocated));
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
            await Execute(new MbrPartAddCommand(GetLogger<MbrPartAddCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, type, ParseSize(size), startSector, null, active));
        }

        public static async Task MbrPartDel(string path, int partitionNumber)
        {
            await Execute(new MbrPartDelCommand(GetLogger<MbrPartDelCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber));
        }

        public static async Task MbrPartFormat(string path, int partitionNumber, string name, string fileSystem)
        {
            await Execute(new MbrPartFormatCommand(GetLogger<MbrPartFormatCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber, name, fileSystem));
        }

        public static async Task MbrPartExport(string sourcePath, string partition, string destinationPath)
        {
            var command = new MbrPartExportCommand(GetLogger<MbrPartExportCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), sourcePath, partition, destinationPath);
            command.DataProcessed += WriteProcessMessage;
            await Execute(command);
        }

        public static async Task MbrPartImport(string sourcePath, string destinationPath, string partition)
        {
            var command = new MbrPartImportCommand(GetLogger<MbrPartImportCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), sourcePath, destinationPath, partition);
            command.DataProcessed += WriteProcessMessage;
            await Execute(command);
        }

        public static async Task MbrPartClone(string srcPath, int srcPartitionNumber, string destPath,
            int destPartitionNumber)
        {
            var command = new MbrPartCloneCommand(GetLogger<MbrPartCloneCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), srcPath, srcPartitionNumber, destPath, destPartitionNumber);
            command.DataProcessed += WriteProcessMessage;
            await Execute(command);
        }

        public static async Task RdbInfo(string path, bool showUnallocated)
        {
            var command = new RdbInfoCommand(GetLogger<RdbInfoCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path);
            command.RdbInfoRead += (_, args) =>
            {
                Log.Logger.Information(RigidDiskBlockPresenter.Present(args.MediaInfo, showUnallocated));
            };
            await Execute(command);
        }

        public static async Task RdbInit(string path, string size, string name, string chs, int rdbBlockLo)
        {
            await Execute(new RdbInitCommand(GetLogger<RdbInitCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, name, ParseSize(size), chs, rdbBlockLo));
        }

        public static async Task RdbResize(string path, string size)
        {
            await Execute(new RdbResizeCommand(GetLogger<RdbResizeCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, ParseSize(size)));
        }

        public static async Task RdbUpdate(string path, uint? flags, uint? hostId, string diskProduct,
            string diskRevision, string diskVendor)
        {
            await Execute(new RdbUpdateCommand(GetLogger<RdbUpdateCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, flags, hostId, diskProduct, diskRevision, diskVendor));
        }

        public static async Task RdbBackup(string path, string backupPath)
        {
            await Execute(new RdbBackupCommand(GetLogger<RdbBackupCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, backupPath));
        }

        public static async Task RdbRestore(string backupPath, string path)
        {
            await Execute(new RdbRestoreCommand(GetLogger<RdbRestoreCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), backupPath, path));
        }

        public static async Task RdbFsAdd(string path, string fileSystemPath, string dosType, string fileSystemName,
            int? version, int? revision)
        {
            await Execute(new RdbFsAddCommand(GetLogger<RdbFsAddCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, fileSystemPath, dosType, fileSystemName, version, revision));
        }

        public static async Task RdbFsDel(string path, int fileSystemNumber)
        {
            await Execute(new RdbFsDelCommand(GetLogger<RdbFsDelCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, fileSystemNumber));
        }

        public static async Task RdbFsImport(string path, string fileSystemPath, string dosType, string fileSystemName)
        {
            await Execute(new RdbFsImportCommand(GetLogger<RdbFsImportCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, fileSystemPath, dosType, fileSystemName, AppState.Instance.AppPath));
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

        public static async Task RdbPartAdd(string path, string name, string dosType, string size, uint? reserved,
            uint? preAlloc, uint? buffers, string maxTransfer, string mask, bool noMount, bool bootable, int? priority,
            int? fileSystemBlockSize)
        {
            await Execute(new RdbPartAddCommand(GetLogger<RdbPartAddCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, name, dosType, ParseSize(size), reserved, preAlloc, buffers,
                ParseHexOrIntegerValue(maxTransfer), ParseHexOrIntegerValue(mask), noMount, bootable, priority,
                fileSystemBlockSize));
        }

        public static async Task RdbPartUpdate(string path, int partitionNumber, string name, string dosType,
            int? reserved,
            int? preAlloc, int? buffers, string maxTransfer, string mask, bool? noMount, bool? bootable,
            int? bootPriority, int? fileSystemBlockSize)
        {
            await Execute(new RdbPartUpdateCommand(GetLogger<RdbPartUpdateCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber, name, dosType, reserved, preAlloc, buffers,
                ParseHexOrIntegerValue(maxTransfer), ParseHexOrIntegerValue(mask), noMount, bootable, bootPriority,
                fileSystemBlockSize));
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
            string name, string dosType, int fileSystemBlockSize, bool bootable)
        {
            var command = new RdbPartImportCommand(GetLogger<RdbPartImportCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), sourcePath, destinationPath, name, dosType, fileSystemBlockSize, bootable);
            command.DataProcessed += WriteProcessMessage;
            await Execute(command);
        }

        public static async Task RdbPartFormat(string path, int partitionNumber, string name, bool nonRdb, string chs, string dosType)
        {
            await Execute(new RdbPartFormatCommand(GetLogger<RdbPartFormatCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber, name, nonRdb, chs, dosType));
        }

        public static async Task RdbPartKill(string path, int partitionNumber, string hexBootBytes)
        {
            await Execute(new RdbPartKillCommand(GetLogger<RdbPartKillCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber, hexBootBytes));
        }

        public static async Task RdbPartMove(string path, int partitionNumber, uint startCylinder)
        {
            var command = new RdbPartMoveCommand(GetLogger<RdbPartMoveCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, partitionNumber, startCylinder);
            command.DataProcessed += WriteProcessMessage;
            await Execute(command);
        }

        public static async Task BlockRead(string path, string outputPath, int blockSize, bool used, long? start, long? end)
        {
            await Execute(new BlockReadCommand(GetLogger<BlockReadCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, outputPath, blockSize, used, start, end));
        }

        public static async Task BlockView(string path, int blockSize, long? start)
        {
            await Execute(new BlockViewCommand(GetLogger<BlockViewCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, blockSize, start));
        }
        
        public static async Task FsDir(string path, bool recursive, FormatEnum format)
        {
            var command = new FsDirCommand(GetLogger<FsDirCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, recursive);
            command.EntriesRead += (_, args) =>
            {
                Console.Write(EntriesPresenter.PresentEntries(args.EntriesInfo, format));
            };
            await Execute(command);
        }

        public static async Task FsCopy(string srcPath, string destPath, bool recursive, bool skipAttributes, bool quiet, UaeMetadata uaeMetadata)
        {
            var command = new FsCopyCommand(GetLogger<FsCopyCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), srcPath, destPath, recursive, skipAttributes, quiet, uaeMetadata);
            await Execute(command);
        }

        public static async Task ArcList(string path, bool recursive)
        {
            var command = new ArcListCommand(GetLogger<ArcListCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), path, recursive);
            command.EntriesRead += (_, args) =>
            {
                Console.Write(EntriesPresenter.PresentEntries(args.EntriesInfo, FormatEnum.Table));
            };
            await Execute(command);
        }

        public static async Task FsExtract(string srcPath, string destPath, bool recursive, bool skipAttributes, bool quiet, UaeMetadata uaeMetadata)
        {
            var command = new FsExtractCommand(GetLogger<FsExtractCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), srcPath, destPath, recursive, skipAttributes, quiet, uaeMetadata);
            await Execute(command);
        }
        
        public static async Task AdfCreate(string adfPath, bool format, string name, string dosType, bool recursive)
        {
            var command = new AdfCreateCommand(GetLogger<AdfCreateCommand>(), GetCommandHelper(),
                await GetPhysicalDrives(), adfPath, format, name, dosType, recursive);
            await Execute(command);
        }
        
        private static void WriteProcessMessage(object sender, DataProcessedEventArgs args)
        {
            var parts = new List<string>();

            if (!args.Indeterminate)
            {
                parts.Add($"{args.PercentComplete:0.0}%");
            }
            parts.Add($"[{args.BytesPerSecond.FormatBytes(format: "0.0")}/s]");
            parts.Add($"[{args.BytesProcessed.FormatBytes()}{(!args.Indeterminate ? $" / {args.BytesTotal.FormatBytes()}" : string.Empty)}]");
            parts.Add($"[{args.TimeElapsed.FormatElapsed()}{(!args.Indeterminate ? $" / {args.TimeTotal.FormatElapsed()}" : string.Empty)}]");
            parts.Add("   \r");

            var progressMessage = string.Join(" ", parts);
            Console.Write(args.PercentComplete >= 100
                ? string.Concat(new string(' ', progressMessage.Length + 3), "\r")
                : progressMessage);
        }
        
        private static void WriteIoErrors(string target, IList<IoError> ioErrors)
        {
            if (!ioErrors.Any())
            {
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"{ioErrors.Count} {target} error{(ioErrors.Count <= 1 ? string.Empty : "s")}:");
            foreach (var ioError in ioErrors)
            {
                Console.WriteLine(
                    $"Offset {ioError.Offset}, Length {ioError.Length}, Error = '{ioError.ErrorMessage}'");
            }
        }
    }
}