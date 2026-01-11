using Hst.Imager.Core.Commands.FsCommands;
using Hst.Imager.Core.Commands.GptCommands;
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

        private static ICommandHelper GetCommandHelper(bool useCache = false)
        {
            var commandHelper = new CommandHelper(GetLogger<CommandHelper>(), User.IsAdministrator,
                AppState.Instance.Settings.UseCache && useCache, AppState.Instance.Settings.CacheType);

            commandHelper.DebugMessage += (_, message) => { Log.Logger.Debug(message); };
            commandHelper.WarningMessage += (_, message) => { Log.Logger.Warning(message); };
            commandHelper.InformationMessage += (_, message) => { Log.Logger.Information(message); };
            commandHelper.DataProcessed += async (sender, args) => await WriteProcessMessage(sender, args);
            
            return commandHelper;
        }

        private static async Task<IEnumerable<IPhysicalDrive>> GetPhysicalDrives()
        {
            if (!User.IsAdministrator)
            {
                return new List<IPhysicalDrive>();
            }

            var physicalDriveManager = new PhysicalDriveManagerFactory(ServiceProvider.GetService<ILoggerFactory>())
                .Create();
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
                return Convert.ToUInt32(value, 16);
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
            command.DebugMessage += (_, message) => { Log.Logger.Debug(message); };
            command.WarningMessage += (_, message) => { Log.Logger.Warning(message); };
            command.InformationMessage += (_, message) => { Log.Logger.Information(message); };
            command.DataProcessed += async (sender, args) => await WriteProcessMessage(sender, args);

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

        public static async Task SettingsUpdate(bool? allPhysicalDrives, int? retries, bool? force, bool? verify,
            bool? skipUnusedSectors, bool? useCache, CacheType? cacheType)
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

            if (useCache.HasValue)
            {
                AppState.Instance.Settings.UseCache = useCache.Value;
                settingsUpdated = true;
            }

            if (cacheType.HasValue)
            {
                AppState.Instance.Settings.CacheType = cacheType.Value;
                settingsUpdated = true;
            }

            Log.Logger.Information(SettingsPresenter.PresentSettings(AppState.Instance.Settings));
            
            if (!settingsUpdated)
            {
                return;
            }

            var appDataPath = ApplicationDataHelper.GetApplicationDataDir(Core.Models.Constants.AppName);
            await ApplicationDataHelper.WriteSettings(appDataPath, Core.Models.Constants.AppName, AppState.Instance.Settings);
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
            using var commandHelper = GetCommandHelper();
            var command = new InfoCommand(GetLogger<InfoCommand>(), commandHelper, await GetPhysicalDrives(), path, 
                false);
            command.DiskInfoRead += (_, args) =>
            {
                Log.Logger.Information(InfoPresenter.PresentInfo(args.MediaInfo.DiskInfo, showUnallocated));
            };
            await Execute(command);
        }

        public static async Task List()
        {
            using var commandHelper = GetCommandHelper();
            var command = new ListCommand(GetLogger<ListCommand>(), commandHelper, await GetPhysicalDrives());
            command.ListRead += (_, args) => { Log.Logger.Information(InfoPresenter.PresentInfo(args.MediaInfos)); };
            await Execute(command);
        }

        public static async Task Transfer(string sourcePath, string destinationPath, string size, bool verify,
            long? srcStart, long? destStart)
        {
            SrcIoErrors.Clear();
            DestIoErrors.Clear();
            using var commandHelper = GetCommandHelper();
            var command = new TransferCommand(commandHelper, sourcePath,
                destinationPath, ParseSize(size), verify, srcStart, destStart);
            command.SrcError += (_, args) => SrcIoErrors.Add(args.IoError);
            command.DestError += (_, args) => DestIoErrors.Add(args.IoError);
            await Execute(command);
            WriteIoErrors("Source", SrcIoErrors);
            WriteIoErrors("Destination", DestIoErrors);
        }

        public static async Task Optimize(string path, string size, PartitionTable? partitionTable)
        {
            using var commandHelper = GetCommandHelper();
            var command = new OptimizeCommand(GetLogger<OptimizeCommand>(), commandHelper, path, ParseSize(size),
                partitionTable);
            await Execute(command);
        }

        public static async Task Read(string sourcePath, string destinationPath, string size, int? retries, bool? verify,
            bool? force, long? start)
        {
            SrcIoErrors.Clear();
            DestIoErrors.Clear();
            using var commandHelper = GetCommandHelper();
            var command = new ReadCommand(GetLogger<ReadCommand>(), commandHelper, await GetPhysicalDrives(),
                sourcePath,
                destinationPath,
                ParseSize(size),
                retries ?? AppState.Instance.Settings.Retries,
                verify ?? AppState.Instance.Settings.Verify,
                force ?? AppState.Instance.Settings.Force,
                start);
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
            using var commandHelper = GetCommandHelper();
            var command = new CompareCommand(GetLogger<CompareCommand>(), commandHelper, await GetPhysicalDrives(),
                sourcePath,
                srcStartOffset ?? 0,
                destinationPath,
                destStartOffset ?? 0,
                ParseSize(size),
                retries ?? AppState.Instance.Settings.Retries,
                force ?? AppState.Instance.Settings.Force,
                skipUnusedSectors ?? AppState.Instance.Settings.SkipUnusedSectors);
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
            using var commandHelper = GetCommandHelper();
            var command = new WriteCommand(GetLogger<WriteCommand>(), commandHelper, await GetPhysicalDrives(),
                sourcePath,
                destinationPath, ParseSize(size),
                retries ?? AppState.Instance.Settings.Retries,
                verify ?? AppState.Instance.Settings.Verify,
                force ?? AppState.Instance.Settings.Force,
                skipUnusedSectors ?? AppState.Instance.Settings.SkipUnusedSectors,
                start);
            command.SrcError += (_, args) => SrcIoErrors.Add(args.IoError);
            command.DestError += (_, args) => DestIoErrors.Add(args.IoError);
            await Execute(command);
            WriteIoErrors("Source", SrcIoErrors);
            WriteIoErrors("Destination", DestIoErrors);
        }

        public static async Task Blank(string path, string size, bool compatibleSize)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new BlankCommand(GetLogger<BlankCommand>(), commandHelper, path, ParseSize(size),
                compatibleSize));
        }

        public static async Task Format(string path, FormatType formatType, string fileSystem,
            string fileSystemPath, string size, string maxPartitionSize, bool useExperimental,
            bool kickstart31)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new FormatCommand(GetLogger<FormatCommand>(), 
                ServiceProvider.GetService<ILoggerFactory>(),
                commandHelper, await GetPhysicalDrives(), path,
                formatType, fileSystem, fileSystemPath,
                AppState.Instance.AppPath, ParseSize(size), ParseSize(maxPartitionSize), useExperimental,
                kickstart31));
        }

        public static async Task GptInfo(string path, bool showUnallocated)
        {
            using var commandHelper = GetCommandHelper();
            var command = new GptInfoCommand(GetLogger<GptInfoCommand>(), commandHelper,
                await GetPhysicalDrives(), path);
            command.GptInfoRead += (_, args) =>
            {
                Log.Logger.Information(GuidPartitionTablePresenter.Present(args.MediaInfo, showUnallocated));
            };
            await Execute(command);
        }

        public static async Task GptInit(string path)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new GptInitCommand(GetLogger<GptInitCommand>(), commandHelper,
                await GetPhysicalDrives(), path));
        }

        public static async Task GptPartAdd(string path, string type, string name, string size, long? startSector)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new GptPartAddCommand(GetLogger<GptPartAddCommand>(), commandHelper,
                await GetPhysicalDrives(), path, type, name, ParseSize(size), startSector, null));
        }

        public static async Task GptPartDel(string path, int partitionNumber)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new GptPartDelCommand(GetLogger<GptPartDelCommand>(), commandHelper,
                await GetPhysicalDrives(), path, partitionNumber));
        }

        public static async Task GptPartFormat(string path, int partitionNumber, GptPartType type, string name)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new GptPartFormatCommand(GetLogger<GptPartFormatCommand>(), commandHelper,
                await GetPhysicalDrives(), path, partitionNumber, type, name));
        }

        public static async Task MbrInfo(string path, bool showUnallocated)
        {
            using var commandHelper = GetCommandHelper();
            var command = new MbrInfoCommand(GetLogger<MbrInfoCommand>(), commandHelper,
                await GetPhysicalDrives(), path);
            command.MbrInfoRead += (_, args) =>
            {
                Log.Logger.Information(MasterBootRecordPresenter.Present(args.MediaInfo, showUnallocated));
            };
            await Execute(command);
        }

        public static async Task MbrInit(string path)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new MbrInitCommand(GetLogger<MbrInitCommand>(), commandHelper,
                await GetPhysicalDrives(), path));
        }

        public static async Task MbrPartAdd(string path, string type, string size, long? startSector, bool active)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new MbrPartAddCommand(GetLogger<MbrPartAddCommand>(), commandHelper,
                await GetPhysicalDrives(), path, type, ParseSize(size), startSector, null, active));
        }

        public static async Task MbrPartDel(string path, int partitionNumber)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new MbrPartDelCommand(GetLogger<MbrPartDelCommand>(), commandHelper,
                await GetPhysicalDrives(), path, partitionNumber));
        }

        public static async Task MbrPartFormat(string path, int partitionNumber, string name, string fileSystem)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new MbrPartFormatCommand(GetLogger<MbrPartFormatCommand>(), commandHelper,
                await GetPhysicalDrives(), path, partitionNumber, name, fileSystem));
        }

        public static async Task MbrPartExport(string sourcePath, string partition, string destinationPath)
        {
            using var commandHelper = GetCommandHelper();
            var command = new MbrPartExportCommand(GetLogger<MbrPartExportCommand>(), commandHelper,
                await GetPhysicalDrives(), sourcePath, partition, destinationPath);
            await Execute(command);
        }

        public static async Task MbrPartImport(string sourcePath, string destinationPath, string partition)
        {
            using var commandHelper = GetCommandHelper();
            var command = new MbrPartImportCommand(GetLogger<MbrPartImportCommand>(), commandHelper,
                await GetPhysicalDrives(), sourcePath, destinationPath, partition);
            await Execute(command);
        }

        public static async Task MbrPartClone(string srcPath, int srcPartitionNumber, string destPath,
            int destPartitionNumber)
        {
            using var commandHelper = GetCommandHelper();
            var command = new MbrPartCloneCommand(GetLogger<MbrPartCloneCommand>(), commandHelper,
                await GetPhysicalDrives(), srcPath, srcPartitionNumber, destPath, destPartitionNumber);
            await Execute(command);
        }

        public static async Task RdbInfo(string path, bool showUnallocated)
        {
            using var commandHelper = GetCommandHelper();
            var command = new RdbInfoCommand(GetLogger<RdbInfoCommand>(), commandHelper,
                await GetPhysicalDrives(), path);
            command.RdbInfoRead += (_, args) =>
            {
                Log.Logger.Information(RigidDiskBlockPresenter.Present(args.MediaInfo, showUnallocated));
            };
            await Execute(command);
        }

        public static async Task RdbInit(string path, string size, string name, string chs, int rdbBlockLo)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbInitCommand(GetLogger<RdbInitCommand>(), commandHelper,
                await GetPhysicalDrives(), path, name, ParseSize(size), chs, rdbBlockLo));
        }

        public static async Task RdbResize(string path, string size)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbResizeCommand(GetLogger<RdbResizeCommand>(), commandHelper,
                await GetPhysicalDrives(), path, ParseSize(size)));
        }

        public static async Task RdbUpdate(string path, uint? flags, uint? hostId, string diskProduct,
            string diskRevision, string diskVendor)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbUpdateCommand(GetLogger<RdbUpdateCommand>(), commandHelper,
                await GetPhysicalDrives(), path, flags, hostId, diskProduct, diskRevision, diskVendor));
        }

        public static async Task RdbBackup(string path, string backupPath)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbBackupCommand(GetLogger<RdbBackupCommand>(), commandHelper,
                await GetPhysicalDrives(), path, backupPath));
        }

        public static async Task RdbRestore(string backupPath, string path)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbRestoreCommand(GetLogger<RdbRestoreCommand>(), commandHelper,
                await GetPhysicalDrives(), backupPath, path));
        }

        public static async Task RdbFsAdd(string path, string fileSystemPath, string dosType, string fileSystemName,
            int? version, int? revision)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbFsAddCommand(GetLogger<RdbFsAddCommand>(), commandHelper,
                await GetPhysicalDrives(), path, fileSystemPath, dosType, fileSystemName, version, revision));
        }

        public static async Task RdbFsDel(string path, int fileSystemNumber)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbFsDelCommand(GetLogger<RdbFsDelCommand>(), commandHelper,
                await GetPhysicalDrives(), path, fileSystemNumber));
        }

        public static async Task RdbFsImport(string path, string fileSystemPath, string dosType, string fileSystemName)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbFsImportCommand(GetLogger<RdbFsImportCommand>(), commandHelper,
                await GetPhysicalDrives(), path, fileSystemPath, dosType, fileSystemName, AppState.Instance.AppPath));
        }

        public static async Task RdbFsExport(string path, int fileSystemNumber, string fileSystemPath)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbFsExportCommand(GetLogger<RdbFsExportCommand>(), commandHelper,
                await GetPhysicalDrives(), path, fileSystemNumber, fileSystemPath));
        }

        public static async Task RdbFsUpdate(string path, int fileSystemNumber, string dosType, string fileSystemName,
            string fileSystemPath)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbFsUpdateCommand(GetLogger<RdbFsUpdateCommand>(), commandHelper,
                await GetPhysicalDrives(), path, fileSystemNumber, dosType, fileSystemName, fileSystemPath));
        }

        public static async Task RdbPartAdd(string path, string name, string dosType, string size, uint? reserved,
            uint? preAlloc, uint? buffers, string maxTransfer, string mask, bool noMount, bool bootable, int? priority,
            int? fileSystemBlockSize, bool useExperimental)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbPartAddCommand(GetLogger<RdbPartAddCommand>(), commandHelper,
                await GetPhysicalDrives(), path, name, dosType, ParseSize(size), reserved, preAlloc, buffers,
                ParseHexOrIntegerValue(maxTransfer), ParseHexOrIntegerValue(mask), noMount, bootable, priority,
                fileSystemBlockSize, useExperimental));
        }

        public static async Task RdbPartUpdate(string path, int partitionNumber, string name, string dosType,
            int? reserved,
            int? preAlloc, int? buffers, string maxTransfer, string mask, bool? noMount, bool? bootable,
            int? bootPriority, int? fileSystemBlockSize)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbPartUpdateCommand(GetLogger<RdbPartUpdateCommand>(), commandHelper,
                await GetPhysicalDrives(), path, partitionNumber, name, dosType, reserved, preAlloc, buffers,
                ParseHexOrIntegerValue(maxTransfer), ParseHexOrIntegerValue(mask), noMount, bootable, bootPriority,
                fileSystemBlockSize));
        }

        public static async Task RdbPartDel(string path, int partitionNumber)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbPartDelCommand(GetLogger<RdbInitCommand>(), commandHelper,
                await GetPhysicalDrives(), path, partitionNumber));
        }

        public static async Task RdbPartCopy(string sourcePath, int partitionNumber, string destinationPath,
            string name, string dosType)
        {
            using var commandHelper = GetCommandHelper();
            var command = new RdbPartCopyCommand(GetLogger<RdbPartCopyCommand>(), commandHelper,
                await GetPhysicalDrives(), sourcePath, partitionNumber, destinationPath, name, dosType);
            await Execute(command);
        }

        public static async Task RdbPartExport(string sourcePath, int partitionNumber, string destinationPath)
        {
            using var commandHelper = GetCommandHelper();
            var command = new RdbPartExportCommand(GetLogger<RdbPartExportCommand>(), commandHelper,
                await GetPhysicalDrives(), sourcePath, partitionNumber, destinationPath);
            await Execute(command);
        }

        public static async Task RdbPartImport(string sourcePath, string destinationPath,
            string name, string dosType, int fileSystemBlockSize, bool bootable)
        {
            using var commandHelper = GetCommandHelper();
            var command = new RdbPartImportCommand(GetLogger<RdbPartImportCommand>(), commandHelper,
                await GetPhysicalDrives(), sourcePath, destinationPath, name, dosType, fileSystemBlockSize, bootable);
            await Execute(command);
        }

        public static async Task RdbPartFormat(string path, int partitionNumber, string name, bool nonRdb, string chs,
            string dosType)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbPartFormatCommand(GetLogger<RdbPartFormatCommand>(), commandHelper,
                await GetPhysicalDrives(), path, partitionNumber, name, nonRdb, chs, dosType));
        }

        public static async Task RdbPartKill(string path, int partitionNumber, string hexBootBytes)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new RdbPartKillCommand(GetLogger<RdbPartKillCommand>(), commandHelper,
                await GetPhysicalDrives(), path, partitionNumber, hexBootBytes));
        }

        public static async Task RdbPartMove(string path, int partitionNumber, uint startCylinder)
        {
            using var commandHelper = GetCommandHelper();
            var command = new RdbPartMoveCommand(GetLogger<RdbPartMoveCommand>(), commandHelper,
                await GetPhysicalDrives(), path, partitionNumber, startCylinder);
            await Execute(command);
        }

        public static async Task BlockRead(string path, string outputPath, int blockSize, bool used, long? start, long? end)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new BlockReadCommand(GetLogger<BlockReadCommand>(), commandHelper,
                await GetPhysicalDrives(), path, outputPath, blockSize, used, start, end));
        }

        public static async Task BlockView(string path, int blockSize, long? start)
        {
            using var commandHelper = GetCommandHelper();
            await Execute(new BlockViewCommand(GetLogger<BlockViewCommand>(), commandHelper,
                await GetPhysicalDrives(), path, blockSize, start));
        }
        
        public static async Task FsDir(string path, bool recursive, FormatEnum format)
        {
            using var commandHelper = GetCommandHelper(useCache: true);
            var command = new FsDirCommand(GetLogger<FsDirCommand>(), commandHelper,
                await GetPhysicalDrives(), path, recursive);
            command.EntriesRead += (_, args) =>
            {
                Console.Write(EntriesPresenter.PresentEntries(args.EntriesInfo, format));
            };
            await Execute(command);
        }

        public static async Task FsMkDir(string path)
        {
            using var commandHelper = GetCommandHelper(useCache: true);
            var command = new FsMkDirCommand(GetLogger<FsMkDirCommand>(), commandHelper,
                await GetPhysicalDrives(), path);
            await Execute(command);
        }
        
        public static async Task FsCopy(string srcPath, string destPath, bool recursive, bool skipAttributes, bool quiet,
            UaeMetadata uaeMetadata, bool makeDirectory, bool forceOverwrite)
        {
            using var commandHelper = GetCommandHelper(useCache: true);
            var command = new FsCopyCommand(GetLogger<FsCopyCommand>(), commandHelper,
                await GetPhysicalDrives(), srcPath, destPath, recursive, skipAttributes, quiet, uaeMetadata: uaeMetadata,
                makeDirectory: makeDirectory, forceOverwrite: forceOverwrite);
            await Execute(command);
        }

        public static async Task ArcList(string path, bool recursive)
        {
            using var commandHelper = GetCommandHelper();
            var command = new ArcListCommand(GetLogger<ArcListCommand>(), commandHelper,
                await GetPhysicalDrives(), path, recursive);
            command.EntriesRead += (_, args) =>
            {
                Console.Write(EntriesPresenter.PresentEntries(args.EntriesInfo, FormatEnum.Table));
            };
            await Execute(command);
        }

        public static async Task FsExtract(string srcPath, string destPath, bool recursive, bool skipAttributes,
            bool quiet, UaeMetadata uaeMetadata, bool makeDirectory, bool forceOverwrite)
        {
            using var commandHelper = GetCommandHelper(useCache: true);
            var command = new FsExtractCommand(GetLogger<FsExtractCommand>(), commandHelper,
                await GetPhysicalDrives(), srcPath, destPath, recursive, skipAttributes, quiet, uaeMetadata: uaeMetadata,
                makeDirectory: makeDirectory, forceOverwrite: forceOverwrite);
            await Execute(command);
        }
        
        public static async Task AdfCreate(string adfPath, bool format, string name, string dosType, bool recursive)
        {
            using var commandHelper = GetCommandHelper();
            var command = new AdfCreateCommand(GetLogger<AdfCreateCommand>(), commandHelper,
                await GetPhysicalDrives(), adfPath, format, name, dosType, recursive);
            await Execute(command);
        }

        private static Task WriteProcessMessage(object sender, DataProcessedEventArgs args)
        {
            var parts = new List<string>();

            if (!args.Indeterminate)
            {
                parts.Add($"{args.PercentComplete:0.0}%");
            }
            parts.Add($"[{args.BytesPerSecond.FormatBytes(format: "0.0")}/s]");
            parts.Add($"[{args.BytesProcessed.FormatBytes()}{(!args.Indeterminate ? $" / {args.BytesTotal.FormatBytes()}" : string.Empty)}]");
            parts.Add($"[{args.TimeElapsed.FormatElapsed()}{(!args.Indeterminate ? $" / {args.TimeTotal.FormatElapsed()}" : string.Empty)}]");
            parts.Add($"{(args.PercentComplete >= 100 ? "      " : "   ")}\r");
            
            Console.Write(string.Join(" ", parts));
            
            return Task.CompletedTask;
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