namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Core;
    using Microsoft.Extensions.Logging;

    public class RdbInfoCommand : CommandBase
    {
        private readonly ILogger<RdbInfoCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;

        public RdbInfoCommand(ILogger<RdbInfoCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
        }

        public event EventHandler<RdbInfoReadEventArgs> RdbInfoRead;
        
        public override async Task<Result> Execute(CancellationToken token)
        {
            OnProgressMessage($"Opening '{path}' for read");

            var mediaResult = commandHelper.GetReadableMedia(physicalDrives, path, allowPhysicalDrive: true);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            using var media = mediaResult.Value;
            await using var stream = media.Stream;

            OnProgressMessage($"Reading Rigid Disk Block from path '{path}'");
            
            var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);

            if (rigidDiskBlock == null)
            {
                return new Result(new Error("Rigid Disk Block not found"));
            }

            OnRdbInfoRead(new RdbInfo
            {
                Path = path,
                RigidDiskBlock = rigidDiskBlock
            });
            
            return new Result();
        }
        
        protected virtual void OnRdbInfoRead(RdbInfo rdbInfo)
        {
            RdbInfoRead?.Invoke(this, new RdbInfoReadEventArgs(rdbInfo));
        }        
    }
}