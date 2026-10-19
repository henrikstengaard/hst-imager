using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.RdbCommandTests
{
    public class GivenRdbResizeCommand : FsCommandTestBase
    {
        [Fact]
        public async Task When_ResizingRdbWithAnySize_Then_RdbIsResizedToDisk()
        {
            // arrange - path and disk size
            var imgPath = $"rdb-{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB().ToSectorSize();
            var rdbSize = 20.MB();

            // arrange - cantest command helper
            var testCommandHelper = new TestCommandHelper();

            // arange - add disk media
            testCommandHelper.AddTestMedia(imgPath, 0);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            // arrange - pfs3 formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, imgPath, rdbSize, create: false);

            // arrange - rdb resize command
            var rdbResizeCommand = new RdbResizeCommand(new NullLogger<RdbResizeCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), imgPath, new Size());

            // act - execute rdb resize command
            var result = await rdbResizeCommand.Execute(CancellationToken.None);
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);

            // assert - read disk info
            var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
            Assert.True(mediaResult.IsSuccess);
            using var media = mediaResult.Value;
            var diskInfo = await testCommandHelper.ReadDiskInfo(media);

            // assert - resized rdb size is equal to disk size with an allowed margin of 512000 bytes
            var margin = 512000;
            Assert.True(diskInfo.RdbPartitionTablePart.Size > diskInfo.Size - margin &&
                diskInfo.RdbPartitionTablePart.Size < diskInfo.Size + margin);
        }

        [Fact]
        public async Task When_ResizingRdbWith50PercentSize_Then_RdbIsResizedTo50PercentOfDisk()
        {
            // arrange - path and disk size
            var imgPath = $"rdb-{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB().ToSectorSize();
            var rdbSize = 20.MB();

            // arrange - cantest command helper
            var testCommandHelper = new TestCommandHelper();

            // arange - add disk media
            testCommandHelper.AddTestMedia(imgPath, 0);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            // arrange - pfs3 formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, imgPath, rdbSize, create: false);

            // arrange - rdb resize command
            var rdbResizeCommand = new RdbResizeCommand(new NullLogger<RdbResizeCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), imgPath, new Size(50, Unit.Percent));

            // act - execute rdb resize command
            var result = await rdbResizeCommand.Execute(CancellationToken.None);
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);

            // assert - read disk info
            var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
            Assert.True(mediaResult.IsSuccess);
            using var media = mediaResult.Value;
            var diskInfo = await testCommandHelper.ReadDiskInfo(media);

            // assert - resized rdb size is equal to 50% of disk size with an allowed margin of 512000 bytes
            var margin = 512000;
            Assert.True(diskInfo.RdbPartitionTablePart.Size > (diskInfo.Size / 2) - margin &&
                diskInfo.RdbPartitionTablePart.Size < (diskInfo.Size / 2) + margin);
        }

        [Fact]
        public async Task When_ResizingRdbWithSizeLargerThanDisk_Then_RdbIsResizedDisk()
        {
            // arrange - path and disk size
            var imgPath = $"rdb-{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB().ToSectorSize();
            var rdbSize = 20.MB();

            // arrange - cantest command helper
            var testCommandHelper = new TestCommandHelper();

            // arange - add disk media
            testCommandHelper.AddTestMedia(imgPath, 0);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            // arrange - pfs3 formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, imgPath, rdbSize, create: false);

            // arrange - rdb resize command
            var rdbResizeCommand = new RdbResizeCommand(new NullLogger<RdbResizeCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), imgPath, new Size(500.MB(), Unit.Bytes));

            // act - execute rdb resize command
            var result = await rdbResizeCommand.Execute(CancellationToken.None);
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);

            // assert - read disk info
            var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
            Assert.True(mediaResult.IsSuccess);
            using var media = mediaResult.Value;
            var diskInfo = await testCommandHelper.ReadDiskInfo(media);

            // assert - resized rdb size is equal to disk size with an allowed margin of 512000 bytes
            var margin = 512000;
            Assert.True(diskInfo.RdbPartitionTablePart.Size > diskSize - margin &&
                diskInfo.RdbPartitionTablePart.Size < diskSize + margin);
        }

        [Fact]
        public async Task When_ResizingRdbTo50PercentOfRdbSize_Then_RdbIsResizedTo50Percent()
        {
            // arrange - path and disk size
            var imgPath = $"rdb-{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB().ToSectorSize();
            var rdbSize = 20.MB().ToSectorSize();

            // arrange - cantest command helper
            var testCommandHelper = new TestCommandHelper();

            // arange - add disk media
            testCommandHelper.AddTestMedia(imgPath, 0);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            // arrange - pfs3 formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, imgPath, rdbSize, create: false);

            // arrange - rdb resize command
            var rdbResizeCommand = new RdbResizeCommand(new NullLogger<RdbResizeCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), imgPath, new Size(rdbSize / 2, Unit.Bytes));

            // act - execute rdb resize command
            var result = await rdbResizeCommand.Execute(CancellationToken.None);
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);

            // assert - read disk info
            var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
            Assert.True(mediaResult.IsSuccess);
            using var media = mediaResult.Value;
            var diskInfo = await testCommandHelper.ReadDiskInfo(media);

            // assert - resized rdb size is equal to rdb size with an allowed margin of 512000 bytes,
            // since partition uses entire rdb size
            var expectedRdbSize = rdbSize / 2;
            var margin = 2.MB();
            Assert.True(diskInfo.RdbPartitionTablePart.Size > expectedRdbSize - margin &&
                diskInfo.RdbPartitionTablePart.Size < expectedRdbSize + margin);
        }

        [Fact]
        public async Task When_ResizingRdbWithSize_Then_RdbIsNotLargerThanSize()
        {
            // arrange - path and disk size
            var imgPath = $"rdb-{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB().ToSectorSize();
            var rdbSize = 20.MB();
            var newRdbSize = 50.MB();

            // arrange - cantest command helper
            var testCommandHelper = new TestCommandHelper();

            // arange - add disk media
            testCommandHelper.AddTestMedia(imgPath, 0);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            // arrange - pfs3 formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, imgPath, rdbSize, create: false);

            // arrange - rdb resize command
            var rdbResizeCommand = new RdbResizeCommand(new NullLogger<RdbResizeCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), imgPath, new Size(newRdbSize, Unit.Bytes));

            // act - execute rdb resize command
            var result = await rdbResizeCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - read disk info
            var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
            Assert.True(mediaResult.IsSuccess);
            using var media = mediaResult.Value;
            var diskInfo = await testCommandHelper.ReadDiskInfo(media);

            // assert - resized rdb size is not larger than size and less than a cylinder smaller
            var cylinderSize = (long)diskInfo.RigidDiskBlock.Heads * diskInfo.RigidDiskBlock.Sectors *
                               diskInfo.RigidDiskBlock.BlockSize;
            Assert.True(diskInfo.RdbPartitionTablePart.Size <= newRdbSize);
            Assert.True(diskInfo.RdbPartitionTablePart.Size > newRdbSize - cylinderSize);
        }

        [Fact]
        public async Task When_ExpandingRdbOverMbrPartitionInHybridDisk_Then_ErrorIsReturned()
        {
            // arrange - path and disk size
            var imgPath = $"rdb-{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB().ToSectorSize();

            // arrange - hybrid disk with rigid disk block of 30mb and master boot record partition at 60mb
            var testCommandHelper = await CreateHybridDisk(imgPath, diskSize, 30.MB(), 60.MB());

            // arrange - rdb resize command expanding rigid disk block over master boot record partition
            var rdbResizeCommand = new RdbResizeCommand(new NullLogger<RdbResizeCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), imgPath, new Size(80.MB(), Unit.Bytes));

            // act - execute rdb resize command
            var result = await rdbResizeCommand.Execute(CancellationToken.None);

            // assert - resize failed and rigid disk block is not resized
            Assert.True(result.IsFaulted);
            var diskInfo = await ReadDiskInfo(testCommandHelper, imgPath);
            Assert.True(diskInfo.RdbPartitionTablePart.Size <= 30.MB());
        }

        [Fact]
        public async Task When_ExpandingRdbBeforeMbrPartitionInHybridDisk_Then_RdbIsResized()
        {
            // arrange - path and disk size
            var imgPath = $"rdb-{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB().ToSectorSize();

            // arrange - hybrid disk with rigid disk block of 30mb and master boot record partition at 60mb
            var testCommandHelper = await CreateHybridDisk(imgPath, diskSize, 30.MB(), 60.MB());

            // arrange - rdb resize command expanding rigid disk block up to master boot record partition
            var rdbResizeCommand = new RdbResizeCommand(new NullLogger<RdbResizeCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), imgPath, new Size(60.MB(), Unit.Bytes));

            // act - execute rdb resize command
            var result = await rdbResizeCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - rigid disk block is expanded up to master boot record partition, which is kept
            var diskInfo = await ReadDiskInfo(testCommandHelper, imgPath);
            Assert.True(diskInfo.RdbPartitionTablePart.Size > 30.MB());
            Assert.True(diskInfo.RdbPartitionTablePart.Size <= 60.MB());
            Assert.Single(diskInfo.MbrPartitionTablePart.Parts, x => x.PartType == PartType.Partition);
        }

        private static async Task<TestCommandHelper> CreateHybridDisk(string imgPath, long diskSize, long rdbSize,
            long mbrPartitionStart)
        {
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(imgPath, 0);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            Assert.True((await new MbrInitCommand(new NullLogger<MbrInitCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), imgPath).Execute(CancellationToken.None)).IsSuccess);
            Assert.True((await new MbrPartAddCommand(new NullLogger<MbrPartAddCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), imgPath, "Fat32", new Size(20.MB(), Unit.Bytes),
                mbrPartitionStart / 512, null).Execute(CancellationToken.None)).IsSuccess);
            Assert.True((await new RdbInitCommand(new NullLogger<RdbInitCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), imgPath, "HstImager", new Size(rdbSize, Unit.Bytes), string.Empty, 2)
                .Execute(CancellationToken.None)).IsSuccess);

            return testCommandHelper;
        }

        private static async Task<DiskInfo> ReadDiskInfo(TestCommandHelper testCommandHelper, string imgPath)
        {
            var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
            Assert.True(mediaResult.IsSuccess);
            using var media = mediaResult.Value;
            return await testCommandHelper.ReadDiskInfo(media);
        }
    }
}