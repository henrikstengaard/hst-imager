using Hst.Core.Extensions;
using Hst.Imager.Core.Commands.MbrCommands;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.MbrCommandTests
{
    public class GivenMbrPartExportCommand : FsCommandTestBase
    {
        [Fact]
        public async Task When_ExportingSrcImagePartitionToDestFile_Then_DestFileHasDataExported()
        {
            // arrange - path, size and test command helper
            var srcPath = $"mbr-disk.img";
            var destPath = "mbr-part2.img";
            var testCommandHelper = new TestCommandHelper();

            // arrange - sector size and mbr partition sectors and offsets
            const int sectorSize = 512;
            const int mbrPartition1StartSector = 63;
            const int mbrPartition1EndSector = 5000;
            const int mbrPartition2StartSector = 5001;
            const int mbrPartition2EndSector = 8000;            
            var mbrPartition2Sectors = mbrPartition2EndSector - mbrPartition2StartSector + 1;
            var mbrPartition2StartOffset = mbrPartition2StartSector * sectorSize;
            const string partitionNumberExportFrom = "2";

            // arrange - create source image data
            var srcImageSize = 10.MB();
            var srcImageDataBytes = TestDataHelper.CreateTestData(srcImageSize);

            // arrange - create source partition data
            var srcPartitionData = new byte[mbrPartition2Sectors * sectorSize];
            for(var i = 0; i < mbrPartition2Sectors; i++)
            {
                Array.Fill(srcPartitionData, (byte)((i + 1) % 256), i * sectorSize, sectorSize);
            }

            // arrange - create source and partition test medias
            testCommandHelper.AddTestMedia(srcPath, srcImageSize);
            await testCommandHelper.AddTestMedia(destPath);

            // arrange - write source image data
            using (var srcStream = testCommandHelper.GetTestMedia(srcPath).Stream)
            {
                await srcStream.WriteBytes(srcImageDataBytes);
                srcStream.Seek(mbrPartition2StartOffset, System.IO.SeekOrigin.Begin);
                await srcStream.WriteBytes(srcPartitionData);
            }

            // arrange - create source mbr disk and partitions
            await CreateMbrDisk(testCommandHelper, srcPath, srcImageSize);
            await AddMbrPartition(testCommandHelper, srcPath,
                mbrPartition1StartSector, mbrPartition1EndSector);
            await AddMbrPartition(testCommandHelper, srcPath,
                mbrPartition2StartSector, mbrPartition2EndSector, Constants.BiosPartitionTypes.PiStormRdb);

            // arrange - mbr partition export command
            var cancellationTokenSource = new CancellationTokenSource();
            var mbrPartExportCommand = new MbrPartExportCommand(new NullLogger<MbrPartExportCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), srcPath, partitionNumberExportFrom, destPath);

            // act - execute mbr partition export
            var result = await mbrPartExportCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - exported dest partition data is identical to src image data at partition offset
            var exportedPartitionData = await testCommandHelper.GetTestMedia(destPath).ReadData();
            Assert.Equal(srcPartitionData, exportedPartitionData);
        }

        [Fact]
        public async Task When_ExportingSrcImagePartitionToDestFileUsingBiosTypeName_Then_DestFileHasDataExported()
        {
            // arrange - path, size and test command helper
            var srcPath = $"mbr-disk.img";
            var destPath = "mbr-part2.img";
            var testCommandHelper = new TestCommandHelper();

            // arrange - sector size and mbr partition sectors and offsets
            const int sectorSize = 512;
            const int mbrPartition1StartSector = 63;
            const int mbrPartition1EndSector = 5000;
            const int mbrPartition2StartSector = 5001;
            const int mbrPartition2EndSector = 8000;
            var mbrPartition2Sectors = mbrPartition2EndSector - mbrPartition2StartSector + 1;
            var mbrPartition2StartOffset = mbrPartition2StartSector * sectorSize;
            const string partitionToExportFrom = "pistormrdb";

            // arrange - create source image data
            var srcImageSize = 10.MB();
            var srcImageDataBytes = TestDataHelper.CreateTestData(srcImageSize);

            // arrange - create source partition data
            var srcPartitionData = new byte[mbrPartition2Sectors * sectorSize];
            for (var i = 0; i < mbrPartition2Sectors; i++)
            {
                Array.Fill(srcPartitionData, (byte)((i + 1) % 256), i * sectorSize, sectorSize);
            }

            // arrange - create source and partition test medias
            testCommandHelper.AddTestMedia(srcPath, srcImageSize);
            await testCommandHelper.AddTestMedia(destPath);

            // arrange - write source image data
            using (var srcStream = testCommandHelper.GetTestMedia(srcPath).Stream)
            {
                await srcStream.WriteBytes(srcImageDataBytes);
                srcStream.Seek(mbrPartition2StartOffset, System.IO.SeekOrigin.Begin);
                await srcStream.WriteBytes(srcPartitionData);
            }

            // arrange - create source mbr disk and partitions
            await CreateMbrDisk(testCommandHelper, srcPath, srcImageSize);
            await AddMbrPartition(testCommandHelper, srcPath,
                mbrPartition1StartSector, mbrPartition1EndSector);
            await AddMbrPartition(testCommandHelper, srcPath,
                mbrPartition2StartSector, mbrPartition2EndSector, Constants.BiosPartitionTypes.PiStormRdb);

            // arrange - mbr partition export command
            var cancellationTokenSource = new CancellationTokenSource();
            var mbrPartExportCommand = new MbrPartExportCommand(new NullLogger<MbrPartExportCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), srcPath, partitionToExportFrom, destPath);

            // act - execute mbr partition export
            var result = await mbrPartExportCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - exported dest partition data is identical to src image data at partition offset
            var exportedPartitionData = await testCommandHelper.GetTestMedia(destPath).ReadData();
            Assert.Equal(srcPartitionData, exportedPartitionData);
        }
    }
}