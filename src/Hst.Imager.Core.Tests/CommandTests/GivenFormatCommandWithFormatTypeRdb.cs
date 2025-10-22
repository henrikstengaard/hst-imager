using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenFormatCommandWithFormatTypeRdb : FsCommandTestBase
{
    [Fact]
    public async Task When_Formatting2GbDiskWithRdbPfs3AndKickstart31_Then_DiskIsPartitionedAndFormattedWith2Partitions()
    {
        var diskPath = $"{Guid.NewGuid()}.vhd";
        var diskSize = 2.GB();
        const FormatType formatType = FormatType.Rdb;
        const string fileSystem = "pfs3";
        const string fileSystemPath = "pfs3aio";
        var outputDir = $"{Guid.NewGuid()}-dir";
        const bool kickstart31 = true;

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
                fileSystemPath, outputDir, new Size(), new Size(), false, kickstart31);

            // act - execute format command
            var formatResult = await formatCommand.Execute(CancellationToken.None);

            // assert - format is successful
            Assert.NotNull(formatResult);
            Assert.True(formatResult.IsSuccess);

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
    public async Task When_Formatting16GbDiskWithRdbPfs3AndKickstart31_Then_DiskIsPartitionedAndFormattedWith2Partitions()
    {
        var diskPath = $"{Guid.NewGuid()}.vhd";
        var diskSize = 16.GB();
        const FormatType formatType = FormatType.Rdb;
        const string fileSystem = "pfs3";
        const string fileSystemPath = "pfs3aio";
        var outputDir = $"{Guid.NewGuid()}-dir";
        const bool kickstart31 = true;

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
                fileSystemPath, outputDir, new Size(), new Size(), false, kickstart31);

            // act - execute format command
            var formatResult = await formatCommand.Execute(CancellationToken.None);

            // assert - format is successful
            Assert.NotNull(formatResult);
            Assert.True(formatResult.IsSuccess);

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
            Assert.True(partitionPart1.PercentSize is >= 5 and <= 10);
            var partitionPart2 = partitionParts[1];
            Assert.Equal("PFS\\3", partitionPart2.FileSystem);
            Assert.True(partitionPart2.PercentSize is >= 90 and <= 95);
        }
        finally
        {
            TestHelper.DeletePaths(outputDir);
        }
    }
}