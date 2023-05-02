namespace Hst.Imager.Core.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core;
    using Commands;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class GivenInfoCommand : CommandTestBase
    {
        [Fact]
        public async Task WhenReadInfoFromImgThenDiskInfoIsReturned()
        {
            // arrange
            var imgPath = $"{Guid.NewGuid()}.img";
            var testCommandHelper = new TestCommandHelper();

            // arrange - create img media
            testCommandHelper.AddTestMedia(imgPath, ImageSize);

            // arrange - info command
            var cancellationTokenSource = new CancellationTokenSource();
            var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper, 
                Enumerable.Empty<IPhysicalDrive>(), imgPath);
            MediaInfo mediaInfo = null;
            infoCommand.DiskInfoRead += (_, args) =>
            {
                mediaInfo = args.MediaInfo;
            };

            // act - read info
            var result = await infoCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - media info is not null and matches disk size matches image size
            Assert.NotNull(mediaInfo);
            Assert.NotNull(mediaInfo.DiskInfo);
            Assert.Empty(mediaInfo.DiskInfo.PartitionTables);
        }
        
        [Fact]
        public async Task WhenReadInfoFromImgWithRigidDiskBlockThenDiskInfoIsReturned()
        {
            // arrange
            var imgPath = $"{Guid.NewGuid()}.img";
            var testCommandHelper = new TestCommandHelper();

            // arrange - create img media
            testCommandHelper.AddTestMedia(imgPath);
            await testCommandHelper.WriteMediaData(imgPath,
                await File.ReadAllBytesAsync(Path.Combine("TestData", "rigid-disk-block.img")));
            
            // arrange - info command
            var cancellationTokenSource = new CancellationTokenSource();
            var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper,
                Enumerable.Empty<IPhysicalDrive>(), imgPath);
            MediaInfo mediaInfo = null;
            infoCommand.DiskInfoRead += (_, args) =>
            {
                mediaInfo = args.MediaInfo;
            };

            // act - read info
            var result = await infoCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - media info is not null
            Assert.NotNull(mediaInfo);
            Assert.NotNull(mediaInfo.DiskInfo);
            Assert.Single(mediaInfo.DiskInfo.PartitionTables);
            Assert.Single(mediaInfo.DiskInfo.PartitionTables.Where(x => x.Type == PartitionTableType.RigidDiskBlock));
        }
    }
}