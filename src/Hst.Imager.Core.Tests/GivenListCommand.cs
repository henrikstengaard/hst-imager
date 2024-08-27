namespace Hst.Imager.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Commands;
    using Models;
    using PhysicalDrives;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class GivenListCommand : CommandTestBase
    {
        [Fact]
        public async Task WhenListPhysicalDrivesThenListReadIsTriggered()
        {
            var physicalDrives = new[]
            {
                new TestPhysicalDrive("Path", "Type", "Model", 8192)
            };
            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();
            
            var listCommand = new ListCommand(new NullLogger<ListCommand>(), fakeCommandHelper, physicalDrives, false);
            IEnumerable<MediaInfo> mediaInfos = null;
            listCommand.ListRead += (_, args) =>
            {
                mediaInfos = args?.MediaInfos;
            };
            var result = await listCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            var mediaInfosList = mediaInfos.ToList();
            Assert.Single(mediaInfosList);

            var mediaInfo = mediaInfosList.First();
            
            Assert.Equal("Path", mediaInfo.Path);
            Assert.Equal(Media.MediaType.Raw, mediaInfo.Type);
            Assert.True(mediaInfo.IsPhysicalDrive);
            Assert.Equal("Model", mediaInfo.Name);
            Assert.Equal(8192, mediaInfo.DiskSize);
        }
        
        [Fact]
        public async Task WhenListPhysicalDrivesWithRigidDiskBlockThenListReadIsTriggered()
        {
            var path = $"{Guid.NewGuid()}.img";

            try
            {
                File.Copy(Path.Combine("TestData", "rigid-disk-block.img"), path, true);

                var physicalDrives = new[]
                {
                new TestPhysicalDrive(path, "Type", "Model", await File.ReadAllBytesAsync(path))
            };
                var testCommandHelper = new TestCommandHelper();
                var cancellationTokenSource = new CancellationTokenSource();

                var listCommand = new ListCommand(new NullLogger<ListCommand>(), testCommandHelper, physicalDrives, false);
                IEnumerable<MediaInfo> mediaInfos = null;
                listCommand.ListRead += (_, args) =>
                {
                    mediaInfos = args?.MediaInfos;
                };
                var result = await listCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                var mediaInfosList = mediaInfos.ToList();
                Assert.Single(mediaInfosList);

                var mediaInfo = mediaInfosList.First();

                Assert.Equal(path, mediaInfo.Path);
                Assert.Equal(Media.MediaType.Raw, mediaInfo.Type);
                Assert.True(mediaInfo.IsPhysicalDrive);
                Assert.Equal("Model", mediaInfo.Name);
                Assert.Equal(131072, mediaInfo.DiskSize);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}