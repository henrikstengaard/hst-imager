using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenFormatCommandWithFormatTypePiStorm : FsCommandTestBase
{
    [Theory]
    [InlineData(FormatType.PiStorm)]
    [InlineData(FormatType.PiStormMbr)]
    public async Task When_FormattingDiskWithPiStormMbrPfs3_Then_DiskIsPartitionedWithMbrAnd2Partitions(
        FormatType formatType)
    {
        // arrange - paths
        var diskPath = $"{Guid.NewGuid()}.vhd";
        var outputDir = $"{Guid.NewGuid()}-dir";

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        try
        {
            // arrange - create disk and format command
            var formatCommand = await CreateFormatCommand(testCommandHelper, diskPath, outputDir, formatType, 2.GB());

            // act - execute format command
            var formatResult = await formatCommand.Execute(CancellationToken.None);

            // assert - format is successful
            Assert.True(formatResult.IsSuccess);

            // arrange - get disk info from media
            var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
            using var diskMedia = diskMediaResult.Value;
            var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

            // assert - disk has mbr partition table and no gpt partition table
            Assert.NotNull(diskInfo);
            Assert.Null(diskInfo.GptPartitionTablePart);
            Assert.NotNull(diskInfo.MbrPartitionTablePart);

            // assert - mbr partition table has 2 partitions
            var partitionParts = diskInfo.MbrPartitionTablePart.Parts
                .Where(x => x.PartType == PartType.Partition)
                .ToList();
            Assert.Equal(2, partitionParts.Count);

            // assert - 1st boot partition is FAT32 formatted
            Assert.Equal("FAT32", partitionParts[0].FileSystem);

            // assert - 2nd partition has pistorm rdb bios type
            Assert.Equal(Constants.BiosPartitionTypes.PiStormRdb.ToString(), partitionParts[1].BiosType);
        }
        finally
        {
            TestHelper.DeletePaths(outputDir);
        }
    }

    [Fact]
    public async Task When_FormattingDiskWithPiStormGptPfs3_Then_DiskIsPartitionedWithGptAnd2Partitions()
    {
        // arrange - paths
        var diskPath = $"{Guid.NewGuid()}.vhd";
        var outputDir = $"{Guid.NewGuid()}-dir";

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        try
        {
            // arrange - create disk and format command
            var formatCommand = await CreateFormatCommand(testCommandHelper, diskPath, outputDir,
                FormatType.PiStormGpt, 2.GB());

            // act - execute format command
            var formatResult = await formatCommand.Execute(CancellationToken.None);

            // assert - format is successful
            Assert.True(formatResult.IsSuccess);

            // arrange - get disk info from media
            var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
            using var diskMedia = diskMediaResult.Value;
            var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);

            // assert - disk has gpt partition table
            Assert.NotNull(diskInfo);
            Assert.NotNull(diskInfo.GptPartitionTablePart);

            // assert - gpt partition table has 2 partitions
            var partitionParts = diskInfo.GptPartitionTablePart.Parts
                .Where(x => x.PartType == PartType.Partition)
                .ToList();
            Assert.Equal(2, partitionParts.Count);

            // assert - 1st boot partition is FAT32 formatted and has a size of 1gb
            var bootPartitionPart = partitionParts[0];
            Assert.Equal("FAT32", bootPartitionPart.FileSystem);
            Assert.True(bootPartitionPart.Size > 920.MB() && bootPartitionPart.Size < 1080.MB());

            // assert - 2nd partition has pistorm rdb guid type and size of remaining disk space
            var piStormRdbPartitionPart = partitionParts[1];
            Assert.Equal(Constants.GuidPartitionTypes.PiStormRdb.ToString(), piStormRdbPartitionPart.GuidType);
            Assert.Equal(Constants.FileSystemNames.PiStormRdb, piStormRdbPartitionPart.FileSystem);
            Assert.True(piStormRdbPartitionPart.Size > 920.MB() && piStormRdbPartitionPart.Size < 1080.MB());
        }
        finally
        {
            TestHelper.DeletePaths(outputDir);
        }

        // arrange - get pistorm rdb media from gpt partition 2
        var mediaResult = await testCommandHelper.GetReadableFileMedia(diskPath);
        using var media = mediaResult.Value;
        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(media, Path.Combine("gpt", "2"),
            Path.DirectorySeparatorChar.ToString());
        Assert.True(piStormRdbMediaResult.HasPiStormRdb);

        // assert - pistorm rdb has workbench partition formatted with pfs3
        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(piStormRdbMediaResult.Media);
        Assert.NotNull(rigidDiskBlock);
        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
        Assert.NotEmpty(partitionBlocks);
        Assert.Equal("DH0", partitionBlocks[0].DriveName);
        Assert.Equal(TestHelper.Pfs3DosType, partitionBlocks[0].DosType);
    }

    [Fact]
    public async Task When_FormattingDiskWithPiStormGptLessThan2Gb_Then_FormatReturnsError()
    {
        // arrange - paths
        var diskPath = $"{Guid.NewGuid()}.vhd";
        var outputDir = $"{Guid.NewGuid()}-dir";

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        try
        {
            // arrange - create disk and format command
            var formatCommand = await CreateFormatCommand(testCommandHelper, diskPath, outputDir,
                FormatType.PiStormGpt, 1.GB());

            // act - execute format command
            var formatResult = await formatCommand.Execute(CancellationToken.None);

            // assert - format failed
            Assert.True(formatResult.IsFaulted);
        }
        finally
        {
            TestHelper.DeletePaths(outputDir);
        }
    }

    private static async Task<FormatCommand> CreateFormatCommand(TestCommandHelper testCommandHelper,
        string diskPath, string outputDir, FormatType formatType, long diskSize)
    {
        const string fileSystem = "pfs3";
        const string fileSystemPath = "pfs3aio";

        // add test pfs3aio file
        await testCommandHelper.AddTestMedia(fileSystemPath, fileSystemPath, data: TestHelper.Pfs3AioBytes);

        // add disk
        testCommandHelper.AddTestMedia(diskPath, 0);
        await testCommandHelper.GetWritableMedia(new List<IPhysicalDrive>(), diskPath, size: diskSize,
            create: true);

        return new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
            testCommandHelper, new List<IPhysicalDrive>(), diskPath, formatType, fileSystem,
            fileSystemPath, outputDir, new Size(), new Size(), false, true);
    }
}
