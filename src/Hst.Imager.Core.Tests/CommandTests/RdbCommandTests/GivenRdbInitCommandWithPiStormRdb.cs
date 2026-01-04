using Hst.Core.Extensions;
using Hst.Imager.Core.Models;
using System.IO;
using System;
using System.Threading.Tasks;
using Xunit;
using DiscUtils.Partitions;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Threading;

namespace Hst.Imager.Core.Tests.CommandTests.RdbCommandTests
{
    public class GivenRdbInitCommandWithPiStormRdb : FsCommandTestBase
    {
        [Fact]
        public async Task When_InitializingTwoPiStormRdbs_Then_PiStormRdbsAreInitialized()
        {
            // arrange - paths
            var mbrDiskPath = $"mbr_{Guid.NewGuid()}.vhd";
            var piStormRdbPartition1Path = Path.Combine(mbrDiskPath, "mbr", "2");
            var piStormRdbPartition2Path = Path.Combine(mbrDiskPath, "mbr", "3");

            // arrange - cantest command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create mdr disk
            await CreateMbrDiskWithFat16And2PiStormRdbPartitions(testCommandHelper, mbrDiskPath);

            // act - create execute rdb init command for 1st pistorm partition
            var rdbInitCommand = new RdbInitCommand(
                new NullLogger<RdbInitCommand>(),
                testCommandHelper,
                new List<IPhysicalDrive>(),
                piStormRdbPartition1Path,
                "Test",
                new Size(50.MB(), Unit.Bytes),
                null,
                0);

            // act - execute rdb init command for 1st pistorm partition
            var result = await rdbInitCommand.Execute(CancellationToken.None);
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);

            // act - create execute rdb init command for 2nd pistorm partition
            rdbInitCommand = new RdbInitCommand(
                new NullLogger<RdbInitCommand>(),
                testCommandHelper,
                new List<IPhysicalDrive>(),
                piStormRdbPartition2Path,
                "Test",
                new Size(),
                null,
                0);

            // act - execute rdb init command for 2nd pistorm partition
            result = await rdbInitCommand.Execute(CancellationToken.None);
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
        }

        private async Task CreateMbrDiskWithFat16And2PiStormRdbPartitions(TestCommandHelper testCommandHelper, string mbrDiskPath)
        {
            // disk sizes
            var mbrDiskSize = 100.MB();

            // add mbr disk media
            testCommandHelper.AddTestMedia(mbrDiskPath, 0);

            // calculate mbr parttion start and end sectors
            var mbrPartition1StartSector = 63;
            var mbrPartition1EndSector = mbrPartition1StartSector + 16384;
            var mbrPartition2StartSector = mbrPartition1EndSector + 1;
            var mbrPartition2EndSector = mbrPartition2StartSector + (mbrDiskSize / 1024);
            var mbrPartition3StartSector = mbrPartition2EndSector + 1;
            var mbrPartition3EndSector = (mbrDiskSize / 512) - 10;

            // mbr disk
            await CreateMbrDisk(testCommandHelper, mbrDiskPath, mbrDiskSize);
            await AddMbrPartition(testCommandHelper, mbrDiskPath,
                mbrPartition1StartSector, mbrPartition1EndSector, BiosPartitionTypes.Fat16);
            await AddMbrPartition(testCommandHelper, mbrDiskPath,
                mbrPartition2StartSector, mbrPartition2EndSector, Constants.BiosPartitionTypes.PiStormRdb);
            await AddMbrPartition(testCommandHelper, mbrDiskPath,
                mbrPartition3StartSector, mbrPartition3EndSector, Constants.BiosPartitionTypes.PiStormRdb);
        }
    }
}