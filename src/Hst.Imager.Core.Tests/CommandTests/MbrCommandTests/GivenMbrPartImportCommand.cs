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
    public class GivenMbrPartImportCommand : FsCommandTestBase
    {
        [Fact]
        public async Task When_ImportingSrcFileToDestImage_Then_DestImageHasDataImported()
        {
            // arrange - path, size and test command helper
            var srcPath = "mbr-part2.img";
            var destPath = $"mbr-disk.img";
            var testCommandHelper = new TestCommandHelper();

            // arrange - sector size and mbr partition sectors and offsets
            const int sectorSize = 512;
            const int mbrPartition1StartSector = 63;
            const int mbrPartition1EndSector = 5000;
            const int mbrPartition2StartSector = 5001;
            const int mbrPartition2EndSector = 8000;
            var mbrPartition2StartOffset = mbrPartition2StartSector * sectorSize;
            const int partitionNumberImportingTo = 2;

            // arrange - create source partition data
            var sectors = mbrPartition2EndSector - mbrPartition2StartSector + 1;
            var srcPartitionDataBytes = new byte[sectors * 512];
            for (var i = 0; i < sectors; i++)
            {
                Array.Fill(srcPartitionDataBytes, (byte)((i + 1) % 256), i * sectorSize, sectorSize);
            }

            // arrange - create destination image data
            var destImageSize = 10.MB();
            var destImageDataBytes = TestDataHelper.CreateTestData(destImageSize);

            // arrange - create src and dest test medias
            testCommandHelper.AddTestMedia(srcPath, srcPartitionDataBytes.Length);
            testCommandHelper.AddTestMedia(destPath, destImageSize);

            // arrange - write source partition data
            await testCommandHelper.GetTestMedia(srcPath).Stream
                .WriteBytes(srcPartitionDataBytes);

            // arrange - write destination image data
            await testCommandHelper.GetTestMedia(destPath).Stream
                .WriteBytes(destImageDataBytes);

            // arrange - create dest mbr disk and partitions
            await CreateMbrDisk(testCommandHelper, destPath, destImageSize);
            await AddMbrPartition(testCommandHelper, destPath,
                mbrPartition1StartSector, mbrPartition1EndSector);
            await AddMbrPartition(testCommandHelper, destPath,
                mbrPartition2StartSector, mbrPartition2EndSector, Constants.BiosPartitionTypes.PiStormRdb);

            // arrange - mbr partition import command
            var cancellationTokenSource = new CancellationTokenSource();
            var mbrPartImportCommand = new MbrPartImportCommand(new NullLogger<MbrPartImportCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, partitionNumberImportingTo);

            // act - execute mbr partition import
            var result = await mbrPartImportCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - dest image at partition start offset has data identical to imported src partition data
            var actualImageDataBytes = await testCommandHelper.GetTestMedia(destPath).ReadData();
            var actualPartitionData = new byte[srcPartitionDataBytes.Length];
            Array.Copy(actualImageDataBytes, mbrPartition2StartOffset,
                actualPartitionData, 0,
                srcPartitionDataBytes.Length);
            Assert.Equal(srcPartitionDataBytes, actualPartitionData);
        }

        [Fact]
        public async Task When_ImportingSrcFileLargerThanDestImagePartition_Then_ErrorIsReturned()
        {
            // arrange - path, size and test command helper
            var srcPath = "mbr-part2.img";
            var destPath = $"mbr-disk.img";
            var testCommandHelper = new TestCommandHelper();

            // arrange - sector size and mbr partition sectors and offsets
            const int mbrPartition1StartSector = 63;
            const int mbrPartition1EndSector = 5000;
            const int mbrPartition2StartSector = 5001;
            const int mbrPartition2EndSector = 8000;
            const int partitionNumberImportTo = 2;

            // arrange - create source partition data
            var srcPartitionDataBytes = new byte[10.MB()];

            // arrange - create destination image data
            var destImageSize = 10.MB();
            var destImageDataBytes = TestDataHelper.CreateTestData(destImageSize);

            // arrange - create src and dest test medias
            testCommandHelper.AddTestMedia(srcPath, srcPartitionDataBytes.Length);
            testCommandHelper.AddTestMedia(destPath, destImageSize);

            // arrange - write source partition data
            await testCommandHelper.GetTestMedia(srcPath).Stream
                .WriteBytes(srcPartitionDataBytes);

            // arrange - write destination image data
            await testCommandHelper.GetTestMedia(destPath).Stream
                .WriteBytes(destImageDataBytes);

            // arrange - create dest mbr disk and partitions
            await CreateMbrDisk(testCommandHelper, destPath, destImageSize);
            await AddMbrPartition(testCommandHelper, destPath,
                mbrPartition1StartSector, mbrPartition1EndSector);
            await AddMbrPartition(testCommandHelper, destPath,
                mbrPartition2StartSector, mbrPartition2EndSector, Constants.BiosPartitionTypes.PiStormRdb);

            // arrange - mbr partition import command
            var cancellationTokenSource = new CancellationTokenSource();
            var mbrPartImportCommand = new MbrPartImportCommand(new NullLogger<MbrPartImportCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, partitionNumberImportTo);

            // act - execute mbr partition import
            var result = await mbrPartImportCommand.Execute(cancellationTokenSource.Token);

            // assert - mbr partition import returned error
            Assert.False(result.IsSuccess);
            Assert.True(result.IsFaulted);
            Assert.NotNull(result.Error);
            Assert.Contains("larger than", result.Error.Message);
        }
    }
}