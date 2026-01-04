using DiscUtils.Streams;
using Hst.Core;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using DiscUtils.Raw;
using Hst.Core.Extensions;
using System.IO;
using Hst.Imager.Core.Commands.GptCommands;
using System;
using Hst.Amiga.VersionStrings;
using Hst.Imager.Core.Extensions;
using Hst.Imager.Core.FileSystems;

namespace Hst.Imager.Core.Commands
{
    public class FormatCommand : CommandBase
    {
        private readonly ILogger<FormatCommand> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly FormatType formatType;
        private readonly string fileSystem;
        private string fileSystemPath;
        private readonly string outputPath;
        private readonly Size size;
        private readonly Size maxPartitionSize;
        private readonly bool useExperimental;
        private readonly bool kickstart31;

        private int commandsExecuted;
        private int maxCommandsToExecute;

        private const int MaxRdbPartitions = 32;
        private const long MaxRdbExperimentalSize = 2199023255552; // [Math]::Pow(2, 41)
        private const long MaxRdbSize = 137438953472; // [Math]::Pow(2, 37)
        private const long WorkbenchPartitionSize = 1073741824; // [Math]::Pow(2, 30)
        private const long PiStormBootPartitionSize = 1073741824; // [Math]::Pow(2, 30)

        private const string Pfs3AioLhaUrl = "https://aminet.net/disk/misc/pfs3aio.lha";

