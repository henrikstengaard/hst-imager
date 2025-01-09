using System.Threading.Tasks;
using System;
using Xunit;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Threading;
using DiscUtils.Partitions;
using System.Linq;
using System.Text;

namespace Hst.Imager.Core.Tests.CommandTests
{
    public class GivenFormatCommand : FsCommandTestBase
    {
        protected static readonly byte[] TestPfs3AioBytes = Encoding.ASCII.GetBytes(
            "$VER: pfs3aio 0.1 (01/01/22)");

        [Fact]
        public async Task When_FormattingDiskWithMbrFat32_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = Models.FormatType.Mbr;
            var fileSystem = "fat32";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(), testCommandHelper,
                new List<IPhysicalDrive>(), diskPath, partitionTable, fileSystem, new Models.Size());

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
            Assert.Equal(BiosPartitionTypes.Fat32.ToString(), partitionPart.BiosType);
            Assert.Equal("FAT32", partitionPart.FileSystem);
            Assert.True(partitionPart.PercentSize >= 98 && partitionPart.PercentSize <= 100);
        }

        [Fact]
        public async Task When_FormattingDiskWithMbrFat32AndSize50Percent_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = Models.FormatType.Mbr;
            var fileSystem = "fat32";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(), testCommandHelper,
                new List<IPhysicalDrive>(), diskPath, partitionTable, fileSystem, new Models.Size(50, Models.Unit.Percent));

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
            Assert.Equal(BiosPartitionTypes.Fat32.ToString(), partitionPart.BiosType);
            Assert.Equal("FAT32", partitionPart.FileSystem);
            Assert.True(partitionPart.PercentSize >= 49 && partitionPart.PercentSize <= 51);
        }

        [Fact]
        public async Task When_FormattingDiskWithMbrNtfs_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = Models.FormatType.Mbr;
            var fileSystem = "ntfs";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(), testCommandHelper,
                new List<IPhysicalDrive>(), diskPath, partitionTable, fileSystem, new Models.Size());

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
            Assert.Equal(BiosPartitionTypes.Ntfs.ToString(), partitionPart.BiosType);
            Assert.Equal("NTFS", partitionPart.FileSystem);
            Assert.True(partitionPart.PercentSize >= 98 && partitionPart.PercentSize <= 100);
        }

        [Fact]
        public async Task When_FormattingDiskWithGptFat32_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = Models.FormatType.Gpt;
            var fileSystem = "fat32";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(), testCommandHelper,
                new List<IPhysicalDrive>(), diskPath, partitionTable, fileSystem, new Models.Size());

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
            Assert.Equal(GuidPartitionTypes.WindowsBasicData.ToString(), partitionPart.GuidType);
            Assert.Equal("FAT32", partitionPart.FileSystem);
            Assert.True(partitionPart.PercentSize >= 98 && partitionPart.PercentSize <= 100);
        }

        [Fact]
        public async Task When_FormattingDiskWithGptFat32AndSize50Percent_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = Models.FormatType.Gpt;
            var fileSystem = "fat32";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(), testCommandHelper,
                new List<IPhysicalDrive>(), diskPath, partitionTable, fileSystem, new Models.Size(50, Models.Unit.Percent));

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
            Assert.Equal(GuidPartitionTypes.WindowsBasicData.ToString(), partitionPart.GuidType);
            Assert.Equal("FAT32", partitionPart.FileSystem);
            Assert.True(partitionPart.PercentSize >= 49 && partitionPart.PercentSize <= 51);
        }

        [Fact]
        public async Task When_FormattingDiskWithGptNtfs_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = Models.FormatType.Gpt;
            var fileSystem = "ntfs";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(), testCommandHelper,
                new List<IPhysicalDrive>(), diskPath, partitionTable, fileSystem, new Models.Size());

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
            Assert.Equal(GuidPartitionTypes.WindowsBasicData.ToString(), partitionPart.GuidType);
            Assert.Equal("NTFS", partitionPart.FileSystem);
            Assert.True(partitionPart.PercentSize >= 98 && partitionPart.PercentSize <= 100);
        }

        [Fact]
        public async Task When_FormattingDiskWithRdbPfs3_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var formatType = Models.FormatType.Rdb;
            var fileSystem = "pfs3";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add test pfs3aio file
            await testCommandHelper.AddTestMedia("pfs3aio", "pfs3aio", data: TestPfs3AioBytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(), testCommandHelper,
                new List<IPhysicalDrive>(), diskPath, formatType, fileSystem, new Models.Size());

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
            Assert.Equal("PDS\\3", partitionPart1.FileSystem);
            Assert.True(partitionPart1.PercentSize >= 98 && partitionPart1.PercentSize <= 100);
        }

        [Fact]
        public async Task When_FormattingDiskWithRdbPfs3AndSize2Gb_Then_DiskIsPartitionedAndFormattedWith2Partitions()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 2.GB();
            var formatType = Models.FormatType.Rdb;
            var fileSystem = "pfs3";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add test pfs3aio file
            await testCommandHelper.AddTestMedia("pfs3aio", "pfs3aio", data: TestPfs3AioBytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(), testCommandHelper,
                new List<IPhysicalDrive>(), diskPath, formatType, fileSystem, new Models.Size());

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
            Assert.Equal("PDS\\3", partitionPart1.FileSystem);
            Assert.True(partitionPart1.PercentSize >= 49 && partitionPart1.PercentSize <= 51);
            var partitionPart2 = partitionParts[1];
            Assert.Equal("PDS\\3", partitionPart2.FileSystem);
            Assert.True(partitionPart2.PercentSize >= 49 && partitionPart2.PercentSize <= 51);
        }

        [Fact]
        public async Task When_FormattingDiskWithRdbPfs3AndSize50Percent_Then_DiskIsPartitionedAndFormattedWith1Partition()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var formatType = Models.FormatType.Rdb;
            var fileSystem = "pfs3";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add test pfs3aio file
            await testCommandHelper.AddTestMedia("pfs3aio", "pfs3aio", data: TestPfs3AioBytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(), testCommandHelper,
                new List<IPhysicalDrive>(), diskPath, formatType, fileSystem, new Models.Size(50, Models.Unit.Percent));

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
            Assert.Equal("PDS\\3", partitionPart.FileSystem);
            Assert.True(partitionPart.PercentSize >= 49 && partitionPart.PercentSize <= 51);
        }

        [Fact]
        public async Task When_FormattingDiskWithPiStormPfs3_Then_DiskIsPartitionedAndFormattedWith2Partitions()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 2.GB();
            var formatType = Models.FormatType.PiStorm;
            var fileSystem = "pfs3";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add test pfs3aio file
            await testCommandHelper.AddTestMedia("pfs3aio", "pfs3aio", data: TestPfs3AioBytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(), testCommandHelper,
                new List<IPhysicalDrive>(), diskPath, formatType, fileSystem, new Models.Size());

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
            Assert.True(bootPartitionPart.Size > 980.MB() && bootPartitionPart.Size < 1020.MB());

            // assert - 2nd PiStorm partition has PiStormRdb and a size of 800mb
            var piStormRdbPartitionPart = partitionParts[1];
            Assert.Equal(Constants.BiosPartitionTypes.PiStormRdb.ToString(), piStormRdbPartitionPart.BiosType);
            Assert.True(piStormRdbPartitionPart.Size > 980.MB() && piStormRdbPartitionPart.Size < 1020.MB());
        }

        [Fact]
        public async Task When_FormattingDiskWithPiStormPfs3AndSize50Percent_Then_DiskIsPartitionedAndFormattedWith2Partitions()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 4.GB();
            var formatType = Models.FormatType.PiStorm;
            var fileSystem = "pfs3";
            var size = new Models.Size(50, Models.Unit.Percent);

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add test pfs3aio file
            await testCommandHelper.AddTestMedia("pfs3aio", "pfs3aio", data: TestPfs3AioBytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(), testCommandHelper,
                new List<IPhysicalDrive>(), diskPath, formatType, fileSystem, size);

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
            Assert.True(bootPartitionPart.PercentSize >= 23 && bootPartitionPart.PercentSize <= 27);

            // assert - 2nd PiStorm partition has PiStormRdb type and a size of 300mb
            var piStormRdbPartitionPart = partitionParts[1];
            Assert.Equal(Constants.BiosPartitionTypes.PiStormRdb.ToString(), piStormRdbPartitionPart.BiosType);
            Assert.True(piStormRdbPartitionPart.PercentSize >= 23 && piStormRdbPartitionPart.PercentSize <= 27);
        }

        [Fact]
        public async Task When_FormattingDiskWithPiStormPfs3LessThan2Gb_Then_FormatReturnsError()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 1.GB();
            var formatType = Models.FormatType.PiStorm;
            var fileSystem = "pfs3";
            var size = new Models.Size(0, Models.Unit.Percent);

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add test pfs3aio file
            await testCommandHelper.AddTestMedia("pfs3aio", "pfs3aio", data: TestPfs3AioBytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize, create: true);

            // arrange - create format command
            var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(), testCommandHelper,
                new List<IPhysicalDrive>(), diskPath, formatType, fileSystem, size);

            // act - execute format command
            var formatResult = await formatCommand.Execute(CancellationToken.None);

            // assert - format failed
            Assert.NotNull(formatResult);
            Assert.True(formatResult.IsFaulted);
            Assert.False(formatResult.IsSuccess);
        }
    }
}