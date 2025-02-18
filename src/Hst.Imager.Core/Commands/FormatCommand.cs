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
        private AssetAction assetAction;
        private string assetPath;
        private readonly string outputPath;
        private readonly Size size;

        private const long WorkbenchPartitionSize = 1000000000;
        private const long WorkPartitionSize = 64000000000;
        private const long PiStormBootPartitionSize = 1000000000;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="commandHelper"></param>
        /// <param name="physicalDrives"></param>
        /// <param name="path"></param>
        /// <param name="formatType"></param>
        /// <param name="fileSystem">File system to format.</param>
        /// <param name="assetAction"></param>
        /// <param name="assetPath">Path to asset file used to format.</param>
        /// <param name="outputPath">Output path to write file system from media.</param>
        /// <param name="size"></param>
        public FormatCommand(ILogger<FormatCommand> logger, ILoggerFactory loggerFactory, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, FormatType formatType, string fileSystem,
            AssetAction assetAction, string assetPath, string outputPath, Size size)
        {
            this.logger = logger;
            this.loggerFactory = loggerFactory;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.formatType = formatType;
            this.fileSystem = fileSystem;
            this.assetAction = string.IsNullOrWhiteSpace(assetPath) ? assetAction : AssetAction.None;
            this.assetPath = assetPath;
            this.outputPath = outputPath;
            this.size = size;
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
                assetAction = AssetAction.None;

                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return new Result(new Error($"Assert path required for file system '{rdbFileSystem.ToString().ToUpper()}'"));
                }

                if (!await MediaPathExists(assetPath))
                {
                    return new Result(new Error($"Assert path '{assetPath}' not found"));
                }
            }

            if ((formatType == FormatType.Rdb || formatType == FormatType.PiStorm) &&
                assetAction == AssetAction.DownloadPfs3Aio)
            {
                var pfs3AioLhaPath = Path.Combine(outputPath, Pfs3AioFileSystemHelper.Pfs3AioLhaFilename);

                if (!File.Exists(pfs3AioLhaPath))
                {
                    OnInformationMessage($"Downloading '{Pfs3AioFileSystemHelper.Pfs3AioLhaFilename}' from url '{Pfs3AioFileSystemHelper.Pfs3AioLhaUrl}'");
                }

                assetPath = await Pfs3AioFileSystemHelper.DownloadPfs3AioLha(outputPath);
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

            using (var media = mediaResult.Value)
            {
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

                var streamCopier = new StreamCopier();
                await streamCopier.Copy(token, emptyPartitionTableStream, disk.Content, 
                    emptyPartitionTableData.Length, 0, 0);
            }

            switch (formatType)
            {
                case FormatType.Mbr:
                    return await FormatMbrDisk(diskSize, mbrFileSystem, token);
                case FormatType.Gpt:
                    return await FormatGptDisk(diskSize, gptFileSystem, token);
                case FormatType.Rdb:
                    return await FormatRdbDisk(diskSize, rdbFileSystem, path, 0, token);
                case FormatType.PiStorm:
                    return await FormatPiStormDisk(diskSize, rdbFileSystem, token);
                default:
                    return new Result(new Error($"Unsupported format type '{formatType}'"));
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
                assetPath, fileSystemName);

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

            var mbrPartFormatCommand = new MbrPartFormatCommand(loggerFactory.CreateLogger<MbrPartFormatCommand>(), commandHelper, physicalDrives,
                path, 1, "Empty", fileSystem.ToUpper());
            AddMessageEvents(mbrPartFormatCommand);

            return await mbrPartFormatCommand.Execute(cancellationToken);
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

            var gptPartType = GetGptPartType(gptFileSystem);

            var gptPartFormatCommand = new GptPartFormatCommand(loggerFactory.CreateLogger<GptPartFormatCommand>(),
                commandHelper, physicalDrives,
                path, 1, gptPartType, "Empty");
            AddMessageEvents(gptPartFormatCommand);

            return await gptPartFormatCommand.Execute(cancellationToken);
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

        private static bool HasFastFileSystem2GbLimit(int version) => version < 45;

        private static bool HasFastFileSystemDos7Support(int version, int revision)
        {
            if (version > 46)
            {
                return true;
            }
            
            return revision >= 13;
        }

        private async Task<Result<int>> FormatRdbDisk(long diskSize,
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
                return new Result<int>(new Error($"No '{dosType}' file system not found in asset path '{assetPath}'"));
            }
            
            // read version string from file system path
            var versionString = VersionStringReader.Read(await File.ReadAllBytesAsync(fileSystemPath, cancellationToken));
            var amigaVersion = string.IsNullOrWhiteSpace(versionString)
                ? null
                : VersionStringReader.Parse(versionString);

            var maxWorkbenchPartitionSize = WorkbenchPartitionSize;
            var maxWorkPartitionSize = WorkPartitionSize;
            
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
                OnInformationMessage($"File system '{fileSystemPath}' with v{amigaVersion.Version}.{amigaVersion.Revision} doesn't partitions larger than 2GB. Max partition size is changed to 2GB, use FastFileSystem from AmigaOS 3.1.4 or newer for larger partitions!");

                maxWorkbenchPartitionSize = 500.MB();
                maxWorkPartitionSize = 2.GB();
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

            // add pfs3 filesystem
            var rdbFsAddCommand = new RdbFsAddCommand(loggerFactory.CreateLogger<RdbFsAddCommand>(), commandHelper, physicalDrives,
                rdbPath, fileSystemPath, dosType, string.Empty, null, null);
            AddMessageEvents(rdbFsAddCommand);

            var rdbFsAddResult = await rdbFsAddCommand.Execute(cancellationToken);
            if (rdbFsAddResult.IsFaulted)
            {
                return new Result<int>(rdbFsAddResult.Error);
            }

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
                physicalDrives, rdbPath, partitionName, dosType, partitionSize, null, null, null, 0x1fe00, null, false, true, null, 512);
                AddMessageEvents(rdbPartAddCommand);

                rdbPartAddResult = await rdbPartAddCommand.Execute(cancellationToken);
                if (rdbPartAddResult.IsFaulted)
                {
                    return new Result<int>(rdbPartAddResult.Error);
                }

                // format workbench partition
                rdbPartFormatCommand = new RdbPartFormatCommand(loggerFactory.CreateLogger<RdbPartFormatCommand>(), commandHelper,
                physicalDrives, rdbPath, 1, "Workbench", false, string.Empty, dosType);
                AddMessageEvents(rdbPartFormatCommand);

                rdbPartFormatResult = await rdbPartFormatCommand.Execute(cancellationToken);
                if (rdbPartFormatResult.IsFaulted)
                {
                    return new Result<int>(rdbPartFormatResult.Error);
                }

                partitionNumber++;
            }

            // return, if no space left for work partitions
            var hasWorkPartitions = diskSize - 50.MB() > maxWorkbenchPartitionSize;
            if (!hasWorkPartitions)
            {
                return new Result<int>(partitionNumber);
            }

            // calculate work partition count and size
            var workPartitionCount = diskSize > maxWorkPartitionSize
                ? Convert.ToInt32(Math.Ceiling((double)diskSize / maxWorkPartitionSize))
                : 1;
            var workPartitionSize = diskSize / workPartitionCount;

            // add work partitions
            for (var i = 0; i < workPartitionCount; i++)
            {
                var partitionSize = new Size(i < workPartitionCount - 1 ? workPartitionSize : 0, Unit.Bytes);

                // add work partition
                partitionName = $"DH{partitionNumber}";
                rdbPartAddCommand = new RdbPartAddCommand(loggerFactory.CreateLogger<RdbPartAddCommand>(), commandHelper,
                    physicalDrives, rdbPath, partitionName, dosType, partitionSize, null, null, null,
                    0x1fe00, null, false, false, null, 512);
                AddMessageEvents(rdbPartAddCommand);

                rdbPartAddResult = await rdbPartAddCommand.Execute(cancellationToken);
                if (rdbPartAddResult.IsFaulted)
                {
                    return new Result<int>(rdbPartAddResult.Error);
                }

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

                partitionNumber++;
            }

            return new Result<int>(partitionNumber);
        }

        private async Task<Result> FormatPiStormDisk(long diskSize,
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

            // limit to 1 pistorm disk by default, if size is not defined and disk size is larger than 128gb
            if ((size == null || size.Value == 0)
                && formatSize > 128.GB()
                && diskSize > 128.GB())
            {
                formatSize = 128.GB();
            }

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

            // format pistorm boot partition
            var mbrPartFormatCommand = new MbrPartFormatCommand(loggerFactory.CreateLogger<MbrPartFormatCommand>(), commandHelper, physicalDrives,
    path, 1, "Empty", MbrPartType.Fat32Lba.ToString());
            AddMessageEvents(mbrPartFormatCommand);

            var mbrPartFormatResult = await mbrPartFormatCommand.Execute(cancellationToken);
            if (mbrPartFormatResult.IsFaulted)
            {
                return new Result(mbrPartFormatResult.Error);
            }

            // calculate number of pistorm disks
            var piStormDiskSize = formatSize > 128.GB() ? 128.GB() : formatSize;
            var piStormDiskCount = formatSize > 128.GB() ? Math.Ceiling((double)formatSize / piStormDiskSize) : 1;

            // max 4 mbr primary partitions
            if (piStormDiskCount > 3)
            {
                piStormDiskCount = 3;
            }

            startSector = endSector + 1;
            var rdbPartitionNumber = 0;

            for (var i = 0; i < piStormDiskCount; i++)
            {
                endSector = startSector + (piStormDiskSize / 512);

                if (endSector > lastSector)
                {
                    endSector = lastSector;
                }

                var partitionSize = (endSector - startSector + 1) * 512;

                // add pistorm rdb partition
                mbrPartAddCommand = new MbrPartAddCommand(loggerFactory.CreateLogger<MbrPartAddCommand>(), commandHelper,
                    physicalDrives, path, MbrPartType.PiStormRdb.ToString(), new Size(), startSector, endSector, active: false);
                AddMessageEvents(mbrPartAddCommand);

                mbrPartAddResult = await mbrPartAddCommand.Execute(cancellationToken);
                if (mbrPartAddResult.IsFaulted)
                {
                    return new Result(mbrPartAddResult.Error);
                }

                // format pistorm mbr partition
                var piStormRdbPath = Path.Combine(path, "mbr", (i + 2).ToString());
                var formatRdbDiskResult = await FormatRdbDisk(partitionSize, formatRdbFileSystem,
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

        private void AddMessageEvents(CommandBase command)
        {
            command.InformationMessage += (object _, string message) => OnInformationMessage(message);
            command.DebugMessage += (object _, string message) => OnInformationMessage(message);
        }
    }
}