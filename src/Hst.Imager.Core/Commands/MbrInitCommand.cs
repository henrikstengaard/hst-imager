using System;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DiscUtils.Partitions;
    using DiscUtils.Raw;
    using DiscUtils.Streams;
    using Hst.Core;
    using Microsoft.Extensions.Logging;

    public class MbrInitCommand : CommandBase
    {
        private readonly ILogger<MbrInitCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;

        public MbrInitCommand(ILogger<MbrInitCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage($"Initializing Master Boot Record at '{path}'");

            OnDebugMessage($"Opening '{path}' as writable");

            var physicalDrivesList = physicalDrives.ToList();
            var mediaResult = commandHelper.GetWritableMedia(physicalDrivesList, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }
            using var media = mediaResult.Value;
            
            using var disk = media is DiskMedia diskMedia
                ? diskMedia.Disk
                : new Disk(media.Stream, Ownership.Dispose);

            var deleteSectorCount = 0L;

            try
            {
                var biosPartitionTable = new BiosPartitionTable(disk);
                deleteSectorCount = 1;
            }
            catch (Exception)
            {
                // ignored, if bios partition table doesnt exist
            }

            try
            {
                var guidPartitionTable = new GuidPartitionTable(disk);
                if (guidPartitionTable.FirstUsableSector - 1 > deleteSectorCount)
                {
                    deleteSectorCount = guidPartitionTable.FirstUsableSector;
                }
            }
            catch (Exception)
            {
                // ignored, if guid partition table doesnt exist
            }
            
            if (deleteSectorCount > 0)
            {
                OnDebugMessage($"Deleting sector{(deleteSectorCount == 1 ? $" {deleteSectorCount}" : $"s 0-{deleteSectorCount - 1}")}");
                
                var blankSectorBytes = new byte[disk.SectorSize];
                var offset = 0;
                for (var i = 0; i < deleteSectorCount; i++)
                {
                    disk.Content.Position = offset;
                    await disk.Content.WriteAsync(blankSectorBytes, 0, blankSectorBytes.Length, token);
                    offset += disk.SectorSize;
                }
            }
            
            OnDebugMessage("Initializing Master Boot Record");

            // initialize mbr
            BiosPartitionTable.Initialize(disk);

            return new Result();
        }
    }
}