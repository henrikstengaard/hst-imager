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
        private readonly PartitionTable partitionTable;
        private readonly string fileSystem;
        private readonly Size size;

        public FormatCommand(ILogger<FormatCommand> logger, ILoggerFactory loggerFactory, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, PartitionTable partitionTable, string fileSystem,
            Size size)
        {
            this.logger = logger;
            this.loggerFactory = loggerFactory;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.partitionTable = partitionTable;
            this.fileSystem = fileSystem;
            this.size = size;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            var gptPartType = GptPartType.Fat32;
            if (partitionTable == PartitionTable.Gpt && !Enum.TryParse<GptPartType>(fileSystem, true, out gptPartType))
            {
                return new Result(new Error($"Unsupported Guid Partition Table file system '{fileSystem}'"));
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

            switch (partitionTable)
            {
                case PartitionTable.Mbr:
                    return await FormatMbrPartition(diskSize, token);
                case PartitionTable.Gpt:
                    return await FormatGptPartition(diskSize, gptPartType, token);
                default:
                    return new Result(new Error($"Unsupported partition table '{partitionTable}'"));
            }
        }

        private async Task<Result> FormatMbrPartition(long diskSize, CancellationToken cancellationToken)
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

        private async Task<Result> FormatGptPartition(long diskSize, GptPartType gptPartType, CancellationToken cancellationToken)
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

        private void AddMessageEvents(CommandBase command)
        {
            command.InformationMessage += (object _, string message) => OnInformationMessage(message);
            command.DebugMessage += (object _, string message) => OnInformationMessage(message);
        }
    }
}