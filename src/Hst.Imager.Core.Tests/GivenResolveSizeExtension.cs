using System;
using Hst.Imager.Core.Extensions;
using Hst.Imager.Core.Models;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenResolveSizeExtensionWithDiskSize
{
    [Theory]
    [InlineData(1000L)]
    [InlineData(2000L)]
    [InlineData(5000L)]
    [InlineData(10000L)]
    public void When_ResolveSizeInByteUnit_Then_ResultIsSameAsValue(long value)
    {
        // arrange
        var diskSize = 10000L;  
        var size = new Size(value, Unit.Bytes);

        // act
        var result = diskSize.ResolveSize(size);

        // assert
        Assert.Equal(value, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(100)]
    public void When_ResolveSizeInPercentUnit_Then_ResultIs50PercentOfDiskSize(int percent)
    {
        // arrange
        var diskSize = 10000L;
        var size = new Size(percent, Unit.Percent);

        // act
        var result = diskSize.ResolveSize(size);

        // assert
        Assert.Equal(Convert.ToInt64(diskSize / 100 * percent), result);
    }
    
    [Fact]
    public void When_ResolveSizeLargerThanDiskSize_Then_ResultIsDiskSize()
    {
        // arrange
        var diskSize = 10000L;
        var size = new Size(20000, Unit.Bytes);

        // act
        var result = diskSize.ResolveSize(size);

        // assert
        Assert.Equal(10000L, result);
    }

}