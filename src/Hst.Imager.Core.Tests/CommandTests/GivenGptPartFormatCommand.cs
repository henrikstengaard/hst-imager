using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.ExFat;
using DiscUtils.Partitions;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands.GptCommands;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenGptPartFormatCommand : FsCommandTestBase
{
    [Fact]
    public async Task WhenExFatFormatGptPartitionLargerThan256Gb_Then_PartitionIsFormattedWith128KbClusters()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 300.GB();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create gpt disk with partition using entire disk
        await CreateGptDisk(testCommandHelper, imgPath, size);
        await GptTestHelper.AddGptPartition(testCommandHelper, imgPath, 2048, size / 512 - 34);

        // arrange - gpt partition format command with exfat and volume name "TEST"
        var cancellationTokenSource = new CancellationTokenSource();
        var gptPartFormatCommand = new GptPartFormatCommand(new NullLogger<GptPartFormatCommand>(),
            testCommandHelper, new List<IPhysicalDrive>(), imgPath, 1, GptPartType.ExFat, "TEST");

        // act - execute gpt partition format
        var result = await gptPartFormatCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - partition is exfat formatted
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        using var media = mediaResult.Value;
        var disk = await MediaHelper.ResolveVirtualDisk(media);
        var guidPartitionTable = new GuidPartitionTable(disk);
        await using var partitionStream = guidPartitionTable.Partitions[0].Open();

        // assert - boot sector has 512 bytes per sector and 256 sectors per cluster (128KB clusters)
        var bootSector = new byte[512];
        partitionStream.ReadExactly(bootSector);
        Assert.Equal("EXFAT   ", System.Text.Encoding.ASCII.GetString(bootSector, 3, 8));
        Assert.Equal(9, bootSector[108]);
        Assert.Equal(8, bootSector[109]);

        // assert - exfat file system is valid
        Assert.Empty(ExFatValidator.Validate(partitionStream, partitionStream.Length).Errors);

        // assert - exfat file system is empty
        partitionStream.Position = 0;
        using var exFatFileSystem = new ExFatFileSystem(partitionStream);
        Assert.True(exFatFileSystem.Size > 256.GB());
        Assert.Empty(exFatFileSystem.GetDirectories(string.Empty));
        Assert.Empty(exFatFileSystem.GetFiles(string.Empty));
    }

    [Fact]
    public async Task WhenExFatFormatGptPartitionWithClusterSize1Mb_Then_PartitionIsFormattedWith1MbClusters()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 1.GB();

        // arrange - create gpt disk with partition
        testCommandHelper.AddTestMedia(imgPath, size);
        await CreateGptDisk(testCommandHelper, imgPath, size);
        await GptTestHelper.AddGptPartition(testCommandHelper, imgPath, 2048, size / 512 - 34);

        // arrange - gpt partition format command with exfat and cluster size 1MB
        var gptPartFormatCommand = new GptPartFormatCommand(new NullLogger<GptPartFormatCommand>(),
            testCommandHelper, new List<IPhysicalDrive>(), imgPath, 1, GptPartType.ExFat, "TEST", 1024 * 1024);

        // act - execute gpt partition format
        var result = await gptPartFormatCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);

        // assert - boot sector has 2048 sectors per cluster (1MB clusters)
        var bootSector = await ReadPartitionBootSector(testCommandHelper, imgPath);
        Assert.Equal("EXFAT   ", System.Text.Encoding.ASCII.GetString(bootSector, 3, 8));
        Assert.Equal(9, bootSector[108]);
        Assert.Equal(11, bootSector[109]);
    }

    [Fact]
    public async Task WhenNtfsFormatGptPartitionWithClusterSize_Then_ErrorIsReturned()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();

        // arrange - create gpt disk with partition
        testCommandHelper.AddTestMedia(imgPath, size);
        await CreateGptDisk(testCommandHelper, imgPath, size);
        await GptTestHelper.AddGptPartition(testCommandHelper, imgPath, 2048, size / 512 - 34);

        // arrange - gpt partition format command with ntfs and cluster size 4096
        var gptPartFormatCommand = new GptPartFormatCommand(new NullLogger<GptPartFormatCommand>(),
            testCommandHelper, new List<IPhysicalDrive>(), imgPath, 1, GptPartType.Ntfs, "TEST", 4096);

        // act - execute gpt partition format
        var result = await gptPartFormatCommand.Execute(CancellationToken.None);

        // assert - error is returned as cluster size is not supported for ntfs
        Assert.True(result.IsFaulted);
        Assert.Contains("not supported", result.Error.Message);
    }

    [Theory]
    [InlineData(3000)]
    [InlineData(256)]
    [InlineData(64 * 1024 * 1024)]
    public async Task WhenExFatFormatGptPartitionWithInvalidClusterSize_Then_ErrorIsReturned(int clusterSize)
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();

        // arrange - create gpt disk with partition
        testCommandHelper.AddTestMedia(imgPath, size);
        await CreateGptDisk(testCommandHelper, imgPath, size);
        await GptTestHelper.AddGptPartition(testCommandHelper, imgPath, 2048, size / 512 - 34);

        // arrange - gpt partition format command with exfat and invalid cluster size
        var gptPartFormatCommand = new GptPartFormatCommand(new NullLogger<GptPartFormatCommand>(),
            testCommandHelper, new List<IPhysicalDrive>(), imgPath, 1, GptPartType.ExFat, "TEST", clusterSize);

        // act - execute gpt partition format
        var result = await gptPartFormatCommand.Execute(CancellationToken.None);

        // assert - error is returned for invalid cluster size
        Assert.True(result.IsFaulted);
        Assert.Contains("Invalid exFAT cluster size", result.Error.Message);
    }

    [Fact]
    public async Task WhenExFatFormatGptPartitionWithExistingData_Then_RootDirectoryOnlyContainsFormattedEntries()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();

        // arrange - create gpt disk with partition
        testCommandHelper.AddTestMedia(imgPath, size);
        await CreateGptDisk(testCommandHelper, imgPath, size);
        await GptTestHelper.AddGptPartition(testCommandHelper, imgPath, 2048, size / 512 - 34);

        // arrange - fill partition with data like a previously used physical disk
        var partitionOffset = 2048 * 512;
        var testMedia = testCommandHelper.GetTestMedia(imgPath);
        var data = testCommandHelper.CreateTestData(size - partitionOffset - 34 * 512);
        testMedia.Stream.Position = partitionOffset;
        await testMedia.Stream.WriteAsync(data);

        // arrange - gpt partition format command with exfat
        var gptPartFormatCommand = new GptPartFormatCommand(new NullLogger<GptPartFormatCommand>(),
            testCommandHelper, new List<IPhysicalDrive>(), imgPath, 1, GptPartType.ExFat, "TEST");

        // act - execute gpt partition format
        var result = await gptPartFormatCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);

        // arrange - read root directory cluster
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        using var media = mediaResult.Value;
        var disk = await MediaHelper.ResolveVirtualDisk(media);
        var guidPartitionTable = new GuidPartitionTable(disk);
        await using var partitionStream = guidPartitionTable.Partitions[0].Open();
        var bootSector = new byte[512];
        partitionStream.ReadExactly(bootSector);
        var bytesPerSector = 1 << bootSector[108];
        var sectorsPerCluster = 1 << bootSector[109];
        var clusterHeapOffset = BitConverter.ToUInt32(bootSector, 88);
        var rootDirectoryCluster = BitConverter.ToUInt32(bootSector, 96);
        var rootDirectory = new byte[bytesPerSector * sectorsPerCluster];
        partitionStream.Position =
            (clusterHeapOffset + (long)(rootDirectoryCluster - 2) * sectorsPerCluster) * bytesPerSector;
        partitionStream.ReadExactly(rootDirectory);

        // assert - root directory has allocation bitmap, up-case table and volume label entries
        Assert.Equal(0x81, rootDirectory[0]);
        Assert.Equal(0x82, rootDirectory[32]);
        Assert.Equal(0x83, rootDirectory[64]);

        // assert - rest of root directory is zeroed, so existing data is not read as directory entries
        Assert.All(rootDirectory[96..], b => Assert.Equal(0, b));
    }

    private static async Task<byte[]> ReadPartitionBootSector(TestCommandHelper testCommandHelper, string imgPath)
    {
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        using var media = mediaResult.Value;
        var disk = await MediaHelper.ResolveVirtualDisk(media);
        var guidPartitionTable = new GuidPartitionTable(disk);
        await using var partitionStream = guidPartitionTable.Partitions[0].Open();
        var bootSector = new byte[512];
        partitionStream.ReadExactly(bootSector);
        return bootSector;
    }
}
