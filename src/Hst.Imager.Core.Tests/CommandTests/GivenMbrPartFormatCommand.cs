using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.ExFat;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenMbrPartFormatCommand : FsCommandTestBase
{
    [Fact]
    public async Task WhenFatFormatMbrPartition_Then_PartitionIsFormattedAndEmpty()
    {
        // arrange - path, size and test command helper
        var imgPath = $"fat16format.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create mbr disk
        await CreateMbrDiskWithPartition(testCommandHelper, imgPath, size, BiosPartitionTypes.Fat16);

        // arrange - mbr partition format command with type volume name "TEST"
        var cancellationTokenSource = new CancellationTokenSource();
        var mbrPartFormatCommand = new MbrPartFormatCommand(new NullLogger<MbrPartFormatCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, 1, "TEST", "Fat16");

        // act - execute mbr partition format
        var result = await mbrPartFormatCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - partition is fat16 formatted
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        using var media = mediaResult.Value;
        var disk = await MediaHelper.ResolveVirtualDisk(media);
        var biosPartitionTable = new BiosPartitionTable(disk);
        using var fatFileSystem = new FatFileSystem(biosPartitionTable.Partitions[0].Open());
        Assert.Equal(FatType.Fat16, fatFileSystem.FatVariant);
        Assert.NotEqual(0, fatFileSystem.Size);
        Assert.NotEqual(0, fatFileSystem.TotalSectors);
        Assert.Empty(fatFileSystem.GetDirectories(string.Empty));
        Assert.Empty(fatFileSystem.GetFiles(string.Empty));
    }

    [Fact]
    public async Task WhenExFatFormatMbrPartitionLargerThan256Gb_Then_PartitionIsFormattedWith128KbClusters()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 300.GB();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create mbr disk with exfat partition
        await CreateMbrDiskWithPartition(testCommandHelper, imgPath, size, BiosPartitionTypes.Ntfs);

        // arrange - mbr partition format command with exfat and volume name "TEST"
        var cancellationTokenSource = new CancellationTokenSource();
        var mbrPartFormatCommand = new MbrPartFormatCommand(new NullLogger<MbrPartFormatCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, 1, "TEST", "ExFat");

        // act - execute mbr partition format
        var result = await mbrPartFormatCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - partition is exfat formatted
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        using var media = mediaResult.Value;
        var disk = await MediaHelper.ResolveVirtualDisk(media);
        var biosPartitionTable = new BiosPartitionTable(disk);
        await using var partitionStream = biosPartitionTable.Partitions[0].Open();

        // assert - boot sector has 512 bytes per sector and 256 sectors per cluster (128KB clusters)
        var bootSector = new byte[512];
        partitionStream.ReadExactly(bootSector);
        Assert.Equal("EXFAT   ", System.Text.Encoding.ASCII.GetString(bootSector, 3, 8));
        Assert.Equal(9, bootSector[108]);
        Assert.Equal(8, bootSector[109]);

        // assert - exfat file system is empty
        partitionStream.Position = 0;
        using var exFatFileSystem = new ExFatFileSystem(partitionStream);
        Assert.True(exFatFileSystem.Size > 256.GB());
        Assert.Empty(exFatFileSystem.GetDirectories(string.Empty));
        Assert.Empty(exFatFileSystem.GetFiles(string.Empty));
    }

    [Fact]
    public async Task WhenFat32FormatMbrPartitionWithClusterSize8Kb_Then_PartitionIsFormattedWith8KbClusters()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 1.GB();

        // arrange - create mbr disk with fat32 partition
        testCommandHelper.AddTestMedia(imgPath, size);
        await CreateMbrDiskWithPartition(testCommandHelper, imgPath, size, BiosPartitionTypes.Fat32Lba);

        // arrange - mbr partition format command with fat32 and cluster size 8KB
        var mbrPartFormatCommand = new MbrPartFormatCommand(new NullLogger<MbrPartFormatCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, 1, "TEST", "Fat32", 8192);

        // act - execute mbr partition format
        var result = await mbrPartFormatCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - partition is fat32 formatted with 8KB clusters
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        using var media = mediaResult.Value;
        var disk = await MediaHelper.ResolveVirtualDisk(media);
        var biosPartitionTable = new BiosPartitionTable(disk);
        using var fatFileSystem = new FatFileSystem(biosPartitionTable.Partitions[0].Open());
        Assert.Equal(FatType.Fat32, fatFileSystem.FatVariant);
        Assert.Equal(8192, fatFileSystem.ClusterSize);
    }

    [Fact]
    public async Task WhenFat32FormatMbrPartitionWithTooLargeClusterSize_Then_ErrorIsReturned()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();

        // arrange - create mbr disk with fat32 partition
        testCommandHelper.AddTestMedia(imgPath, size);
        await CreateMbrDiskWithPartition(testCommandHelper, imgPath, size, BiosPartitionTypes.Fat32Lba);

        // arrange - mbr partition format command with fat32 and cluster size 64KB giving too few clusters
        var mbrPartFormatCommand = new MbrPartFormatCommand(new NullLogger<MbrPartFormatCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, 1, "TEST", "Fat32", 65536);

        // act - execute mbr partition format
        var result = await mbrPartFormatCommand.Execute(CancellationToken.None);

        // assert - error is returned as fat32 requires a minimum of 65536 clusters
        Assert.True(result.IsFaulted);
        Assert.Contains("clusters", result.Error.Message);
    }
}
