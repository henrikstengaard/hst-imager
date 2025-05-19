using System.Linq;
using Hst.Imager.Core.FileSystems;
using Xunit;

namespace Hst.Imager.Core.Tests.HelperTests;

public class GivenFileSystemHelperCalculatingRdbPartitionSizes
{
    [Theory]
    [InlineData(1024L * 1024 * 64000, 1)] // 64 GB
    [InlineData(1024L * 1024 * 130000, 2)] // 130 GB
    [InlineData(1024L * 1024 * 180000, 2)] // 180 GB
    [InlineData(1024L * 1024 * 256000, 3)] // 256 GB
    public void When_UsingPfs3MaxPartitionSize_Then_PartitionsAreCreated(long diskSize, 
        int expectedPartitionCount)
    {
        // act
        var partitionSizes = FileSystemHelper.CalculateRdbPartitionSizes(diskSize,
            FileSystemHelper.Pfs3MaxPartitionSize).ToList();
        
        // assert - partition sizes are created
        Assert.NotEmpty(partitionSizes);
        
        // assert - disk size is equal to sum of partition sizes
        Assert.Equal(diskSize, partitionSizes.Sum(x => x));

        // assert - expected number of partitions are created
        Assert.Equal(expectedPartitionCount, partitionSizes.Count);

        // assert - partition sizes are max partition size except the last one
        Assert.Equal(expectedPartitionCount - 1, partitionSizes.Count(x => x == FileSystemHelper.Pfs3MaxPartitionSize));

        // assert - last partition is smaller than max partition size
        Assert.Equal(1, partitionSizes.Count(x => x != FileSystemHelper.Pfs3MaxPartitionSize));
    }

    [Theory]
    [InlineData(1024L * 1024 * 110000, 2)] // 110 GB
    [InlineData(1024L * 1024 * 120000, 2)] // 120 GB
    [InlineData(1024L * 1024 * 220000, 3)] // 220 GB
    public void When_UsingPfs3MaxPartitionSizeCloseToDiskSize_Then_LastPartitionsAreAdjusted(long diskSize, 
        int expectedPartitionCount)
    {
        // act
        var partitionSizes = FileSystemHelper.CalculateRdbPartitionSizes(diskSize,
            FileSystemHelper.Pfs3MaxPartitionSize).ToList();
        
        // assert - partition sizes are created
        Assert.NotEmpty(partitionSizes);

        // assert - disk size is equal to sum of partition sizes
        Assert.Equal(diskSize, partitionSizes.Sum(x => x));
        
        // assert - expected number of partitions are created
        Assert.Equal(expectedPartitionCount, partitionSizes.Count);

        // assert - partition sizes are max partition size except the last two partitions
        Assert.Equal(expectedPartitionCount - 2, partitionSizes.Count(x => x == FileSystemHelper.Pfs3MaxPartitionSize));

        // assert - last two partition are smaller than max partition size
        Assert.Equal(2, partitionSizes.Count(x => x != FileSystemHelper.Pfs3MaxPartitionSize));
    }
}