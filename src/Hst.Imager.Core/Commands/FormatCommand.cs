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
using Hst.Imager.Core.Extensions;

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
        private readonly Size size;

        private const long WorkbenchPartitionSize = 1000000000;
        private const long WorkPartitionSize = 64000000000;
        private const long PiStormBootPartitionSize = 1000000000;

        public FormatCommand(ILogger<FormatCommand> logger, ILoggerFactory loggerFactory, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, FormatType formatType, string fileSystem,
            Size size)
        {
            this.logger = logger;
            this.loggerFactory = loggerFactory;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.formatType = formatType;
            this.fileSystem = fileSystem;
            this.size = size;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            var gptPartType = GptPartType.Fat32;
            if (formatType == FormatType.Gpt && !Enum.TryParse<GptPartType>(fileSystem, true, out gptPartType))
            {
                return new Result(new Error($"Unsupported Guid Partition Table file system '{fileSystem}'"));
            }

            if ((formatType == FormatType.Rdb || formatType == FormatType.PiStorm) &&
                !(fileSystem.Equals("pfs3", StringComparison.OrdinalIgnoreCase) ||
                fileSystem.Equals("pds3", StringComparison.OrdinalIgnoreCase)))
            {
                return new Result(new Error($"Unsupported Rigid Disk Block file system '{fileSystem}'"));
            }

            OnInformationMessage($"Formatting '{path}'");

            OnDebugMessage($"Opening '{path}' as writable");

            var physicalDrivesList = physicalDrives.ToList();
            var mediaResult = await commandHelper.GetWritableMedia(physicalDrivesList, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            var diskSize = 0L;

            using (var media = mediaResult.Value)
            {
                var disk = media is DiskMedia diskMedia
                    ? diskMedia.Disk
                    : new Disk(media.Stream, Ownership.None);

                using var stream = media.Stream;

                diskSize = media.Size;

                OnDebugMessage($"Disk size '{diskSize.FormatBytes()}' ({diskSize} bytes)");

                if (formatType == FormatType.PiStorm && diskSize < 2.GB())
                {
                    return new Result(new Error($"Formatting PiStorm requires disk size of minimum 2GB and disk size is '{diskSize.FormatBytes()}' ({diskSize} bytes)"));
                }

                OnInformationMessage($"Erasing partition tables");

                var emptyPartitionTableData = new byte[diskSize < 10.MB().ToSectorSize() ? diskSize : 10.MB().ToSectorSize()];
                var emptyPartitionTableStream = new MemoryStream(emptyPartitionTableData);

                var isVhd = commandHelper.IsVhd(path);
                var streamCopier = new StreamCopier();
                await streamCopier.Copy(token, emptyPartitionTableStream, disk.Content, emptyPartitionTableData.Length, 0, 0, isVhd);
            }

            mediaResult = await commandHelper.GetWritableMedia(physicalDrivesList, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            switch (formatType)
            {
                case FormatType.Mbr:
                    return await FormatMbrDisk(diskSize, token);
                case FormatType.Gpt:
                    return await FormatGptDisk(diskSize, gptPartType, token);
                case FormatType.Rdb:
                    return await FormatRdbDisk(diskSize, path, 0, token);
                case FormatType.PiStorm:
                    return await FormatPiStormDisk(diskSize, token);
                default:
                    return new Result(new Error($"Unsupported partition table '{formatType}'"));
            }
        }

        private async Task<Result> FormatMbrDisk(long diskSize, CancellationToken cancellationToken)
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

            var mbrPartAddCommand = new MbrPartAddCommand(loggerFactory.CreateLogger<MbrPartAddCommand>(), commandHelper, physicalDrives,
                path, fileSystem, new Size(), startSector, endSector, active: true);
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

        private async Task<Result> FormatGptDisk(long diskSize, GptPartType gptPartType, CancellationToken cancellationToken)
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
                path, gptPartType.ToString(), "Empty", new Size(), startSector, endSector);
            AddMessageEvents(gptPartAddCommand);

            var gptPartAddResult = await gptPartAddCommand.Execute(cancellationToken);
            if (gptPartAddResult.IsFaulted)
            {
                return new Result(gptPartAddResult.Error);
            }

            var gptPartFormatCommand = new GptPartFormatCommand(loggerFactory.CreateLogger<GptPartFormatCommand>(), commandHelper, physicalDrives,
                path, 1, gptPartType, "Empty");
            AddMessageEvents(gptPartFormatCommand);

            return await gptPartFormatCommand.Execute(cancellationToken);
        }

        private async Task<Result<int>> FormatRdbDisk(long diskSize, string rdbPath, int partitionNumber, CancellationToken cancellationToken)
        {
            var pfs3AioPath = "pfs3aio";

            if (!File.Exists(pfs3AioPath))
            {
                return new Result<int>(new Error("Pfs3 filesystem file 'pfs3aio' not found"));
            }

            var rdbFileSystem = "PDS3";

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
                rdbPath, pfs3AioPath, rdbFileSystem, string.Empty, null, null);
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
                var size = new Size(diskSize - 5.MB() > WorkbenchPartitionSize ? WorkbenchPartitionSize : 0, Unit.Bytes);

                // add workbench partition
                partitionName = $"DH{partitionNumber}";
                rdbPartAddCommand = new RdbPartAddCommand(loggerFactory.CreateLogger<RdbPartAddCommand>(), commandHelper,
                physicalDrives, rdbPath, partitionName, rdbFileSystem, size, null, null, null, 0x1fe00, null, false, true, null, 512);
                AddMessageEvents(rdbPartAddCommand);

                rdbPartAddResult = await rdbPartAddCommand.Execute(cancellationToken);
                if (rdbPartAddResult.IsFaulted)
                {
                    return new Result<int>(rdbPartAddResult.Error);
                }

                // format workbench partition
                rdbPartFormatCommand = new RdbPartFormatCommand(loggerFactory.CreateLogger<RdbPartFormatCommand>(), commandHelper,
                physicalDrives, rdbPath, 1, "Workbench", false, string.Empty, rdbFileSystem);
                AddMessageEvents(rdbPartFormatCommand);

                rdbPartFormatResult = await rdbPartFormatCommand.Execute(cancellationToken);
                if (rdbPartFormatResult.IsFaulted)
                {
                    return new Result<int>(rdbPartFormatResult.Error);
                }

                partitionNumber++;
            }

            // return, if no space left for work partitions
            var hasWorkPartitions = diskSize - 50.MB() > WorkbenchPartitionSize;
            if (!hasWorkPartitions)
            {
                return new Result<int>(partitionNumber);
            }

            // calculate work partition count and size
            var workPartitionCount = diskSize > WorkPartitionSize
                ? Convert.ToInt32(Math.Ceiling((double)diskSize / WorkPartitionSize))
                : 1;
            var workPartitionSize = diskSize / workPartitionCount;

            // add work partitions
            for (var i = 0; i < workPartitionCount; i++)
            {
                var size = new Size(i < workPartitionCount - 1 ? workPartitionSize : 0, Unit.Bytes);

                // add work partition
                partitionName = $"DH{partitionNumber}";
                rdbPartAddCommand = new RdbPartAddCommand(loggerFactory.CreateLogger<RdbPartAddCommand>(), commandHelper,
                    physicalDrives, rdbPath, partitionName, rdbFileSystem, size, null, null, null,
                    0x1fe00, null, false, false, null, 512);
                AddMessageEvents(rdbPartAddCommand);

                rdbPartAddResult = await rdbPartAddCommand.Execute(cancellationToken);
                if (rdbPartAddResult.IsFaulted)
                {
                    return new Result<int>(rdbPartAddResult.Error);
                }

                // format work partition
                rdbPartFormatCommand = new RdbPartFormatCommand(loggerFactory.CreateLogger<RdbPartFormatCommand>(), commandHelper,
                    physicalDrives, rdbPath, i + 1 + (hasWorkbenchPartition ? 1 : 0),
                    $"Work{(partitionNumber >= 2 ? partitionNumber.ToString() : string.Empty)}", false, string.Empty, rdbFileSystem);
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

        private async Task<Result> FormatPiStormDisk(long diskSize, CancellationToken cancellationToken)
        {
            var pfs3AioPath = "pfs3aio";

            if (!File.Exists(pfs3AioPath))
            {
                return new Result(new Error("Pfs3 filesystem file 'pfs3aio' not found"));
            }

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

                //
                var piStormRdbPath = Path.Combine(path, "mbr", (i + 2).ToString());
                var formatRdbDiskResult = await FormatRdbDisk(partitionSize, piStormRdbPath, rdbPartitionNumber, cancellationToken);
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