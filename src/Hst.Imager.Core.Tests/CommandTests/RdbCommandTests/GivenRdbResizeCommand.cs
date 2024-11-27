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
            testCommandHelper.AddTestMedia(imgPath, diskSize);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            // arrange - pfs3 formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, imgPath, rdbSize, false);

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
            testCommandHelper.AddTestMedia(imgPath, diskSize);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            // arrange - pfs3 formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, imgPath, rdbSize, false);

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
            testCommandHelper.AddTestMedia(imgPath, diskSize);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            // arrange - pfs3 formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, imgPath, rdbSize, false);

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
        public async Task When_ResizingRdbWithSizeSmallerThanRdb_Then_RdbIsResizedTo50PercentOfDisk()
        {
            // arrange - path and disk size
            var imgPath = $"rdb-{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB().ToSectorSize();
            var rdbSize = 20.MB().ToSectorSize();

            // arrange - cantest command helper
            var testCommandHelper = new TestCommandHelper();

            // arange - add disk media
            testCommandHelper.AddTestMedia(imgPath, diskSize);
            await testCommandHelper.GetWritableFileMedia(imgPath, size: diskSize, create: true);

            // arrange - pfs3 formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, imgPath, rdbSize, false);

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
            var margin = 512000;
            Assert.True(diskInfo.RdbPartitionTablePart.Size > rdbSize - margin &&
                diskInfo.RdbPartitionTablePart.Size < rdbSize + margin);
        }
    }
}