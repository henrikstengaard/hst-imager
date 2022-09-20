namespace Hst.Imager.Core.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Imager.Core;
    using Hst.Imager.Core.Commands;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class GivenInfoCommand : CommandTestBase
    {
        [Fact]
        public async Task WhenReadInfoFromSourceImgThenDiskInfoIsReturned()
        {
            // arrange
            var path = $"{Guid.NewGuid()}.img";
            var fakeCommandHelper = new FakeCommandHelper(new[] { path });
            var cancellationTokenSource = new CancellationTokenSource();

            // read info from path
            var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), fakeCommandHelper, Enumerable.Empty<IPhysicalDrive>(), path);
            MediaInfo mediaInfo = null;
            infoCommand.DiskInfoRead += (_, args) =>
            {
                mediaInfo = args.MediaInfo;
            };
            var result = await infoCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - media info is not null and matches disk size matches image size
            Assert.NotNull(mediaInfo);
            Assert.NotNull(mediaInfo.DiskInfo);
            Assert.Empty(mediaInfo.DiskInfo.PartitionTables);
        }
        
        [Fact]
        public async Task WhenReadInfoFromSourceImgWithRigidDiskBlockThenDiskInfoIsReturned()
        {
            // arrange
            var path = Path.Combine("TestData", "rigid-disk-block.img");
            var fakeCommandHelper = new FakeCommandHelper(new[] { path });
            var cancellationTokenSource = new CancellationTokenSource();

            // read info from path
            var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), fakeCommandHelper, Enumerable.Empty<IPhysicalDrive>(), path);
            MediaInfo mediaInfo = null;
            infoCommand.DiskInfoRead += (_, args) =>
            {
                mediaInfo = args.MediaInfo;
            };
            await infoCommand.Execute(cancellationTokenSource.Token);
            
            // assert - media info is not null
            Assert.NotNull(mediaInfo);
            Assert.NotNull(mediaInfo.DiskInfo);
            Assert.Single(mediaInfo.DiskInfo.PartitionTables);
            Assert.Single(mediaInfo.DiskInfo.PartitionTables.Where(x => x.Type == PartitionTableInfo.PartitionTableType.RigidDiskBlock));
        }
    }
}