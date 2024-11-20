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
using SharpCompress.IO;
using System.IO;

namespace Hst.Imager.Core.Commands
{
    public class FormatCommand : CommandBase
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<FormatCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly PartitionTable partitionTable;
        private readonly string fileSystem;

        public FormatCommand(ILogger<FormatCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, PartitionTable partitionTable, string fileSystem)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.partitionTable = partitionTable;
            this.fileSystem = fileSystem;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Formatting '{path}'");

            OnDebugMessage($"Opening '{path}' as writable");

            var physicalDrivesList = physicalDrives.ToList();
            var mediaResult = await commandHelper.GetWritableMedia(physicalDrivesList, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }
            using var media = mediaResult.Value;

            var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new Disk(media.Stream, Ownership.None);

            OnInformationMessage($"Erasing partition tables");

            var emptyPartitionTableData = new byte[10.MB().ToSectorSize()];
            var emptyPartitionTableStream = new MemoryStream(emptyPartitionTableData);

            var isVhd = commandHelper.IsVhd(path);
            var streamCopier = new StreamCopier();
            await streamCopier.Copy(token, emptyPartitionTableStream, disk.Content, emptyPartitionTableData.Length, 0, 0, isVhd);

            OnInformationMessage($"Erasing partition tables");

            switch(partitionTable)
            {
                case PartitionTable.Mbr:
                    break;
            }

            return new Result();
        }

        private async Task<Result> FormatMbrPartition(CancellationToken cancellationToken)
        {
            var mbrInitCommand = new MbrInitCommand(_loggerFactory.CreateLogger<MbrInitCommand>(), commandHelper, physicalDrives, path);

            var mbrInitResult = await mbrInitCommand.Execute(cancellationToken);
            if (mbrInitResult.IsFaulted)
            {
                return new Result(mbrInitResult.Error);
            }

            long? startSector = 63;
            long? endSector = null;

            var mbrPartAddCommand = new MbrPartAddCommand(_loggerFactory.CreateLogger<MbrPartAddCommand>(), commandHelper, physicalDrives,
                path, fileSystem, new Size(), startSector, endSector, active: true);

            var mbrPartAddResult = await mbrPartAddCommand.Execute(cancellationToken);
            if (mbrPartAddResult.IsFaulted)
            {
                return new Result(mbrPartAddResult.Error);
            }

            var mbrPartFormatCommand = new MbrPartFormatCommand(_loggerFactory.CreateLogger<MbrPartFormatCommand>(), commandHelper, physicalDrives,
                path, 1, fileSystem);

            return await mbrPartFormatCommand.Execute(cancellationToken);
        }
    }
}
