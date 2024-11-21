using System.Threading.Tasks;
using System;
using Xunit;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Threading;
using PartitionTable = Hst.Imager.Core.Models.PartitionTable;
using DiscUtils.Partitions;
using System.Linq;

namespace Hst.Imager.Core.Tests.CommandTests
{
    public class GivenFormatCommand : FsCommandTestBase
    {
        [Fact]
        public async Task When_FormattingDiskWithMbrFat32_Then_DiskIsFormatted()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = PartitionTable.Mbr;
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
        public async Task When_FormattingDiskWithMbrFat32AndSize50Percent_Then_DiskIsFormatted()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = PartitionTable.Mbr;
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
        public async Task When_FormattingDiskWithMbrNtfs_Then_DiskIsFormatted()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = PartitionTable.Mbr;
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
        public async Task When_FormattingDiskWithGptFat32_Then_DiskIsFormatted()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = PartitionTable.Gpt;
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
        public async Task When_FormattingDiskWithGptFat32AndSize50Percent_Then_DiskIsFormatted()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = PartitionTable.Gpt;
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
        public async Task When_FormattingDiskWithGptNtfs_Then_DiskIsFormatted()
        {
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 100.MB();
            var partitionTable = PartitionTable.Gpt;
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
    }
}