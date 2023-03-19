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

        public override async Task<Result> Execute(CancellationToken token)
        {
            OnInformationMessage("Reading list of physical drives");
            
            var mediaInfos = new List<MediaInfo>();
            foreach (var physicalDrive in physicalDrives)
            {
                OnDebugMessage($"Physical drive size '{physicalDrive.Size}'");
                OnDebugMessage($"Opening physical drive '{physicalDrive.Path}'");

                long diskSize;
                await using (var sourceStream = physicalDrive.Open())
                {
                    var streamSize = sourceStream.Length;
                    diskSize = streamSize is > 0 ? streamSize : physicalDrive.Size;
                }

                OnDebugMessage($"Disk size '{diskSize}'");
                
                mediaInfos.Add(new MediaInfo
                {
                    Path = physicalDrive.Path,
                    Name = physicalDrive.Name,
                    IsPhysicalDrive = true,
                    Type = Media.MediaType.Raw,
                    DiskSize = diskSize
                });
            }

            OnListRead(mediaInfos);

            return new Result();
        }

        private void OnListRead(IEnumerable<MediaInfo> mediaInfos)
        {
            ListRead?.Invoke(this, new ListReadEventArgs(mediaInfos));
        }
    }
}