        /// <summary>
        /// Format command.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="commandHelper"></param>
        /// <param name="physicalDrives"></param>
        /// <param name="path"></param>
        /// <param name="formatType"></param>
        /// <param name="fileSystem">File system to format.</param>
        /// <param name="fileSystemPath">Path to asset file used to format.</param>
        /// <param name="outputPath">Output path to write file system from media.</param>
        /// <param name="size"></param>
        /// <param name="maxPartitionSize">Max partition size for RDB and PiStorm.</param>
        /// <param name="useExperimental">Confirm using PFS3 experimental partition size.</param>
        /// <param name="kickstart31">Create Workbench partition size for Kickstart v3.1 or lower.</param>
        public FormatCommand(ILogger<FormatCommand> logger, ILoggerFactory loggerFactory, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, FormatType formatType, string fileSystem,
            string fileSystemPath, string outputPath, Size size, Size maxPartitionSize, bool useExperimental,
            bool kickstart31)
        {
            this.logger = logger;
            this.loggerFactory = loggerFactory;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.formatType = formatType;
            this.fileSystem = fileSystem;
            this.fileSystemPath = fileSystemPath;
            this.outputPath = outputPath;
            this.size = size;
            this.maxPartitionSize = maxPartitionSize;
            this.useExperimental = useExperimental;
            this.commandsExecuted = 0;
            this.maxCommandsToExecute = 0;
            this.kickstart31 = kickstart31;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            var gptFileSystem = FormatGptFileSystem.Fat32;
            if (formatType == FormatType.Gpt && !Enum.TryParse(fileSystem, true, out gptFileSystem))
            {
                return new Result(new Error($"Unsupported Guid Partition Table file system '{fileSystem}'"));
            }

            var mbrFileSystem = FormatMbrFileSystem.Fat32;
            if (formatType == FormatType.Mbr && !Enum.TryParse(fileSystem, true, out mbrFileSystem))
            {
                return new Result(new Error($"Unsupported Master Boot Record file system '{fileSystem}'"));
            }

            var rdbFileSystem = FormatRdbFileSystem.Pds3;
            if (formatType is FormatType.Rdb or FormatType.PiStorm &&
                !Enum.TryParse(fileSystem, true, out rdbFileSystem))
            {
                return new Result(new Error($"Unsupported Rigid Disk Block file system '{fileSystem}'"));
            }

            if ((formatType == FormatType.Rdb || formatType == FormatType.PiStorm) &&
                    (rdbFileSystem == FormatRdbFileSystem.Dos3 || rdbFileSystem == FormatRdbFileSystem.Dos7))
            {
                if (string.IsNullOrWhiteSpace(fileSystemPath))
                {
                    return new Result(new Error($"File system path required for file system '{rdbFileSystem.ToString().ToUpper()}'"));
                }

                if (!await MediaPathExists(fileSystemPath))
                {
                    return new Result(new Error($"Assert path '{fileSystemPath}' not found"));
                }
            }

            if (maxPartitionSize.Unit == Unit.Percent)
            {
                return new Result(new Error($"Max partition size doesn't support values in percentage"));
            }
            
            var maxRdbPartitionSize = useExperimental ? MaxRdbExperimentalSize : MaxRdbSize;
            if ((formatType == FormatType.Rdb || formatType == FormatType.PiStorm) &&
                maxPartitionSize.Value > maxRdbPartitionSize)
            {
                return new Result(new Error($"Max {(useExperimental ? "experimental " : "")}partition size must be equal or less than {maxRdbPartitionSize} bytes"));
            }

            // set file system path to pfs3aio url, if pfs3 or pds3 file system and file system path is not set
            if ((formatType == FormatType.Rdb || formatType == FormatType.PiStorm) &&
                (rdbFileSystem == FormatRdbFileSystem.Pfs3 || rdbFileSystem == FormatRdbFileSystem.Pds3) &&
                string.IsNullOrWhiteSpace(fileSystemPath))
            {
                fileSystemPath = Pfs3AioLhaUrl;
            }

            if ((formatType == FormatType.Rdb || formatType == FormatType.PiStorm) &&
                fileSystemPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                OnInformationMessage($"Downloading file system from url '{fileSystemPath}'");

                fileSystemPath = await FileSystemHelper.DownloadFile(fileSystemPath, outputPath, Path.GetFileName(fileSystemPath));
            }

            OnInformationMessage($"Formatting '{path}'");

            OnDebugMessage($"Opening '{path}' as writable");

            var physicalDrivesList = physicalDrives.ToList();
            var mediaResult = await commandHelper.GetWritableMedia(physicalDrivesList, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            long diskSize;
            bool isPhysicalDrive;

            using (var media = mediaResult.Value)
            {
                isPhysicalDrive = media.IsPhysicalDrive;
                
                var disk = media is DiskMedia diskMedia
                    ? diskMedia.Disk
                    : new Disk(media.Stream, Ownership.None);

                await using var stream = media.Stream;

                diskSize = media.Size;

                OnDebugMessage($"Disk size '{diskSize.FormatBytes()}' ({diskSize} bytes)");

                if (formatType == FormatType.PiStorm && diskSize < 2.GB())
                {
                    return new Result(new Error($"Formatting PiStorm requires disk size of minimum 2GB and disk size is '{diskSize.FormatBytes()}' ({diskSize} bytes)"));
                }

                OnInformationMessage($"Erasing partition tables");

                var emptyPartitionTableData = new byte[diskSize < 10.MB().ToSectorSize() ? diskSize : 10.MB().ToSectorSize()];
                var emptyPartitionTableStream = new MemoryStream(emptyPartitionTableData);

                using var streamCopier = new StreamCopier();
                await streamCopier.Copy(token, emptyPartitionTableStream, disk.Content, 
                    emptyPartitionTableData.Length, 0, 0);
            }

            // calculate max commands to execute + 1 for erasing partition tables
            maxCommandsToExecute = CalculateCommandsToExecute(formatType, diskSize) + 1;

            UpdateCommandsExecuted(1);

            Result formatResult;

            switch (formatType)
            {
                case FormatType.Mbr:
                    formatResult = await FormatMbrDisk(diskSize, mbrFileSystem, token);
                    break;
                case FormatType.Gpt:
                    formatResult = await FormatGptDisk(diskSize, gptFileSystem, token);
                    break;
                case FormatType.Rdb:
                    formatResult = await FormatRdbDisk(diskSize, maxRdbPartitionSize, rdbFileSystem, path, 0, token);
                    break;
                case FormatType.PiStorm:
                    formatResult = await FormatPiStormDisk(diskSize, maxRdbPartitionSize, rdbFileSystem, token);
                    break;
                default:
                    formatResult = new Result(new Error($"Unsupported format type '{formatType}'"));
                    break;
            }

            if (isPhysicalDrive)
            {
                await commandHelper.RescanPhysicalDrives();
            }

            UpdatePercentComplete(100);

            return formatResult;
        }

        private int CalculateCommandsToExecute(FormatType formatType, long diskSize)
        {
            switch (formatType)
            {
                case FormatType.Mbr:
                case FormatType.Gpt:
                    // 3 commands: mbr/gpt init command, mbr/gpt part add command and mbr/gpt part format commands
                    return 3;
                case FormatType.Rdb:
                case FormatType.PiStorm:
                    var formatSize = diskSize.ResolveSize(size).ToSectorSize();

                    // 2 commands: rdb init command and rdb fs add command
                    // 2 commands: rdb part add command and rdb part format command for workbench
                    // 2 command for each 2gb: rdb part add command and rdb part format for each work partition, assuming worst case of max 2gb partition size.
                    var formatRdbCommands = 4 + (Math.Ceiling((double)formatSize / 2.GB()) * 2);

                    if (formatType == FormatType.Rdb)
                    {
                        return Convert.ToInt32(formatRdbCommands);
                    }

                    // 2 commands: mbr part add command and mbr part format command for pistorm boot.
                    // 1 command for each 128gb: mbr part add command for each pistorm rdb disk
                    var formatPiStormRdbCommands = 2 + (Math.Ceiling((double)formatSize / 128.GB()) * (formatRdbCommands + 1));

                    return Convert.ToInt32(formatPiStormRdbCommands);
                default:
                    return 0;
            }

        }

        private async Task<bool> MediaPathExists(string mediaPath)
        {
            var mediaResult = await commandHelper.GetReadableFileMedia(mediaPath);
            if (mediaResult.IsFaulted)
            {
                return false;
            }

            using var media = mediaResult.Value;

            return true;
        }

        private async Task<Result<string>> PrepareRdbFileSystem(FormatRdbFileSystem rdbFileSystem)
        {
            var dosType = rdbFileSystem.ToString().ToUpper();

            OnInformationMessage($"Preparing Rigid Disk Block file system '{dosType}' at path '{outputPath}'");

            var fileSystemName = GetFileSystemName(rdbFileSystem);

            var fileSystemPath = Path.Combine(outputPath, fileSystemName);

            if (Path.Exists(fileSystemPath))
            {
                File.Delete(fileSystemPath);
            }

            var findFileSystemInMediaResult = await AmigaFileSystemHelper.FindFileSystemInMedia(commandHelper,
                this.fileSystemPath, fileSystemName);

            if (findFileSystemInMediaResult.IsFaulted)
            {
                return new Result<string>(findFileSystemInMediaResult.Error);
            }

            var fileSystem = findFileSystemInMediaResult.Value;

            var fileSystemMediaResult = await commandHelper.GetWritableFileMedia(
                Path.Combine(outputPath, fileSystem.Item1), create: true);
            if (fileSystemMediaResult.IsFaulted)
            {
                return new Result<string>(fileSystemMediaResult.Error);
            }

            using var fileSystemMedia = fileSystemMediaResult.Value;
            await fileSystemMedia.Stream.WriteBytes(fileSystem.Item2);

            OnInformationMessage($"Prepared '{fileSystem.Item1}' for Rigid Disk Block file system '{dosType}'");

            return new Result<string>(Path.Exists(fileSystemPath) ? fileSystemPath : null);
        }
        
        private static string GetFileSystemName(FormatRdbFileSystem formatRdbFileSystem)
        {
            return formatRdbFileSystem switch
            {
                FormatRdbFileSystem.Dos3 or FormatRdbFileSystem.Dos7 => "FastFileSystem",
                FormatRdbFileSystem.Pfs3 or FormatRdbFileSystem.Pds3 => "pfs3aio",
                _ => throw new ArgumentOutOfRangeException(nameof(formatRdbFileSystem), formatRdbFileSystem, null)
            };
        }

        private async Task<Result> FormatMbrDisk(long diskSize, FormatMbrFileSystem mbrFileSystem, 
            CancellationToken cancellationToken)
        {
            var mbrInitCommand = new MbrInitCommand(loggerFactory.CreateLogger<MbrInitCommand>(), commandHelper, physicalDrives, path);
            AddMessageEvents(mbrInitCommand);

            var mbrInitResult = await mbrInitCommand.Execute(cancellationToken);
            if (mbrInitResult.IsFaulted)
            {
                return new Result(mbrInitResult.Error);
            }

            UpdateCommandsExecuted(1);

            var partitionSize = diskSize.ResolveSize(size).ToSectorSize();
            long? startSector = 2048;
            long? endSector = partitionSize / 512;
            var type = mbrFileSystem == FormatMbrFileSystem.Fat32
                ? MbrPartType.Fat32Lba.ToString()
                : fileSystem;

            var mbrPartAddCommand = new MbrPartAddCommand(loggerFactory.CreateLogger<MbrPartAddCommand>(), commandHelper, physicalDrives,
                path, type, new Size(), startSector, endSector, active: true);
            AddMessageEvents(mbrPartAddCommand);

            var mbrPartAddResult = await mbrPartAddCommand.Execute(cancellationToken);
            if (mbrPartAddResult.IsFaulted)
            {
                return new Result(mbrPartAddResult.Error);
            }

            UpdateCommandsExecuted(1);

            var mbrPartFormatCommand = new MbrPartFormatCommand(loggerFactory.CreateLogger<MbrPartFormatCommand>(), commandHelper, physicalDrives,
                path, 1, "Empty", fileSystem.ToUpper());
            AddMessageEvents(mbrPartFormatCommand);

            var mbrPartFormatResult = await mbrPartFormatCommand.Execute(cancellationToken);

            UpdateCommandsExecuted(1);

            return mbrPartFormatResult;
        }

        private async Task<Result> FormatGptDisk(long diskSize, FormatGptFileSystem gptFileSystem, CancellationToken cancellationToken)
        {
            var gptInitCommand = new GptInitCommand(loggerFactory.CreateLogger<GptInitCommand>(), commandHelper, physicalDrives, path);
            AddMessageEvents(gptInitCommand);

            var gptInitResult = await gptInitCommand.Execute(cancellationToken);
            if (gptInitResult.IsFaulted)
            {
                return new Result(gptInitResult.Error);
            }

            UpdateCommandsExecuted(1);

            var partitionSize = diskSize.ResolveSize(size).ToSectorSize();
            long? startSector = 2048;
            long? endSector = partitionSize / 512;

            var gptPartAddCommand = new GptPartAddCommand(loggerFactory.CreateLogger<GptPartAddCommand>(), commandHelper, physicalDrives,
                path, gptFileSystem.ToString(), "Empty", new Size(), startSector, endSector);
            AddMessageEvents(gptPartAddCommand);

            var gptPartAddResult = await gptPartAddCommand.Execute(cancellationToken);
            if (gptPartAddResult.IsFaulted)
            {
                return new Result(gptPartAddResult.Error);
            }

            UpdateCommandsExecuted(1);

            var gptPartType = GetGptPartType(gptFileSystem);

            var gptPartFormatCommand = new GptPartFormatCommand(loggerFactory.CreateLogger<GptPartFormatCommand>(),
                commandHelper, physicalDrives,
                path, 1, gptPartType, "Empty");
            AddMessageEvents(gptPartFormatCommand);

            var gptPartFormatResult = await gptPartFormatCommand.Execute(cancellationToken);

            UpdateCommandsExecuted(1);

            return gptPartFormatResult;
        }

        private static GptPartType GetGptPartType(FormatGptFileSystem gptFileSystem)
        {
            return gptFileSystem switch
            {
                FormatGptFileSystem.Fat32 => GptPartType.Fat32,
                FormatGptFileSystem.Ntfs => GptPartType.Ntfs,
                FormatGptFileSystem.ExFat => GptPartType.ExFat,
                _ => throw new ArgumentException($"Unsupported Guid Partition Table file system '{gptFileSystem}'", nameof(gptFileSystem)),
            };
        }

        /// <summary>
        /// Determine if FastFileSystem version supports partitions larger than 2GB.
        /// FastFileSystem with AmigaOS 3.5 and newer supports partitions larger than 2GB.
        /// </summary>
        /// <param name="version">Version to check.</param>
        /// <returns>True if version supports partitions larger than 2GB. Otherwise false.</returns>
        private static bool HasFastFileSystem2GbLimit(int version) => version < 45;

        /// <summary>
        /// Determine if FastFileSystem version and revision supports DOS7 long filenames.
        /// FastFileSystem with AmigaOS 3.1.4 and newer supports DOS7 long filenames.
        /// </summary>
        /// <param name="version">Version to check.</param>
        /// <param name="revision">Revision to check.</param>
        /// <returns>True if version and revision supports DOS7 long filenames. Otherwise false.</returns>
        private static bool HasFastFileSystemDos7Support(int version, int revision)
        {
            if (version > 46)
            {
                return true;
            }
            
            return revision >= 13;
        }

        private async Task<Result<int>> FormatRdbDisk(long diskSize, long maxRdbDiskSize,
            FormatRdbFileSystem formatRdbFileSystem, string rdbPath, int partitionNumber, CancellationToken cancellationToken)
        {
            var fileSystemPathResult = await PrepareRdbFileSystem(formatRdbFileSystem);
            if (fileSystemPathResult.IsFaulted)
            {
                return new Result<int>(fileSystemPathResult.Error);
            }

            var dosType = formatRdbFileSystem.ToString().ToUpper();
            var fileSystemPath = fileSystemPathResult.Value;

            if (string.IsNullOrEmpty(fileSystemPath))
            {
                return new Result<int>(new Error($"No '{dosType}' file system not found in asset path '{this.fileSystemPath}'"));
            }
            
            // read version string from file system path
            var versionString = VersionStringReader.Read(await File.ReadAllBytesAsync(fileSystemPath, cancellationToken));
            var amigaVersion = string.IsNullOrWhiteSpace(versionString)
                ? null
                : VersionStringReader.Parse(versionString);

            var maxWorkPartitionSize = Convert.ToInt64(maxPartitionSize.Value == 0 ? maxRdbDiskSize : maxPartitionSize.Value);
            var maxWorkbenchPartitionSize = kickstart31 ? WorkbenchPartitionSize : maxWorkPartitionSize;
            
            // change dos type to dos3, if fast file system doesn't support dos7 (version 46.13 or higher)
            if (amigaVersion != null &&
                dosType.Equals("DOS7", StringComparison.OrdinalIgnoreCase) &&
                !HasFastFileSystemDos7Support(amigaVersion.Version, amigaVersion.Revision))
            {
                OnInformationMessage($"File system '{fileSystemPath}' with v{amigaVersion.Version}.{amigaVersion.Revision} doesn't support DOS7. File system is changed to DOS3, use FastFileSystem from AmigaOS 3.1.4 or newer for DOS7 support!");

                dosType = "DOS3";
            }
            
            // change partition sizes, if fast file system has 2gb limit (version 45 or lower)
            if (amigaVersion != null &&
                (dosType.Equals("DOS3", StringComparison.OrdinalIgnoreCase) || 
                 dosType.Equals("DOS7", StringComparison.OrdinalIgnoreCase)) &&
                HasFastFileSystem2GbLimit(amigaVersion.Version))
            {
                OnInformationMessage($"File system '{fileSystemPath}' with v{amigaVersion.Version}.{amigaVersion.Revision} doesn't support partitions larger than 2GB. Max partition size is changed to 2GB, use FastFileSystem from AmigaOS 3.1.4 or newer for larger partitions!");

                maxWorkPartitionSize = 2.GB();
                maxWorkbenchPartitionSize = kickstart31 ? 500.MB() : maxWorkPartitionSize;
            }

            // change max work partition size to pfs3 max partition size limit,
            // if dos type is pfs3 or pds3 and max work partition size is larger
            // than pfs3 max partition size
            var cylinderSize = 16 * 63 * 512;
            var cylinderGapSize = cylinderSize * 10;
            if ((dosType.Equals("PDS3", StringComparison.OrdinalIgnoreCase) ||
                 dosType.Equals("PFS3", StringComparison.OrdinalIgnoreCase)) &&
                maxWorkPartitionSize > FileSystemHelper.Pfs3MaxPartitionSize - cylinderGapSize &&
                !useExperimental)
            {
                // cylinder size of 16 heads * 63 sectors * 512 bytes is subtracted from max partition size
                // to ensure that max work partition size is less than pfs3 max partition size
                maxWorkPartitionSize = FileSystemHelper.Pfs3MaxPartitionSize - cylinderGapSize;
            }
            
            // init rdb
            var rdbInitCommand = new RdbInitCommand(loggerFactory.CreateLogger<RdbInitCommand>(), commandHelper, physicalDrives,
                rdbPath, "HstImager", size, string.Empty, 0);
            AddMessageEvents(rdbInitCommand);

            var rdbInitResult = await rdbInitCommand.Execute(cancellationToken);
            if (rdbInitResult.IsFaulted)
            {
                return new Result<int>(rdbInitResult.Error);
            }

            UpdateCommandsExecuted(1);

            // add pfs3 filesystem
            var rdbFsAddCommand = new RdbFsAddCommand(loggerFactory.CreateLogger<RdbFsAddCommand>(), commandHelper, physicalDrives,
                rdbPath, fileSystemPath, dosType, string.Empty, null, null);
            AddMessageEvents(rdbFsAddCommand);

            var rdbFsAddResult = await rdbFsAddCommand.Execute(cancellationToken);
            if (rdbFsAddResult.IsFaulted)
            {
                return new Result<int>(rdbFsAddResult.Error);
            }

            UpdateCommandsExecuted(1);

            string partitionName;
            RdbPartAddCommand rdbPartAddCommand;
            Result rdbPartAddResult;
            RdbPartFormatCommand rdbPartFormatCommand;
            Result rdbPartFormatResult;

            var hasWorkbenchPartition = false;

            if (partitionNumber == 0)
            {
                hasWorkbenchPartition = true;
                var partitionSize = new Size(diskSize - 5.MB() > maxWorkbenchPartitionSize
                    ? maxWorkbenchPartitionSize
                    : 0, Unit.Bytes);

                // add workbench partition
                partitionName = $"DH{partitionNumber}";
                rdbPartAddCommand = new RdbPartAddCommand(loggerFactory.CreateLogger<RdbPartAddCommand>(), commandHelper,
                physicalDrives, rdbPath, partitionName, dosType, partitionSize, null, null, null, 0x1fe00, null, false, true, null, 512, useExperimental);
                AddMessageEvents(rdbPartAddCommand);

                rdbPartAddResult = await rdbPartAddCommand.Execute(cancellationToken);
                if (rdbPartAddResult.IsFaulted)
                {
                    return new Result<int>(rdbPartAddResult.Error);
                }

                UpdateCommandsExecuted(1);

                // format workbench partition
                rdbPartFormatCommand = new RdbPartFormatCommand(loggerFactory.CreateLogger<RdbPartFormatCommand>(), commandHelper,
                physicalDrives, rdbPath, 1, "Workbench", false, string.Empty, dosType);
                AddMessageEvents(rdbPartFormatCommand);

                rdbPartFormatResult = await rdbPartFormatCommand.Execute(cancellationToken);
                if (rdbPartFormatResult.IsFaulted)
                {
                    return new Result<int>(rdbPartFormatResult.Error);
                }

                UpdateCommandsExecuted(1);

                partitionNumber++;
            }

            // return, if no space left for work partitions
            var hasWorkPartitions = diskSize - 50.MB() > maxWorkbenchPartitionSize;
            if (!hasWorkPartitions)
            {
                return new Result<int>(partitionNumber);
            }

            // calculate work partition sizes based on max work partition size
            var workPartitionSizes = FileSystemHelper.CalculateRdbPartitionSizes(diskSize,
                maxWorkPartitionSize).ToList();

            // add work partitions to rdb
            var workPartitionCount =
                Math.Min(MaxRdbPartitions - (hasWorkbenchPartition ? 1 : 0), workPartitionSizes.Count);
            for (var i = 0; i < workPartitionCount; i++)
            {
                var partitionSize = new Size(i < workPartitionSizes.Count - 1 ? workPartitionSizes[i] : 0, Unit.Bytes);

                // add work partition
                partitionName = $"DH{partitionNumber}";
                rdbPartAddCommand = new RdbPartAddCommand(loggerFactory.CreateLogger<RdbPartAddCommand>(), commandHelper,
                    physicalDrives, rdbPath, partitionName, dosType, partitionSize, null, null, null,
                    0x1fe00, null, false, false, null, 512, useExperimental);
                AddMessageEvents(rdbPartAddCommand);

                rdbPartAddResult = await rdbPartAddCommand.Execute(cancellationToken);
                if (rdbPartAddResult.IsFaulted)
                {
                    return new Result<int>(rdbPartAddResult.Error);
                }

                UpdateCommandsExecuted(1);

                // format work partition
                rdbPartFormatCommand = new RdbPartFormatCommand(loggerFactory.CreateLogger<RdbPartFormatCommand>(),
                    commandHelper,
                    physicalDrives, rdbPath, i + 1 + (hasWorkbenchPartition ? 1 : 0),
                    $"Work{(partitionNumber >= 2 ? partitionNumber.ToString() : string.Empty)}",
                    false,
                    string.Empty,
                    dosType);
                AddMessageEvents(rdbPartFormatCommand);

                rdbPartFormatResult = await rdbPartFormatCommand.Execute(cancellationToken);
                if (rdbPartFormatResult.IsFaulted)
                {
                    return new Result<int>(rdbPartFormatResult.Error);
                }

                UpdateCommandsExecuted(1);

                partitionNumber++;
            }

            return new Result<int>(partitionNumber);
        }

        private async Task<Result> FormatPiStormDisk(long diskSize, long maxRdbPartitionSize,
            FormatRdbFileSystem formatRdbFileSystem, CancellationToken cancellationToken)
        {
            var mbrInitCommand = new MbrInitCommand(loggerFactory.CreateLogger<MbrInitCommand>(), commandHelper, physicalDrives, path);
            AddMessageEvents(mbrInitCommand);

            var mbrInitResult = await mbrInitCommand.Execute(cancellationToken);
            if (mbrInitResult.IsFaulted)
            {
                return new Result(mbrInitResult.Error);
            }

            var formatSize = diskSize.ResolveSize(size).ToSectorSize();


            // should be a argument?
            //var piStormDiskCount = 1;
            
            
            // limit to 1 pistorm disk by default, if size is not defined and disk size is larger than 128gb
            // if ((size == null || size.Value == 0)
            //     && formatSize > 128.GB()
            //     && diskSize > 128.GB())
            // {
            //     formatSize = 128.GB();
            // }

            long lastSector = formatSize / 512;

            long startSector = 2048;
            long endSector = PiStormBootPartitionSize / 512;

            // add pistorm boot partition
            var mbrPartAddCommand = new MbrPartAddCommand(loggerFactory.CreateLogger<MbrPartAddCommand>(), commandHelper,
                physicalDrives, path, MbrPartType.Fat32Lba.ToString(), new Size(), startSector, endSector, active: true);
            AddMessageEvents(mbrPartAddCommand);

            var mbrPartAddResult = await mbrPartAddCommand.Execute(cancellationToken);
            if (mbrPartAddResult.IsFaulted)
            {
                return new Result(mbrPartAddResult.Error);
            }

            UpdateCommandsExecuted(1);

            // format pistorm boot partition
            var mbrPartFormatCommand = new MbrPartFormatCommand(loggerFactory.CreateLogger<MbrPartFormatCommand>(), commandHelper, physicalDrives,
    path, 1, "Empty", MbrPartType.Fat32Lba.ToString());
            AddMessageEvents(mbrPartFormatCommand);

            var mbrPartFormatResult = await mbrPartFormatCommand.Execute(cancellationToken);
            if (mbrPartFormatResult.IsFaulted)
            {
                return new Result(mbrPartFormatResult.Error);
            }

            UpdateCommandsExecuted(1);

            // calculate number of pistorm disks
            //var piStormDiskSize = formatSize > 128.GB() ? 128.GB() : formatSize;
            
            // use experimental should allow big pistorm disk sizes and custom ones.
            var piStormDiskSize = useExperimental ? MaxRdbExperimentalSize : MaxRdbSize;
            //var piStormDiskCount = useExperimental ? formatSize > 128.GB() ? Math.Ceiling((double)formatSize / piStormDiskSize) : 1;
            
            // for simplicity pistorm disk count is set to 1
            // when supportming more than one then must be limited to 3 due to max 4 mbr primary partitions: boot partition and max 3 pistorm partitions
            var piStormDiskCount = 1;
            
            startSector = endSector + 1;
            var rdbPartitionNumber = 0;

            for (var i = 0; i < piStormDiskCount; i++)
            {
                endSector = startSector + (piStormDiskSize / 512);

                if (endSector > lastSector)
                {
                    endSector = lastSector;
                }

                var mbrPartitionSize = (endSector - startSector + 1) * 512;

                // add pistorm rdb partition
                mbrPartAddCommand = new MbrPartAddCommand(loggerFactory.CreateLogger<MbrPartAddCommand>(), commandHelper,
                    physicalDrives, path, MbrPartType.PiStormRdb.ToString(), new Size(), startSector, endSector, active: false);
                AddMessageEvents(mbrPartAddCommand);

                mbrPartAddResult = await mbrPartAddCommand.Execute(cancellationToken);
                if (mbrPartAddResult.IsFaulted)
                {
                    return new Result(mbrPartAddResult.Error);
                }

                UpdateCommandsExecuted(1);

                // format pistorm mbr partition
                var piStormRdbPath = Path.Combine(path, "mbr", (i + 2).ToString());
                var formatRdbDiskResult = await FormatRdbDisk(mbrPartitionSize, maxRdbPartitionSize, formatRdbFileSystem,
                    piStormRdbPath, rdbPartitionNumber, cancellationToken);
                if (formatRdbDiskResult.IsFaulted)
                {
                    return new Result(formatRdbDiskResult.Error);
                }

                rdbPartitionNumber = formatRdbDiskResult.Value;

                // calculate next start sector
                startSector = endSector + 1;
            }

            return new Result();
        }

        private void UpdateCommandsExecuted(int commands)
        {
            commandsExecuted += commands;

            var percentComplete = Math.Round((100d / maxCommandsToExecute) * commandsExecuted);

            if (percentComplete < 0)
            {
                percentComplete = 0;
            }

            if (percentComplete > 100)
            {
                percentComplete = 100;
            }

            UpdatePercentComplete(percentComplete);
        }

        private void UpdatePercentComplete(double percentComplete)
        {
            OnDataProcessed(false, percentComplete, 0, 0, 0,
                TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 0);
        }

        private void AddMessageEvents(CommandBase command)
        {
            command.InformationMessage += (object _, string message) => OnInformationMessage(message);
            command.WarningMessage += (object _, string message) => OnWarningMessage(message);
            command.DebugMessage += (object _, string message) => OnDebugMessage(message);
        }
    }
}