using Hst.Amiga.RigidDiskBlocks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands.RdbCommands;
using Hst.Imager.Core.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.RdbCommandTests
{
    public class GivenRdbPartMoveCommand : FsCommandTestBase
    {
        [Fact]
        public async Task When_MovingPartitionFromStartCylinderToUnallocatedDiskSpace_Then_PartitionIsMoved()
        {
            // arrange - path and disk size
            var imgPath = $"rdb-{Guid.NewGuid()}.vhd";
            var diskSize = 20.MB().ToSectorSize();
            var partitionSize = 5.MB();

            // arrange - cantest command helper
            var testCommandHelper = new TestCommandHelper();

            // arange - add disk media
            testCommandHelper.AddTestMedia(imgPath, 0);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            // arrange - pfs3 formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, imgPath, diskSize, partitionSize, false);
            await CreatePfs3DirectoriesAndFiles(testCommandHelper, imgPath);

            var readableMediaResult = await testCommandHelper.GetReadableFileMedia(imgPath);
            Assert.True(readableMediaResult.IsSuccess);
            RigidDiskBlock rigidDiskBlock;
            using var mediaBeforeMove = readableMediaResult.Value;
            rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(mediaBeforeMove);
            Assert.NotNull(rigidDiskBlock);
            Assert.Single(rigidDiskBlock.PartitionBlocks);
            var partitionBlock = rigidDiskBlock.PartitionBlocks.First();
            var startCylinder = partitionBlock.LowCyl + 5;
            var endCylinder = partitionBlock.HighCyl + 5;

            // arrange - rdb part move command
            var rdbPartMoveCommand = new RdbPartMoveCommand(new NullLogger<RdbPartMoveCommand>(), testCommandHelper,
                [], imgPath, 1, startCylinder);

            // act - execute rdb resize command
            var result = await rdbPartMoveCommand.Execute(CancellationToken.None);
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);

            // assert - get media
            readableMediaResult = await testCommandHelper.GetReadableFileMedia(imgPath);
            Assert.True(readableMediaResult.IsSuccess);
            using var media = readableMediaResult.Value;

            // assert - partition is moved to start and end cylinder
            rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(media);
            Assert.NotNull(rigidDiskBlock);
            Assert.Single(rigidDiskBlock.PartitionBlocks);
            partitionBlock = rigidDiskBlock.PartitionBlocks.First();
            Assert.Equal(startCylinder, partitionBlock.LowCyl);
            Assert.Equal(endCylinder, partitionBlock.HighCyl);

            // assert - mount pfs3 volume
            var stream = MediaHelper.GetStreamFromMedia(media);
            await using var pfs3Volume = await MountPfs3Volume(stream);

            // assert - get root entries
            var entries = (await pfs3Volume.ListEntries()).ToList();

            // assert - root directory has 3 entries
            Assert.Equal(3, entries.Count);
            Assert.Equal(["dir1", "dir2", "file1.txt"], entries.Select(x => x.Name));
        }

        [Fact]
        public async Task When_MovingPartitionStartCylinderToEndCylinder_Then_PartitionIsMoved()
        {
            // arrange - path and disk size
            var imgPath = $"rdb-{Guid.NewGuid()}.vhd";
            var diskSize = 20.MB().ToSectorSize();
            var partitionSize = 5.MB();

            // arrange - cantest command helper
            var testCommandHelper = new TestCommandHelper();

            // arange - add disk media
            testCommandHelper.AddTestMedia(imgPath, 0);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            // arrange - pfs3 formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, imgPath, diskSize, partitionSize, false);
            await CreatePfs3DirectoriesAndFiles(testCommandHelper, imgPath);

            var readableMediaResult = await testCommandHelper.GetReadableFileMedia(imgPath);
            Assert.True(readableMediaResult.IsSuccess);
            RigidDiskBlock rigidDiskBlock;
            using var mediaBeforeMove = readableMediaResult.Value;
            rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(mediaBeforeMove);
            Assert.NotNull(rigidDiskBlock);
            Assert.Single(rigidDiskBlock.PartitionBlocks);
            var partitionBlock = rigidDiskBlock.PartitionBlocks.First();
            var startCylinder = rigidDiskBlock.HiCylinder - (partitionBlock.HighCyl - partitionBlock.LowCyl);
            var endCylinder = rigidDiskBlock.HiCylinder;

            // arrange - rdb part move command
            var rdbPartMoveCommand = new RdbPartMoveCommand(new NullLogger<RdbPartMoveCommand>(), testCommandHelper,
                [], imgPath, 1, startCylinder);

            // act - execute rdb resize command
            var result = await rdbPartMoveCommand.Execute(CancellationToken.None);
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);

            // assert - get media
            readableMediaResult = await testCommandHelper.GetReadableFileMedia(imgPath);
            Assert.True(readableMediaResult.IsSuccess);
            using var media = readableMediaResult.Value;

            // assert - partition is moved to start and end cylinder
            rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(media);
            Assert.NotNull(rigidDiskBlock);
            Assert.Single(rigidDiskBlock.PartitionBlocks);
            partitionBlock = rigidDiskBlock.PartitionBlocks.First();
            Assert.Equal(startCylinder, partitionBlock.LowCyl);
            Assert.Equal(endCylinder, partitionBlock.HighCyl);

            // assert - mount pfs3 volume
            var stream = MediaHelper.GetStreamFromMedia(media);
            await using var pfs3Volume = await MountPfs3Volume(stream);

            // assert - get root entries
            var entries = (await pfs3Volume.ListEntries()).ToList();

            // assert - root directory has 3 entries
            Assert.Equal(3, entries.Count);
            Assert.Equal(["dir1", "dir2", "file1.txt"], entries.Select(x => x.Name));
        }

        [Fact]
        public async Task When_MovingPartitionFromStartCylinderToCylinderWithAnotherPartition_Then_ErrorIsReturned()
        {
            // arrange - path and disk size
            var imgPath = $"rdb-{Guid.NewGuid()}.vhd";
            var diskSize = 20.MB().ToSectorSize();
            var partitionSize = 5.MB();

            // arrange - cantest command helper
            var testCommandHelper = new TestCommandHelper();

            // arange - add disk media
            testCommandHelper.AddTestMedia(imgPath, 0);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            // arrange - pfs3 formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, imgPath, diskSize, partitionSize, false);
            await AddPfs3FormattedPartition(testCommandHelper, imgPath, "DH1", "Work", 5.MB());

            var readableMediaResult = await testCommandHelper.GetReadableFileMedia(imgPath);
            Assert.True(readableMediaResult.IsSuccess);
            RigidDiskBlock rigidDiskBlock;
            using var mediaBeforeMove = readableMediaResult.Value;
            rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(mediaBeforeMove);
            Assert.NotNull(rigidDiskBlock);
            Assert.Equal(2, rigidDiskBlock.PartitionBlocks.Count());
            var partitionBlock = rigidDiskBlock.PartitionBlocks.First();
            var startCylinder = partitionBlock.LowCyl + 5;

            // arrange - rdb part move command
            var rdbPartMoveCommand = new RdbPartMoveCommand(new NullLogger<RdbPartMoveCommand>(), testCommandHelper,
                [], imgPath, 1, startCylinder);

            // act - execute rdb resize command
            var result = await rdbPartMoveCommand.Execute(CancellationToken.None);
            Assert.NotNull(result);
            Assert.True(result.IsFaulted);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public async Task When_MovingPartitionToCylinderLowerThanStartCylinder_Then_ErrorIsReturned()
        {
            // arrange - path and disk size
            var imgPath = $"rdb-{Guid.NewGuid()}.vhd";
            var diskSize = 20.MB().ToSectorSize();
            var partitionSize = 5.MB();

            // arrange - cantest command helper
            var testCommandHelper = new TestCommandHelper();

            // arange - add disk media
            testCommandHelper.AddTestMedia(imgPath, 0);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            // arrange - pfs3 formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, imgPath, diskSize, partitionSize, false);

            var readableMediaResult = await testCommandHelper.GetReadableFileMedia(imgPath);
            Assert.True(readableMediaResult.IsSuccess);
            RigidDiskBlock rigidDiskBlock;
            using var mediaBeforeMove = readableMediaResult.Value;
            rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(mediaBeforeMove);
            Assert.NotNull(rigidDiskBlock);
            Assert.Single(rigidDiskBlock.PartitionBlocks);
            var startCylinder = rigidDiskBlock.LoCylinder - 1;

            // arrange - rdb part move command
            var rdbPartMoveCommand = new RdbPartMoveCommand(new NullLogger<RdbPartMoveCommand>(), testCommandHelper,
                [], imgPath, 1, startCylinder);

            // act - execute rdb resize command
            var result = await rdbPartMoveCommand.Execute(CancellationToken.None);
            Assert.NotNull(result);
            Assert.True(result.IsFaulted);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public async Task When_MovingPartitionToCylinderHigherThanEndCylinder_Then_ErrorIsReturned()
        {
            // arrange - path and disk size
            var imgPath = $"rdb-{Guid.NewGuid()}.vhd";
            var diskSize = 20.MB().ToSectorSize();
            var partitionSize = 5.MB();

            // arrange - cantest command helper
            var testCommandHelper = new TestCommandHelper();

            // arange - add disk media
            testCommandHelper.AddTestMedia(imgPath, 0);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            // arrange - pfs3 formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, imgPath, diskSize, partitionSize, false);

            var readableMediaResult = await testCommandHelper.GetReadableFileMedia(imgPath);
            Assert.True(readableMediaResult.IsSuccess);
            RigidDiskBlock rigidDiskBlock;
            using var mediaBeforeMove = readableMediaResult.Value;
            rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(mediaBeforeMove);
            Assert.NotNull(rigidDiskBlock);
            Assert.Single(rigidDiskBlock.PartitionBlocks);
            var startCylinder = rigidDiskBlock.HiCylinder + 1;

            // arrange - rdb part move command
            var rdbPartMoveCommand = new RdbPartMoveCommand(new NullLogger<RdbPartMoveCommand>(), testCommandHelper,
                [], imgPath, 1, startCylinder);

            // act - execute rdb resize command
            var result = await rdbPartMoveCommand.Execute(CancellationToken.None);
            Assert.NotNull(result);
            Assert.True(result.IsFaulted);
            Assert.NotNull(result.Error);
        }

        private async Task CreatePfs3DirectoriesAndFiles(TestCommandHelper testCommandHelper, string path)
        {
            var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }

            using var media = mediaResult.Value;
            var stream = media.Stream;

            await using var pfs3Volume = await MountPfs3Volume(stream);
            await pfs3Volume.CreateDirectory("dir1");
            await pfs3Volume.CreateDirectory("dir2");
            await pfs3Volume.CreateFile("file1.txt");
            await pfs3Volume.ChangeDirectory("dir1");
            await pfs3Volume.CreateFile("file2.txt");
        }
    }
}
