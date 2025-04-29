using System.Linq;

namespace Hst.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Core;
    using Microsoft.Extensions.Logging;
    using Models;

    public class ListCommand : CommandBase
    {
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;

        public ListCommand(ILogger<ListCommand> logger, ICommandHelper commandHelper, IEnumerable<IPhysicalDrive> physicalDrives)
        {
            this.physicalDrives = physicalDrives;
        }

        public event EventHandler<ListReadEventArgs> ListRead;

        public override Task<Result> Execute(CancellationToken token)
        {
            OnListRead(physicalDrives.Select(x => new MediaInfo
            {
                Path = x.Path,
                Name = x.Name,
                IsPhysicalDrive = true,
                Type = Media.MediaType.Raw,
                DiskSize = x.Size,
                SystemDrive = x.SystemDrive
            }));
            
            return Task.FromResult(new Result());
        }

        private void OnListRead(IEnumerable<MediaInfo> mediaInfos)
        {
            ListRead?.Invoke(this, new ListReadEventArgs(mediaInfos));
        }
    }
}