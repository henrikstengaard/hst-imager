using Hst.Amiga.Extensions;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Core.Extensions;

namespace Hst.Imager.Core.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
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
                [], imgPath, false);
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
                [], imgPath, false);
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

            try
            {
                var testCommandHelper = new TestCommandHelper();

                // arrange - xz compressed img media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.xz"), imgPath);

                // arrange - info command
                var cancellationTokenSource = new CancellationTokenSource();
                var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper,
                    [], imgPath, false);
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

            try
            {
                using var testCommandHelper = new TestCommandHelper();

                // arrange - gz compressed img media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.gz"), imgPath);

                // arrange - info command
                var cancellationTokenSource = new CancellationTokenSource();
                var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper,
                    [], imgPath, false);
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

            try
            {
                var testCommandHelper = new TestCommandHelper();

                // arrange - zip compressed img media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.zip"), imgPath);

                // arrange - info command
                var cancellationTokenSource = new CancellationTokenSource();
                var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper,
                    [], imgPath, false);
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

            try
            {
                using var testCommandHelper = new TestCommandHelper();

                // arrange - gz compressed vhd media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.vhd.gz"), imgPath);

                // arrange - info command
                var cancellationTokenSource = new CancellationTokenSource();
                var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper,
                    [], imgPath, false);
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

            try
            {
                var testCommandHelper = new TestCommandHelper();

                // arrange - zip compressed vhd media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.vhd.zip"), imgPath);

                // arrange - info command
                var cancellationTokenSource = new CancellationTokenSource();
                var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper,
                    [], imgPath, false);
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
        public async Task When_ReadInfoFromMbrPiStormRdbPartition_Then_DiskInfoIsRead()
        {
            // arrange - img and info paths
            var imgPath = $"{Guid.NewGuid()}.img";
            var infoPath = Path.Combine($"{imgPath}", "mbr", "1");

            // arrange - create test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create rigid disk block
            byte[] rdbBytes;
            using (var rdbStream = new MemoryStream())
            {
                var rdb = RigidDiskBlock
                    .Create(80.MB())
                    .AddFileSystem("PDS3", TestHelper.Pfs3AioBytes)
                    .AddPartition("DH0", 80.MB());

                await RigidDiskBlockWriter.WriteBlock(rdb, rdbStream);
                rdbBytes = rdbStream.ToArray();
            }

            // arrange - create img media
            testCommandHelper.AddTestMedia(imgPath, 100.MB());
            
            // arrange - create mbr disk
            await TestHelper.CreateMbrDisk(testCommandHelper, imgPath, 100.MB());
            
            // arrange - add mbr disk partition for pistorm
            await TestHelper.AddMbrDiskPartition(testCommandHelper,  imgPath, 100.MB(), biosType: 0x76);

            // arrange - write rigid disk block to mbr partition
            var mbrPartitionPart = await TestHelper.GetMbrPartitionPart(testCommandHelper, imgPath, 1);
            await TestHelper.WriteData(testCommandHelper, imgPath, mbrPartitionPart.StartOffset, rdbBytes);
            
            // arrange - info command
            var infoCommand = new InfoCommand(new NullLogger<InfoCommand>(), testCommandHelper,
                [], infoPath, false);
            MediaInfo mediaInfo = null;
            infoCommand.DiskInfoRead += (_, args) => { mediaInfo = args.MediaInfo; };

            // act - read info
            var result = await infoCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);
            
            // assert
            Assert.NotNull(mediaInfo);
            Assert.Null(mediaInfo.DiskInfo.MbrPartitionTablePart);
            Assert.Null(mediaInfo.DiskInfo.GptPartitionTablePart);
            Assert.NotNull(mediaInfo.DiskInfo.RdbPartitionTablePart);
            var rdbPartitionPart = mediaInfo.DiskInfo.RdbPartitionTablePart.Parts.FirstOrDefault(
                x=> x.FileSystem == "PDS\\3" && x.PartitionNumber == 1);
            Assert.NotNull(rdbPartitionPart);
        }
    }
}