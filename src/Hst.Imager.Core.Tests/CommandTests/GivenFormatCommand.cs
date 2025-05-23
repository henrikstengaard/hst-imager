﻿using System.Threading.Tasks;
using System;
using Xunit;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Threading;
using DiscUtils.Partitions;
using System.Linq;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Tests.CommandTests
{
    public class GivenFormatCommand : FsCommandTestBase
    {
        [Fact]
        public async Task When_FormattingDiskWithMbrFat32_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = FormatType.Mbr;
            var fileSystem = "fat32";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                testCommandHelper, new List<IPhysicalDrive>(), diskPath, partitionTable, fileSystem, 
                string.Empty, string.Empty, new Size(), new Size(), false);

            // act - execute format command
            var result = await formatCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - get disk info from media
            var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
            using var diskMedia = diskMediaResult.Value;
            var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

            // assert - disk info is not null and no mbr or rdb partition table exists
            Assert.NotNull(diskInfo);
            Assert.Null(diskInfo.GptPartitionTablePart);
            Assert.Null(diskInfo.RdbPartitionTablePart);

            // assert - mbr partition table contains a fat32 partition
            Assert.NotNull(diskInfo.MbrPartitionTablePart);
            Assert.Single(diskInfo.MbrPartitionTablePart.Parts,
                x => x.PartType == PartType.Partition);
            var partitionPart = diskInfo.MbrPartitionTablePart.Parts.FirstOrDefault(x => x.PartType == PartType.Partition);
            Assert.NotNull(partitionPart);
            Assert.Equal(BiosPartitionTypes.Fat32Lba.ToString(), partitionPart.BiosType);
            Assert.Equal("FAT32", partitionPart.FileSystem);
            Assert.True(partitionPart.PercentSize is >= 98 and <= 100);
        }

        [Fact]
        public async Task When_FormattingDiskWithMbrFat32AndSize50Percent_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = FormatType.Mbr;
            var fileSystem = "fat32";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize,
                create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(), 
                testCommandHelper, new List<IPhysicalDrive>(), diskPath, partitionTable, fileSystem, 
                string.Empty, string.Empty, new Size(50, Unit.Percent), new Size(), false);

            // act - execute format command
            var result = await formatCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - get disk info from media
            var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
            using var diskMedia = diskMediaResult.Value;
            var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

            // assert - disk info is not null and no mbr or rdb partition table exists
            Assert.NotNull(diskInfo);
            Assert.Null(diskInfo.GptPartitionTablePart);
            Assert.Null(diskInfo.RdbPartitionTablePart);

            // assert - mbr partition table contains a fat32 partition
            Assert.NotNull(diskInfo.MbrPartitionTablePart);
            Assert.Single(diskInfo.MbrPartitionTablePart.Parts,
                x => x.PartType == PartType.Partition);
            var partitionPart = diskInfo.MbrPartitionTablePart.Parts.FirstOrDefault(x => x.PartType == PartType.Partition);
            Assert.NotNull(partitionPart);
            Assert.Equal(BiosPartitionTypes.Fat32Lba.ToString(), partitionPart.BiosType);
            Assert.Equal("FAT32", partitionPart.FileSystem);
            Assert.True(partitionPart.PercentSize is >= 49 and <= 51);
        }

        [Fact]
        public async Task When_FormattingDiskWithMbrNtfs_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = FormatType.Mbr;
            var fileSystem = "ntfs";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                testCommandHelper, new List<IPhysicalDrive>(), diskPath, partitionTable, fileSystem, 
                string.Empty, string.Empty, new Size(), new Size(), false);

            // act - execute format command
            var result = await formatCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - get disk info from media
            var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
            using var diskMedia = diskMediaResult.Value;
            var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

            // assert - disk info is not null and no mbr or rdb partition table exists
            Assert.NotNull(diskInfo);
            Assert.Null(diskInfo.GptPartitionTablePart);
            Assert.Null(diskInfo.RdbPartitionTablePart);

            // assert - mbr partition table contains a ntfs partition
            Assert.NotNull(diskInfo.MbrPartitionTablePart);
            Assert.Single(diskInfo.MbrPartitionTablePart.Parts,
                x => x.PartType == PartType.Partition);
            var partitionPart = diskInfo.MbrPartitionTablePart.Parts.FirstOrDefault(x => x.PartType == PartType.Partition);
            Assert.NotNull(partitionPart);
            Assert.Equal(BiosPartitionTypes.Ntfs.ToString(), partitionPart.BiosType);
            Assert.Equal("NTFS", partitionPart.FileSystem);
            Assert.True(partitionPart.PercentSize is >= 98 and <= 100);
        }

        [Fact]
        public async Task When_FormattingDiskWithGptFat32_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = FormatType.Gpt;
            var fileSystem = "fat32";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                testCommandHelper,
                new List<IPhysicalDrive>(), diskPath, partitionTable, fileSystem, 
                string.Empty, string.Empty, new Size(), new Size(), false);

            // act - execute format command
            var result = await formatCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - get disk info from media
            var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
            using var diskMedia = diskMediaResult.Value;
            var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

            // assert - disk info is not null and no rdb partition table exists
            Assert.NotNull(diskInfo);
            Assert.Null(diskInfo.RdbPartitionTablePart);

            // assert - mbr partition table contains a gpt protective partition
            Assert.NotNull(diskInfo.MbrPartitionTablePart);
            Assert.Single(diskInfo.MbrPartitionTablePart.Parts,
                x => x.PartType == PartType.Partition);
            Assert.Single(diskInfo.MbrPartitionTablePart.Parts,
                x => x.PartType == PartType.Partition &&
                x.BiosType == BiosPartitionTypes.GptProtective.ToString());

            // assert - gpt partition table contains a ntfs partition
            Assert.NotNull(diskInfo.GptPartitionTablePart);
            Assert.Single(diskInfo.GptPartitionTablePart.Parts,
                x => x.PartType == PartType.Partition);
            var partitionPart = diskInfo.GptPartitionTablePart.Parts.FirstOrDefault(x => x.PartType == PartType.Partition);
            Assert.NotNull(partitionPart);
            Assert.Equal(GuidPartitionTypes.WindowsBasicData.ToString(), partitionPart.GuidType);
            Assert.Equal("FAT32", partitionPart.FileSystem);
            Assert.True(partitionPart.PercentSize is >= 98 and <= 100);
        }

        [Fact]
        public async Task When_FormattingDiskWithGptFat32AndSize50Percent_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = FormatType.Gpt;
            var fileSystem = "fat32";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                testCommandHelper, new List<IPhysicalDrive>(), diskPath, partitionTable, fileSystem, 
                string.Empty, string.Empty, new Size(50, Unit.Percent), new Size(), false);

            // act - execute format command
            var result = await formatCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - get disk info from media
            var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
            using var diskMedia = diskMediaResult.Value;
            var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

            // assert - disk info is not null and no rdb partition table exists
            Assert.NotNull(diskInfo);
            Assert.Null(diskInfo.RdbPartitionTablePart);

            // assert - mbr partition table contains a gpt protective partition
            Assert.NotNull(diskInfo.MbrPartitionTablePart);
            Assert.Single(diskInfo.MbrPartitionTablePart.Parts,
                x => x.PartType == PartType.Partition);
            Assert.Single(diskInfo.MbrPartitionTablePart.Parts,
                x => x.PartType == PartType.Partition &&
                x.BiosType == BiosPartitionTypes.GptProtective.ToString());

            // assert - gpt partition table contains a ntfs partition
            Assert.NotNull(diskInfo.GptPartitionTablePart);
            Assert.Single(diskInfo.GptPartitionTablePart.Parts,
                x => x.PartType == PartType.Partition);
            var partitionPart = diskInfo.GptPartitionTablePart.Parts.FirstOrDefault(x => x.PartType == PartType.Partition);
            Assert.NotNull(partitionPart);
            Assert.Equal(GuidPartitionTypes.WindowsBasicData.ToString(), partitionPart.GuidType);
            Assert.Equal("FAT32", partitionPart.FileSystem);
            Assert.True(partitionPart.PercentSize is >= 49 and <= 51);
        }

        [Fact]
        public async Task When_FormattingDiskWithGptNtfs_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = FormatType.Gpt;
            var fileSystem = "ntfs";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                testCommandHelper, new List<IPhysicalDrive>(), diskPath, partitionTable, fileSystem, 
                string.Empty, string.Empty, new Size(), new Size(), false);

            // act - execute format command
            var result = await formatCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // arrange - get disk info from media
            var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
            using var diskMedia = diskMediaResult.Value;
            var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

            // assert - disk info is not null and no rdb partition table exists
            Assert.NotNull(diskInfo);
            Assert.Null(diskInfo.RdbPartitionTablePart);

            // assert - mbr partition table contains a gpt protective partition
            Assert.NotNull(diskInfo.MbrPartitionTablePart);
            Assert.Single(diskInfo.MbrPartitionTablePart.Parts,
                x => x.PartType == PartType.Partition);
            Assert.Single(diskInfo.MbrPartitionTablePart.Parts,
                x => x.PartType == PartType.Partition &&
                x.BiosType == BiosPartitionTypes.GptProtective.ToString());

            // assert - gpt partition table contains a ntfs partition
            Assert.NotNull(diskInfo.GptPartitionTablePart);
            Assert.Single(diskInfo.GptPartitionTablePart.Parts,
                x => x.PartType == PartType.Partition);
            var partitionPart = diskInfo.GptPartitionTablePart.Parts.FirstOrDefault(x => x.PartType == PartType.Partition);
            Assert.NotNull(partitionPart);
            Assert.Equal(GuidPartitionTypes.WindowsBasicData.ToString(), partitionPart.GuidType);
            Assert.Equal("NTFS", partitionPart.FileSystem);
            Assert.True(partitionPart.PercentSize is >= 98 and <= 100);
        }

        [Fact]
        public async Task When_FormattingDiskWithRdbPfs3_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            const FormatType formatType = FormatType.Rdb;
            const string fileSystem = "pfs3";
            const string fileSystemPath = "pfs3aio";
            var outputDir = $"{Guid.NewGuid()}-dir";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add test pfs3aio file
            await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: TestHelper.Pfs3AioBytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            try
            {
                // arrange - create format command
                var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                    testCommandHelper, new List<IPhysicalDrive>(), diskPath, formatType, fileSystem, 
                    fileSystemPath, outputDir, new Size(), new Size(), false);

                // act - execute format command
                var formatResult = await formatCommand.Execute(CancellationToken.None);

                // assert - format is successful
                Assert.NotNull(formatResult);
                Assert.True(formatResult.IsSuccess);

                // arrange - get disk info from media
                var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
                using var diskMedia = diskMediaResult.Value;
                var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

                // assert - disk info is not null and no mbr partition table exists
                Assert.NotNull(diskInfo);
                Assert.Null(diskInfo.MbrPartitionTablePart);

                // assert - no gpt partition table exists
                Assert.Null(diskInfo.GptPartitionTablePart);

                // assert - rdb partition table exists and contains 1 partition
                Assert.NotNull(diskInfo.RdbPartitionTablePart);
                var partitionParts = diskInfo.RdbPartitionTablePart.Parts
                    .Where(x => x.PartType == PartType.Partition)
                    .ToList();
                Assert.Single(partitionParts);
                var partitionPart1 = partitionParts[0];
                Assert.Equal("PFS\\3", partitionPart1.FileSystem);
                Assert.True(partitionPart1.PercentSize is >= 98 and <= 100);
            }
            finally
            {
                TestHelper.DeletePaths(outputDir);
            }
        }

        [Fact]
        public async Task When_FormattingDiskWithRdbPfs3AndSize2Gb_Then_DiskIsPartitionedAndFormattedWith2Partitions()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 2.GB();
            const FormatType formatType = FormatType.Rdb;
            const string fileSystem = "pfs3";
            const string fileSystemPath = "pfs3aio";
            var outputDir = $"{Guid.NewGuid()}-dir";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add test pfs3aio file
            await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: TestHelper.Pfs3AioBytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            try
            {
                // arrange - create format command
                var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                    testCommandHelper, new List<IPhysicalDrive>(), diskPath, formatType, fileSystem, 
                    fileSystemPath, outputDir, new Size(), new Size(), false);

                // act - execute format command
                var formatResult = await formatCommand.Execute(CancellationToken.None);

                // assert - format is successful
                Assert.NotNull(formatResult);
                Assert.True(formatResult.IsSuccess);

                // arrange - get disk info from media
                var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
                using var diskMedia = diskMediaResult.Value;
                var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

                // assert - disk info is not null and no mbr partition table exists
                Assert.NotNull(diskInfo);
                Assert.Null(diskInfo.MbrPartitionTablePart);

                // assert - no gpt partition table exists
                Assert.Null(diskInfo.GptPartitionTablePart);

                // assert - rdb partition table exists and contains 2 partitions
                Assert.NotNull(diskInfo.RdbPartitionTablePart);
                var partitionParts = diskInfo.RdbPartitionTablePart.Parts
                    .Where(x => x.PartType == PartType.Partition)
                    .ToList();
                Assert.Equal(2, partitionParts.Count);
                var partitionPart1 = partitionParts[0];
                Assert.Equal("PFS\\3", partitionPart1.FileSystem);
                Assert.True(partitionPart1.PercentSize is >= 45 and <= 55);
                var partitionPart2 = partitionParts[1];
                Assert.Equal("PFS\\3", partitionPart2.FileSystem);
                Assert.True(partitionPart2.PercentSize is >= 45 and <= 55);
            }
            finally
            {
                TestHelper.DeletePaths(outputDir);
            }
        }

        [Fact]
        public async Task When_FormattingDiskWithRdbPfs3AndSize50Percent_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            const FormatType formatType = FormatType.Rdb;
            const string fileSystem = "pfs3";
            const string fileSystemPath = "pfs3aio";
            var outputDir = $"{Guid.NewGuid()}-dir";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add test pfs3aio file
            await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: TestHelper.Pfs3AioBytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize,
                create: true);

            try
            {
                // arrange - create format command
                var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                    testCommandHelper, new List<IPhysicalDrive>(), diskPath, formatType, fileSystem,
                    fileSystemPath, outputDir, new Size(50, Unit.Percent), new Size(), false);

                // act - execute format command
                var formatResult = await formatCommand.Execute(CancellationToken.None);

                // assert - format is successful
                Assert.NotNull(formatResult);
                Assert.True(formatResult.IsSuccess);

                // arrange - get disk info from media
                var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
                using var diskMedia = diskMediaResult.Value;
                var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

                // assert - disk info is not null and no mbr partition table exists
                Assert.NotNull(diskInfo);
                Assert.Null(diskInfo.MbrPartitionTablePart);

                // assert - no gpt partition table exists
                Assert.Null(diskInfo.GptPartitionTablePart);

                // assert - rdb partition table exists and contains 1 partition
                Assert.NotNull(diskInfo.RdbPartitionTablePart);
                var partitionParts = diskInfo.RdbPartitionTablePart.Parts
                    .Where(x => x.PartType == PartType.Partition)
                    .ToList();
                Assert.Single(partitionParts);
                var partitionPart = partitionParts[0];
                Assert.Equal("PFS\\3", partitionPart.FileSystem);
                Assert.True(partitionPart.PercentSize is >= 49 and <= 51);
            }
            finally
            {
                TestHelper.DeletePaths(outputDir);
            }
        }

        [Fact]
        public async Task When_FormattingDiskWithPiStormPfs3_Then_DiskIsPartitionedAndFormattedWith2Partitions()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 2.GB();
            const FormatType formatType = FormatType.PiStorm;
            const string fileSystem = "pfs3";
            const string fileSystemPath = "pfs3aio";
            var outputDir = $"{Guid.NewGuid()}-dir";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add test pfs3aio file
            await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: TestHelper.Pfs3AioBytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize,
                create: true);

            try
            {
                // arrange - create format command
                var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                    testCommandHelper, new List<IPhysicalDrive>(), diskPath, formatType, fileSystem,
                    fileSystemPath, outputDir, new Size(), new Size(), false);

                // act - execute format command
                var formatResult = await formatCommand.Execute(CancellationToken.None);

                // assert - format is successful
                Assert.NotNull(formatResult);
                Assert.True(formatResult.IsSuccess);

                // arrange - get disk info from media
                var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
                using var diskMedia = diskMediaResult.Value;
                var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

                // assert - disk info is not null
                Assert.NotNull(diskInfo);

                // assert - no gpt partition table exists
                Assert.Null(diskInfo.GptPartitionTablePart);

                // assert - no rdb partition table exists
                Assert.Null(diskInfo.RdbPartitionTablePart);

                // assert - disk info is not null and no mbr partition table exists
                Assert.NotNull(diskInfo.MbrPartitionTablePart);

                // assert - mbr partition has 2 partitions
                var partitionParts = diskInfo.MbrPartitionTablePart.Parts
                    .Where(x => x.PartType == PartType.Partition)
                    .ToList();
                Assert.Equal(2, partitionParts.Count);

                // assert - 1st boot partition is FAT32 formatted and has a size of 200mb
                var bootPartitionPart = partitionParts[0];
                Assert.Equal("FAT32", bootPartitionPart.FileSystem);
                Assert.True(bootPartitionPart.Size > 920.MB() && bootPartitionPart.Size < 1080.MB());

                // assert - 2nd PiStorm partition has PiStormRdb and a size of 800mb
                var piStormRdbPartitionPart = partitionParts[1];
                Assert.Equal(Constants.BiosPartitionTypes.PiStormRdb.ToString(), piStormRdbPartitionPart.BiosType);
                Assert.True(piStormRdbPartitionPart.Size > 920.MB() && piStormRdbPartitionPart.Size < 1080.MB());
            }
            finally
            {
                TestHelper.DeletePaths(outputDir);
            }
        }

        [Fact]
        public async Task When_FormattingDiskWithPiStormPfs3AndSize50Percent_Then_DiskIsPartitionedAndFormattedWith2Partitions()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 4.GB();
            const FormatType formatType = FormatType.PiStorm;
            const string fileSystem = "pfs3";
            const string fileSystemPath = "pfs3aio";
            var outputDir = $"{Guid.NewGuid()}-dir";
            var size = new Size(50, Unit.Percent);

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add test pfs3aio file
            await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: TestHelper.Pfs3AioBytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            try
            {
                // arrange - create format command
                var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                    testCommandHelper, new List<IPhysicalDrive>(), diskPath, formatType, fileSystem, 
                    fileSystemPath, outputDir, size, new Size(), false);

                // act - execute format command
                var formatResult = await formatCommand.Execute(CancellationToken.None);

                // assert - format is successful
                Assert.NotNull(formatResult);
                Assert.True(formatResult.IsSuccess);

                // arrange - get disk info from media
                var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
                using var diskMedia = diskMediaResult.Value;
                var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

                // assert - disk info is not null
                Assert.NotNull(diskInfo);

                // assert - no gpt partition table exists
                Assert.Null(diskInfo.GptPartitionTablePart);

                // assert - no rdb partition table exists
                Assert.Null(diskInfo.RdbPartitionTablePart);

                // assert - disk info is not null and no mbr partition table exists
                Assert.NotNull(diskInfo.MbrPartitionTablePart);

                // assert - mbr partition has 2 partitions
                var partitionParts = diskInfo.MbrPartitionTablePart.Parts
                    .Where(x => x.PartType == PartType.Partition)
                    .ToList();
                Assert.Equal(2, partitionParts.Count);

                // assert - 1st boot partition is FAT32 formatted and has a size of 200mb
                var bootPartitionPart = partitionParts[0];
                Assert.Equal("FAT32", bootPartitionPart.FileSystem);
                Assert.True(bootPartitionPart.PercentSize is >= 23 and <= 27);

                // assert - 2nd PiStorm partition has PiStormRdb type and a size of 300mb
                var piStormRdbPartitionPart = partitionParts[1];
                Assert.Equal(Constants.BiosPartitionTypes.PiStormRdb.ToString(), piStormRdbPartitionPart.BiosType);
                Assert.True(piStormRdbPartitionPart.PercentSize is >= 23 and <= 27);
            }
            finally
            {
                TestHelper.DeletePaths(outputDir);
            }
        }

        [Fact]
        public async Task When_FormattingDiskWithPiStormPfs3LessThan2Gb_Then_FormatReturnsError()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 1.GB();
            const FormatType formatType = FormatType.PiStorm;
            const string fileSystem = "pfs3";
            const string fileSystemPath = "pfs3aio";
            var outputDir = $"{Guid.NewGuid()}-dir";
            var size = new Size(0, Unit.Percent);

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add test pfs3aio file
            await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: TestHelper.Pfs3AioBytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            try
            {
                // arrange - create format command
                var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                    testCommandHelper, new List<IPhysicalDrive>(), diskPath, formatType, fileSystem,
                    fileSystemPath, outputDir, size, new Size(), false);

                // act - execute format command
                var formatResult = await formatCommand.Execute(CancellationToken.None);

                // assert - format failed
                Assert.NotNull(formatResult);
                Assert.True(formatResult.IsFaulted);
                Assert.False(formatResult.IsSuccess);
            }
            finally
            {
                TestHelper.DeletePaths(outputDir);
            }
        }

                [Fact]
        public async Task When_FormattingDiskWithRdbDos7WithDos7Support_Then_DiskIsPartitionedAndFormattedWithDos7()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            const FormatType formatType = FormatType.Rdb;
            const string fileSystem = "dos7";
            const string fileSystemPath = "FastFileSystem";
            var outputDir = $"{Guid.NewGuid()}-dir";
            var size = new Size(0, Unit.Bytes);
            
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add fast file system file
            await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: TestHelper.FastFileSystemDos7Bytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            try
            {
                // arrange - create format command
                var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                    testCommandHelper, new List<IPhysicalDrive>(), diskPath, formatType, fileSystem,
                    fileSystemPath, outputDir, size, new Size(), false);

                // act - execute format command
                var formatResult = await formatCommand.Execute(CancellationToken.None);
                
                // assert - format is successful
                Assert.NotNull(formatResult);
                Assert.True(formatResult.IsSuccess);

                // arrange - get disk info from media
                var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
                using var diskMedia = diskMediaResult.Value;
                var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

                // assert - disk info is not null and no mbr partition table exists
                Assert.NotNull(diskInfo);
                Assert.Null(diskInfo.MbrPartitionTablePart);

                // assert - no gpt partition table exists
                Assert.Null(diskInfo.GptPartitionTablePart);

                // assert - rdb partition table exists and contains 1 partition
                Assert.NotNull(diskInfo.RdbPartitionTablePart);
                var partitionParts = diskInfo.RdbPartitionTablePart.Parts
                    .Where(x => x.PartType == PartType.Partition)
                    .ToList();
                Assert.Single(partitionParts);
                var partitionPart = partitionParts[0];
                Assert.Equal("DOS\\7", partitionPart.FileSystem);
            }
            finally
            {
                TestHelper.DeletePaths(outputDir);
            }
        }

        [Fact]
        public async Task When_FormattingDiskWithRdbDos7WithoutDos7Support_Then_DiskIsPartitionedAndFormattedWithDos3()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            const FormatType formatType = FormatType.Rdb;
            const string fileSystem = "dos7";
            const string fileSystemPath = "FastFileSystem";
            var outputDir = $"{Guid.NewGuid()}-dir";
            var size = new Size(0, Unit.Bytes);
            
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add fast file system file
            await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: TestHelper.FastFileSystemDos3Bytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            try
            {
                // arrange - create format command
                var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                    testCommandHelper, new List<IPhysicalDrive>(), diskPath, formatType, fileSystem,
                    fileSystemPath, outputDir, size, new Size(), false);

                // act - execute format command
                var formatResult = await formatCommand.Execute(CancellationToken.None);
                
                // assert - format is successful
                Assert.NotNull(formatResult);
                Assert.True(formatResult.IsSuccess);

                // arrange - get disk info from media
                var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
                using var diskMedia = diskMediaResult.Value;
                var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

                // assert - disk info is not null and no mbr partition table exists
                Assert.NotNull(diskInfo);
                Assert.Null(diskInfo.MbrPartitionTablePart);

                // assert - no gpt partition table exists
                Assert.Null(diskInfo.GptPartitionTablePart);

                // assert - rdb partition table exists and contains 1 partition
                Assert.NotNull(diskInfo.RdbPartitionTablePart);
                var partitionParts = diskInfo.RdbPartitionTablePart.Parts
                    .Where(x => x.PartType == PartType.Partition)
                    .ToList();
                Assert.Single(partitionParts);
                var partitionPart = partitionParts[0];
                Assert.Equal("DOS\\3", partitionPart.FileSystem);
            }
            finally
            {
                TestHelper.DeletePaths(outputDir);
            }
        }
        
        [Fact]
        public async Task When_FormattingDiskWith4GbRdbDos7WithoutDos7Support_Then_DiskIsPartitionedAndFormattedWithDos3()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 4.GB();
            const FormatType formatType = FormatType.Rdb;
            const string fileSystem = "dos7";
            const string fileSystemPath = "FastFileSystem";
            var outputDir = $"{Guid.NewGuid()}-dir";
            var size = new Size(0, Unit.Bytes);
            
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add fast file system file
            await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: TestHelper.FastFileSystemDos3Bytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            try
            {
                // arrange - create format command
                var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                    testCommandHelper, new List<IPhysicalDrive>(), diskPath, formatType, fileSystem,
                    fileSystemPath, outputDir, size, new Size(), false);

                // act - execute format command
                var formatResult = await formatCommand.Execute(CancellationToken.None);
                
                // assert - format is successful
                Assert.NotNull(formatResult);
                Assert.True(formatResult.IsSuccess);

                // arrange - get disk info from media
                var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
                using var diskMedia = diskMediaResult.Value;
                var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

                // assert - disk info is not null and no mbr partition table exists
                Assert.NotNull(diskInfo);
                Assert.Null(diskInfo.MbrPartitionTablePart);

                // assert - no gpt partition table exists
                Assert.Null(diskInfo.GptPartitionTablePart);

                // assert - rdb partition table exists and contains 1 partition
                Assert.NotNull(diskInfo.RdbPartitionTablePart);
                var partitionParts = diskInfo.RdbPartitionTablePart.Parts
                    .Where(x => x.PartType == PartType.Partition)
                    .ToList();
                Assert.Equal(3, partitionParts.Count);
                Assert.True(partitionParts.All(x => x.FileSystem == "DOS\\3" && x.PercentSize < 2.GB()));
            }
            finally
            {
                TestHelper.DeletePaths(outputDir);
            }
        }
    }
}