using System;
using System.IO;
using System.Threading.Tasks;
using DiscUtils.ExFat;
using DiscUtils.Streams;
using Hst.Core.IO;
using Hst.Imager.Core.FileSystems.ExFat;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenExFatFormatter
{
    private const long PartitionOffset = 1024 * 1024;
    private const long Size1Mb = 1024 * 1024;
    private const long Size100Mb = 100 * Size1Mb;
    private const long Size1Gb = 1000 * Size1Mb;
    private const long Size300Gb = 300 * Size1Gb;
    private const long Size2Tb = 2000398934016 - PartitionOffset; // size of 2TB disk minus partition offset
    private const long Size4Tb = 4000787030016 - PartitionOffset; // size of 4TB disk minus partition offset

    [Theory]
    [InlineData(Size1Mb, 512, 0, 4096)]
    [InlineData(Size100Mb, 512, 0, 4096)]
    [InlineData(Size1Gb, 512, 0, 32768)]
    [InlineData(Size1Gb, 4096, 0, 32768)]
    [InlineData(Size1Gb, 512, 512, 512)]
    [InlineData(Size1Gb, 512, 32 * 1024 * 1024, 32 * 1024 * 1024)]
    [InlineData(Size300Gb, 512, 0, 131072)]
    [InlineData(Size2Tb, 512, 0, 131072)]
    [InlineData(Size2Tb, 4096, 0, 131072)]
    [InlineData(Size4Tb, 512, 0, 131072)]
    [InlineData(Size4Tb, 4096, 0, 131072)]
    public async Task WhenFormatPartitionThenExFatFileSystemIsValid(long size, int bytesPerSector, int clusterSize,
        int expectedClusterSize)
    {
        // arrange - blank stream with partition offset and size
        await using var stream = new BlockMemoryStream();
        stream.SetLength(PartitionOffset + size);

        // act - exfat format partition
        await ExFatFormatter.FormatPartition(stream, PartitionOffset, size, bytesPerSector, "UNITTEST", clusterSize);

        // assert - exfat file system is valid
        using var partitionStream = new SubStream(stream, PartitionOffset, size);
        var validator = ExFatValidator.Validate(partitionStream, size);
        Assert.Empty(validator.Errors);
        Assert.Equal(expectedClusterSize, validator.BytesPerCluster);
        Assert.Equal("UNITTEST", validator.VolumeLabel);
    }

    [Theory]
    [InlineData(Size100Mb, 0)]
    [InlineData(Size1Gb, 512)]
    [InlineData(Size300Gb, 0)]
    public async Task WhenFormatPartitionThenFilesCanBeWrittenAndReadUsingExFatFileSystem(long size, int clusterSize)
    {
        // arrange - blank stream with partition offset and size
        await using var stream = new BlockMemoryStream();
        stream.SetLength(PartitionOffset + size);

        // act - exfat format partition
        await ExFatFormatter.FormatPartition(stream, PartitionOffset, size, 512, "UNITTEST", clusterSize);

        // act - write file to exfat file system
        var data = new byte[100000];
        Random.Shared.NextBytes(data);
        using (var exFatFileSystem = new ExFatFileSystem(new SubStream(stream, PartitionOffset, size)))
        {
            Assert.Empty(exFatFileSystem.GetFiles(string.Empty));
            Assert.Empty(exFatFileSystem.GetDirectories(string.Empty));
            exFatFileSystem.CreateDirectory("dir");
            await using var fileStream = exFatFileSystem.OpenFile(@"dir\file.bin", FileMode.Create, FileAccess.Write);
            await fileStream.WriteAsync(data);
        }

        // assert - file can be read from exfat file system
        using (var exFatFileSystem = new ExFatFileSystem(new SubStream(stream, PartitionOffset, size)))
        {
            await using var fileStream = exFatFileSystem.OpenFile(@"dir\file.bin", FileMode.Open, FileAccess.Read);
            var readData = new byte[data.Length];
            fileStream.ReadExactly(readData);
            Assert.Equal(data, readData);
        }
    }

    [Fact]
    public async Task WhenFormatPartitionWithExistingDataThenExFatFileSystemIsValid()
    {
        // arrange - stream filled with data like a previously used disk
        var size = Size100Mb;
        await using var stream = new BlockMemoryStream();
        var data = new byte[PartitionOffset + size];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256 | 0x80);
        }
        await stream.WriteAsync(data);

        // act - exfat format partition
        await ExFatFormatter.FormatPartition(stream, PartitionOffset, size, 512, "UNITTEST");

        // assert - exfat file system is valid
        using var partitionStream = new SubStream(stream, PartitionOffset, size);
        var validator = ExFatValidator.Validate(partitionStream, size);
        Assert.Empty(validator.Errors);
    }

    [Fact]
    public async Task WhenFormatPartitionWithVolumeLabelLongerThan11CharactersThenVolumeLabelIsTruncated()
    {
        // arrange - blank stream with partition offset and size
        var size = Size100Mb;
        await using var stream = new BlockMemoryStream();
        stream.SetLength(PartitionOffset + size);

        // act - exfat format partition with long volume label
        await ExFatFormatter.FormatPartition(stream, PartitionOffset, size, 512, "Microsoft basic data");

        // assert - exfat file system is valid and volume label is truncated to 11 characters
        using var partitionStream = new SubStream(stream, PartitionOffset, size);
        var validator = ExFatValidator.Validate(partitionStream, size);
        Assert.Empty(validator.Errors);
        Assert.Equal("Microsoft b", validator.VolumeLabel);
    }

    [Fact]
    public void WhenCreateLayoutWithTooSmallClusterSizeForVolumeThenExceptionIsThrown()
    {
        // act & assert - 512 byte clusters on 4TB volume exceeds max cluster count
        Assert.Throws<ArgumentOutOfRangeException>(() => ExFatFormatter.CreateLayout(Size4Tb, 512, 512));
    }

    [Fact]
    public void WhenCreateLayoutWithVolumeSmallerThan1MbThenExceptionIsThrown()
    {
        // act & assert - volume smaller than 1MB is not supported by exfat
        Assert.Throws<ArgumentOutOfRangeException>(() => ExFatFormatter.CreateLayout(Size1Mb - 512, 512));
    }

    [Fact]
    public void WhenCreateLayoutFor2TbVolumeThenFatAndClusterHeapAreAlignedToClusterSize()
    {
        // act - create layout for 2tb volume
        var layout = ExFatFormatter.CreateLayout(Size2Tb, 512);

        // assert - fat offset and cluster heap offset are aligned to 128kb cluster size
        Assert.Equal(256, layout.SectorsPerCluster);
        Assert.Equal(0U, layout.FatOffset % 256);
        Assert.Equal(0U, layout.ClusterHeapOffset % 256);
        Assert.True((ulong)layout.FatLength * 512 >= ((ulong)layout.ClusterCount + 2) * 4);
    }
}
