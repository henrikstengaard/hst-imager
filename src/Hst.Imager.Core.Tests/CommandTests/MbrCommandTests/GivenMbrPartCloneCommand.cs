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
    public class GivenMbrPartCloneCommand : FsCommandTestBase
    {
        [Fact]
        public async Task When_CloningPartFromSrcImgToDestImg_Then_PartitionIsCloned()
        {
            // arrange - path, size and test command helper
            var srcPath = $"src-{Guid.NewGuid()}.img";
            var destPath = $"dest-{Guid.NewGuid()}.img";
            var diskSize = 10.MB();
            const int srcPartNumberCloneFrom = 1;
            const int destPartNumberCloneTo = 2;
            var testCommandHelper = new TestCommandHelper();

            // arrange - sector size and partition sectors
            const int sectorSize = 512;
            var part1StartSector = 63;
            var part1EndSector = diskSize / (sectorSize * 2);
            var part2StartSector = part1EndSector + 1;
            var part2EndSector = (diskSize / sectorSize) - 10;

            // arrange - create src test media
            testCommandHelper.AddTestMedia(srcPath, diskSize);
            await CreateMbrDisk(testCommandHelper, srcPath, diskSize);
            await AddMbrPartition(testCommandHelper, srcPath, part1StartSector, part1EndSector);
            await AddMbrPartition(testCommandHelper, srcPath, part2StartSector, part2EndSector);

            // arrange - create dest test media
            testCommandHelper.AddTestMedia(destPath, diskSize);
            await CreateMbrDisk(testCommandHelper, destPath, diskSize);
            await AddMbrPartition(testCommandHelper, destPath, part1StartSector, part1EndSector);
            await AddMbrPartition(testCommandHelper, destPath, part2StartSector, part2EndSector);

            // arrange - create source part 1 data
            var srcPart1Size = (part1EndSector - part1StartSector + 1) * sectorSize;
            var srcPart1Bytes = TestDataHelper.CreateTestData(srcPart1Size);

            // arrange - write source part 1 data
            var mbrPartition1StartOffset = part1StartSector * sectorSize;
            using (var srcStream = testCommandHelper.GetTestMedia(srcPath).Stream)
            {
                srcStream.Seek(mbrPartition1StartOffset, System.IO.SeekOrigin.Begin);
                await srcStream.WriteBytes(srcPart1Bytes);
            }

            // arrange - get destination data
            var destData = await testCommandHelper.GetTestMedia(destPath).ReadData();

            // arrange - mbr partition clone command
            var mbrPartCloneCommand = new MbrPartCloneCommand(new NullLogger<MbrPartCloneCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), srcPath, srcPartNumberCloneFrom, destPath, destPartNumberCloneTo);

            // act - execute mbr partition clone
            var result = await mbrPartCloneCommand.Execute(CancellationToken.None);

            // assert - execute mbr partition clone returned success
            Assert.True(result.IsSuccess);

            // arrange - create expected dest data with a copy of dest data and
            // src part 1 data written to part 2 offset
            var expectedDestData = new byte[diskSize];
            Array.Copy(destData, 0, expectedDestData, 0, diskSize); 
            var part2StartOffset = part2StartSector * sectorSize;
            Array.Copy(srcPart1Bytes, 0, expectedDestData, part2StartOffset, srcPart1Size);

            // assert - dest data is equal to expected dest data
            destData = await testCommandHelper.GetTestMedia(destPath).ReadData();
            Assert.Equal(expectedDestData, destData);
        }

        [Fact]
        public async Task When_CloningPartFromAndToSameImg_Then_PartitionIsCloned()
        {
            // arrange - path, size and test command helper
            var imgPath = $"clone-{Guid.NewGuid()}.img";
            var diskSize = 10.MB();
            const int srcPartNumberCloneFrom = 1;
            const int destPartNumberCloneTo = 2;
            var testCommandHelper = new TestCommandHelper();

            // arrange - sector size and partition sectors
            const int sectorSize = 512;
            var mbrPartition1StartSector = 63;
            var mbrPartition1EndSector = diskSize / (sectorSize * 2);
            var mbrPartition2StartSector = mbrPartition1EndSector + 1;
            var mbrPartition2EndSector = (diskSize / sectorSize) - 10;

            // arrange - create test media
            testCommandHelper.AddTestMedia(imgPath, diskSize);
            await CreateMbrDisk(testCommandHelper, imgPath, diskSize);
            await AddMbrPartition(testCommandHelper, imgPath, mbrPartition1StartSector, mbrPartition1EndSector);
            await AddMbrPartition(testCommandHelper, imgPath, mbrPartition2StartSector, mbrPartition2EndSector);

            // arrange - create part 1 data
            var part1Size = (mbrPartition1EndSector - mbrPartition1StartSector + 1) * sectorSize;
            var part1Bytes = TestDataHelper.CreateTestData(part1Size);

            // arrange - write part 1 data
            var mbrPartition1StartOffset = mbrPartition1StartSector * sectorSize;
            using (var imgStream = testCommandHelper.GetTestMedia(imgPath).Stream)
            {
                imgStream.Seek(mbrPartition1StartOffset, System.IO.SeekOrigin.Begin);
                await imgStream.WriteBytes(part1Bytes);
            }

            // arrange - get img data
            var imgData = await testCommandHelper.GetTestMedia(imgPath).ReadData();

            // arrange - mbr partition clone command
            var mbrPartCloneCommand = new MbrPartCloneCommand(new NullLogger<MbrPartCloneCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), imgPath, srcPartNumberCloneFrom, imgPath, destPartNumberCloneTo);

            // act - execute mbr partition clone
            var result = await mbrPartCloneCommand.Execute(CancellationToken.None);

            // assert - execute mbr partition clone returned success
            Assert.True(result.IsSuccess);

            // arrange - create expected img data with a copy of img data and
            // part 1 data written to part 2 offset
            var expectedImgData = new byte[diskSize];
            Array.Copy(imgData, 0, expectedImgData, 0, diskSize);
            var part2StartOffset = mbrPartition2StartSector * sectorSize;
            Array.Copy(part1Bytes, 0, expectedImgData, part2StartOffset, part1Size);

            // assert - img data is equal to expected img data
            imgData = await testCommandHelper.GetTestMedia(imgPath).ReadData();
            Assert.Equal(expectedImgData, imgData);
        }

        [Fact]
        public async Task When_CloningLargerPartFromSrcImgToDestImg_Then_ErrorIsReturned()
        {
            // arrange - path, size and test command helper
            var srcPath = $"src-{Guid.NewGuid()}.img";
            var destPath = $"dest-{Guid.NewGuid()}.img";
            const int srcPartNumberCloneFrom = 2;
            const int destPartNumberCloneTo = 1;
            var testCommandHelper = new TestCommandHelper();

            // arrange - sector size and mbr partition sectors and offsets
            var diskSize = 10.MB();
            const int sectorSize = 512;
            var mbrPartition1StartSector = 63;
            var mbrPartition1EndSector = diskSize / (sectorSize * 2);
            var mbrPartition2StartSector = mbrPartition1EndSector + 1;
            var mbrPartition2EndSector = (diskSize / sectorSize) - 10;

            // arrange - create src test media
            testCommandHelper.AddTestMedia(srcPath, diskSize);
            await CreateMbrDisk(testCommandHelper, srcPath, diskSize);
            await AddMbrPartition(testCommandHelper, srcPath, mbrPartition1StartSector, mbrPartition1EndSector);
            await AddMbrPartition(testCommandHelper, srcPath, mbrPartition2StartSector, mbrPartition2EndSector);

            // arrange - create dest test media
            testCommandHelper.AddTestMedia(destPath, diskSize);
            await CreateMbrDisk(testCommandHelper, destPath, diskSize);
            await AddMbrPartition(testCommandHelper, destPath, mbrPartition1StartSector, mbrPartition1EndSector);
            await AddMbrPartition(testCommandHelper, destPath, mbrPartition2StartSector, mbrPartition2EndSector);

            // arrange - mbr partition clone command
            var mbrPartCloneCommand = new MbrPartCloneCommand(new NullLogger<MbrPartCloneCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), srcPath, srcPartNumberCloneFrom, destPath, destPartNumberCloneTo);

            // act - execute mbr partition clone
            var result = await mbrPartCloneCommand.Execute(CancellationToken.None);

            // assert - execute mbr partition clone returned error
            Assert.True(result.IsFaulted);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task When_CloningPartFromOneVhdToAnother_Then_PartitionIsCloned()
        {
            // arrange - path, size and test command helper
            var srcPath = $"src-{Guid.NewGuid()}.vhd";
            var destPath = $"dest-{Guid.NewGuid()}.vhd";
            const int srcPartNumberCloneFrom = 1;
            const int destPartNumberCloneTo = 1;

            // arrange - sector size and mbr partition sectors and offsets
            var diskSize = 10.MB();
            const int sectorSize = 512;
            var mbrPartition1StartSector = 63;
            var mbrPartition1EndSector = (diskSize / sectorSize) - 10;

            try
            {
                using var testCommandHelper = new TestCommandHelper();

                // arrange - create src test media
                await CreateMbrDisk(testCommandHelper, srcPath, diskSize);
                await AddMbrPartition(testCommandHelper, srcPath, mbrPartition1StartSector, mbrPartition1EndSector);

                // arrange - create part 1 data
                var part1Size = (mbrPartition1EndSector - mbrPartition1StartSector + 1) * sectorSize;
                var part1Bytes = TestDataHelper.CreateTestData(part1Size);

                // arrange - write part 1 data
                var mbrPartition1StartOffset = mbrPartition1StartSector * sectorSize;
                await TestHelper.WriteData(testCommandHelper, srcPath, mbrPartition1StartOffset, part1Bytes);

                // arrange - create dest test media
                await CreateMbrDisk(testCommandHelper, destPath, diskSize);
                await AddMbrPartition(testCommandHelper, destPath, mbrPartition1StartSector, mbrPartition1EndSector);

                // arrange - get dest data
                var destData = await TestHelper.ReadData(testCommandHelper, destPath, 0, (int)diskSize);

                // arrange - clear src and dest medias used for creating mbr disks
                testCommandHelper.ClearActiveMedias();
                
                // arrange - mbr partition clone command
                var mbrPartCloneCommand = new MbrPartCloneCommand(new NullLogger<MbrPartCloneCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(), srcPath, srcPartNumberCloneFrom, destPath, destPartNumberCloneTo);

                // act - execute mbr partition clone
                var result = await mbrPartCloneCommand.Execute(CancellationToken.None);

                // assert - execute mbr partition clone returned success
                Assert.True(result.IsSuccess);

                // arrange - create expected dest data with part 1 data written to dest part 1 offset
                var expectedDestData = new byte[diskSize];
                Array.Copy(destData, 0, expectedDestData, 0, diskSize);
                Array.Copy(part1Bytes, 0, expectedDestData, mbrPartition1StartOffset, part1Size);

                // assert - actual dest data is equal to expected dest data
                var actualDestData = await TestHelper.ReadData(testCommandHelper, destPath, 0, (int)diskSize);
                Assert.Equal(expectedDestData, actualDestData);
            }
            finally
            {
                TestHelper.DeletePaths(srcPath, destPath);
            }
        }
    }
}