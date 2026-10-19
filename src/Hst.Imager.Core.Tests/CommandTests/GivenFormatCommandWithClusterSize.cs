using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Partitions;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenFormatCommandWithClusterSize
{
    [Fact]
    public async Task When_FormattingDiskWithGptExFatAndClusterSize64Kb_Then_PartitionIsFormattedWith64KbClusters()
    {
        // arrange - disk path, size and test command helper
        var diskPath = $"{Guid.NewGuid()}.img";
        var diskSize = 1.GB();
        var testCommandHelper = new TestCommandHelper();
        testCommandHelper.AddTestMedia(diskPath, diskSize);

        // arrange - create format command with gpt, exfat and cluster size 64KB
        var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
            testCommandHelper, new List<IPhysicalDrive>(), diskPath, FormatType.Gpt, "exfat",
            string.Empty, string.Empty, new Size(), new Size(), false, false, 65536);

        // act - execute format command
        var result = await formatCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - partition boot sector is exfat with 128 sectors per cluster (64KB clusters)
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
        using var media = mediaResult.Value;
        var disk = await MediaHelper.ResolveVirtualDisk(media);
        var guidPartitionTable = new GuidPartitionTable(disk);
        await using var partitionStream = guidPartitionTable.Partitions[0].Open();
        var bootSector = new byte[512];
        partitionStream.ReadExactly(bootSector);
        Assert.Equal("EXFAT   ", System.Text.Encoding.ASCII.GetString(bootSector, 3, 8));
        Assert.Equal(7, bootSector[109]);
    }

    [Theory]
    [InlineData(FormatType.Gpt, "ntfs", 4096)]
    [InlineData(FormatType.Mbr, "ntfs", 4096)]
    [InlineData(FormatType.Mbr, "fat32", 3000)]
    [InlineData(FormatType.Rdb, "pds3", 4096)]
    public async Task When_FormattingDiskWithUnsupportedClusterSize_Then_ErrorIsReturnedBeforeDiskIsChanged(
        FormatType formatType, string fileSystem, int clusterSize)
    {
        // arrange - disk path, size and test command helper
        var diskPath = $"{Guid.NewGuid()}.img";
        var diskSize = 100.MB();
        var testCommandHelper = new TestCommandHelper();
        testCommandHelper.AddTestMedia(diskPath, diskSize);

        // arrange - create format command with unsupported cluster size
        var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
            testCommandHelper, new List<IPhysicalDrive>(), diskPath, formatType, fileSystem,
            string.Empty, string.Empty, new Size(), new Size(), false, false, clusterSize);

        // act - execute format command
        var result = await formatCommand.Execute(CancellationToken.None);

        // assert - error is returned
        Assert.True(result.IsFaulted);

        // assert - disk is not changed and has no partition tables
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), diskPath);
        using var media = mediaResult.Value;
        var diskInfo = await testCommandHelper.ReadDiskInfo(media);
        Assert.Null(diskInfo.MbrPartitionTablePart);
        Assert.Null(diskInfo.GptPartitionTablePart);
        Assert.Null(diskInfo.RdbPartitionTablePart);
    }
}
