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
            testCommandHelper.AddTestMediaWithData(imgPath, ImageSize);

            // arrange - info command
            var cancellationTokenSource = new CancellationTokenSource();
            var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper,
                Enumerable.Empty<IPhysicalDrive>(), imgPath);
            MediaInfo mediaInfo = null;
            infoCommand.DiskInfoRead += (_, args) => { mediaInfo = args.MediaInfo; };

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
            testCommandHelper.AddTestMedia(imgPath, ImageSize);
            await testCommandHelper.WriteMediaData(imgPath,
                await File.ReadAllBytesAsync(Path.Combine("TestData", "rigid-disk-block.img")));

            // arrange - info command
            var cancellationTokenSource = new CancellationTokenSource();
            var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper,
                Enumerable.Empty<IPhysicalDrive>(), imgPath);
            MediaInfo mediaInfo = null;
            infoCommand.DiskInfoRead += (_, args) => { mediaInfo = args.MediaInfo; };

            // act - read info
            var result = await infoCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - media info is not null
            Assert.NotNull(mediaInfo);
            Assert.NotNull(mediaInfo.DiskInfo);
            Assert.Single(mediaInfo.DiskInfo.PartitionTables);
            Assert.Single(mediaInfo.DiskInfo.PartitionTables, x => x.Type == PartitionTableType.RigidDiskBlock);
        }

        [Fact]
        public async Task WhenReadInfoFromXzCompressedImgThenDiskInfoMatches()
        {
            // arrange
            var imgPath = $"{Guid.NewGuid()}.img.xz";
            var testCommandHelper = new TestCommandHelper();

            try
            {
                // arrange - xz compressed img media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.xz"), imgPath);

                // arrange - info command
                var cancellationTokenSource = new CancellationTokenSource();
                var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper,
                    Enumerable.Empty<IPhysicalDrive>(), imgPath);
                MediaInfo mediaInfo = null;
                infoCommand.DiskInfoRead += (_, args) => { mediaInfo = args.MediaInfo; };

                // act - read info
                var result = await infoCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - media info matches disk size and partitions
                Assert.NotNull(mediaInfo);
                Assert.Equal(1020055040, mediaInfo.DiskSize);
                Assert.NotNull(mediaInfo.DiskInfo);
                Assert.Equal(1020055040, mediaInfo.DiskInfo.Size);
                var partitionTable =
                    mediaInfo.DiskInfo.PartitionTables.FirstOrDefault(x => x.Type == PartitionTableType.RigidDiskBlock);
                Assert.NotNull(partitionTable);
                Assert.Equal(2, partitionTable.Partitions.Count());
            }
            finally
            { 
                if (File.Exists(imgPath))
                {
                    File.Delete(imgPath);
                }
            }
        }

        [Fact]
        public async Task WhenReadInfoFromGZipCompressedImgThenDiskInfoMatches()
        {
            // arrange
            var imgPath = $"{Guid.NewGuid()}.img.gz";
            var testCommandHelper = new TestCommandHelper();

            try
            {
                // arrange - gz compressed img media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.gz"), imgPath);

                // arrange - info command
                var cancellationTokenSource = new CancellationTokenSource();
                var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper,
                    Enumerable.Empty<IPhysicalDrive>(), imgPath);
                MediaInfo mediaInfo = null;
                infoCommand.DiskInfoRead += (_, args) => { mediaInfo = args.MediaInfo; };

                // act - read info
                var result = await infoCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - media info matches disk size and partitions
                Assert.NotNull(mediaInfo);
                Assert.Equal(1020055040, mediaInfo.DiskSize);
                Assert.NotNull(mediaInfo.DiskInfo);
                Assert.Equal(1020055040, mediaInfo.DiskInfo.Size);
                var partitionTable =
                    mediaInfo.DiskInfo.PartitionTables.FirstOrDefault(x => x.Type == PartitionTableType.RigidDiskBlock);
                Assert.NotNull(partitionTable);
                Assert.Equal(2, partitionTable.Partitions.Count());
            }
            finally
            {
                if (File.Exists(imgPath))
                {
                    File.Delete(imgPath);
                }
            }
        }
        
        [Fact]
        public async Task WhenReadInfoFromZipCompressedImgThenDiskInfoMatches()
        {
            // arrange
            var imgPath = $"{Guid.NewGuid()}.img.zip";
            var testCommandHelper = new TestCommandHelper();

            try
            {
                // arrange - zip compressed img media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.zip"), imgPath);

                // arrange - info command
                var cancellationTokenSource = new CancellationTokenSource();
                var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper,
                    Enumerable.Empty<IPhysicalDrive>(), imgPath);
                MediaInfo mediaInfo = null;
                infoCommand.DiskInfoRead += (_, args) => { mediaInfo = args.MediaInfo; };

                // act - read info
                var result = await infoCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - media info matches disk size and partitions
                Assert.NotNull(mediaInfo);
                Assert.Equal(1020055040, mediaInfo.DiskSize);
                Assert.NotNull(mediaInfo.DiskInfo);
                Assert.Equal(1020055040, mediaInfo.DiskInfo.Size);
                var partitionTable =
                    mediaInfo.DiskInfo.PartitionTables.FirstOrDefault(x => x.Type == PartitionTableType.RigidDiskBlock);
                Assert.NotNull(partitionTable);
                Assert.Equal(2, partitionTable.Partitions.Count());
            }
            finally
            {
                if (File.Exists(imgPath))
                {
                    File.Delete(imgPath);
                }
            }
        }
        
        [Fact]
        public async Task WhenReadInfoFromGZipCompressedVhdThenDiskInfoMatches()
        {
            // arrange
            var imgPath = $"{Guid.NewGuid()}.vhd.gz";
            var testCommandHelper = new TestCommandHelper();

            try
            {
                // arrange - gz compressed vhd media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.vhd.gz"), imgPath);

                // arrange - info command
                var cancellationTokenSource = new CancellationTokenSource();
                var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper,
                    Enumerable.Empty<IPhysicalDrive>(), imgPath);
                MediaInfo mediaInfo = null;
                infoCommand.DiskInfoRead += (_, args) => { mediaInfo = args.MediaInfo; };

                // act - read info
                var result = await infoCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - media info matches disk size and partitions
                Assert.NotNull(mediaInfo);
                Assert.Equal(1020055040, mediaInfo.DiskSize);
                Assert.NotNull(mediaInfo.DiskInfo);
                Assert.Equal(1020055040, mediaInfo.DiskInfo.Size);
                var partitionTable =
                    mediaInfo.DiskInfo.PartitionTables.FirstOrDefault(x => x.Type == PartitionTableType.RigidDiskBlock);
                Assert.NotNull(partitionTable);
                Assert.Equal(2, partitionTable.Partitions.Count());
            }
            finally
            {
                if (File.Exists(imgPath))
                {
                    File.Delete(imgPath);
                }
            }
        }

        [Fact]
        public async Task WhenReadInfoFromZipCompressedVhdThenDiskInfoMatches()
        {
            // arrange
            var imgPath = $"{Guid.NewGuid()}.vhd.zip";
            var testCommandHelper = new TestCommandHelper();

            try
            {
                // arrange - zip compressed vhd media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.vhd.zip"), imgPath);

                // arrange - info command
                var cancellationTokenSource = new CancellationTokenSource();
                var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper,
                    Enumerable.Empty<IPhysicalDrive>(), imgPath);
                MediaInfo mediaInfo = null;
                infoCommand.DiskInfoRead += (_, args) => { mediaInfo = args.MediaInfo; };

                // act - read info
                var result = await infoCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - media info matches disk size and partitions
                Assert.NotNull(mediaInfo);
                Assert.Equal(1020055040, mediaInfo.DiskSize);
                Assert.NotNull(mediaInfo.DiskInfo);
                Assert.Equal(1020055040, mediaInfo.DiskInfo.Size);
                var partitionTable =
                    mediaInfo.DiskInfo.PartitionTables.FirstOrDefault(x => x.Type == PartitionTableType.RigidDiskBlock);
                Assert.NotNull(partitionTable);
                Assert.Equal(2, partitionTable.Partitions.Count());
            }
            finally
            {
                if (File.Exists(imgPath))
                {
                    File.Delete(imgPath);
                }
            }
        }
    }
}