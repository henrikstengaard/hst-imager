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
            await using var stream = media.Stream;
            
            using var disk = new Disk(stream, Ownership.None);
            
            OnDebugMessage("Initializing Master Boot Record");

            BiosPartitionTable.Initialize(disk);

            await disk.Content.DisposeAsync();
            disk.Dispose();
            
            return new Result();
        }
    }
}