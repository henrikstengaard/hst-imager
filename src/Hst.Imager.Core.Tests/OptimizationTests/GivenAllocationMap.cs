using System.Linq;
using Hst.Imager.Core.Optimization;
using Xunit;

namespace Hst.Imager.Core.Tests.OptimizationTests;

public class GivenAllocationMap
{
    [Fact]
    public void When_NoFreeRangesAreAdded_Then_AllBytesAreUsed()
    {
        // arrange - allocation map without free ranges
        var allocationMap = new AllocationMap();

        // act - get used ranges
        var usedRanges = allocationMap.GetUsedRanges(0, 1000).ToList();

        // assert - one used range covering all bytes
        Assert.Equal([new ByteRange(0, 1000)], usedRanges);
    }

    [Fact]
    public void When_OverlappingAndAdjacentFreeRangesAreAdded_Then_FreeRangesAreMerged()
    {
        // arrange - allocation map with overlapping, adjacent and unordered free ranges
        var allocationMap = new AllocationMap();
        allocationMap.AddFree(1024, 512);
        allocationMap.AddFree(0, 512);
        allocationMap.AddFree(1536, 512);
        allocationMap.AddFree(1800, 1000);

        // act - get free ranges
        var freeRanges = allocationMap.FreeRanges;

        // assert - free ranges are sorted and merged
        Assert.Equal([new ByteRange(0, 512), new ByteRange(1024, 1776)], freeRanges);
        Assert.Equal(2288, allocationMap.FreeBytes);
    }

    [Fact]
    public void When_GettingUsedRangesWithinFreeRanges_Then_FreeRangesAreExcluded()
    {
        // arrange - allocation map with free ranges
        var allocationMap = new AllocationMap();
        allocationMap.AddFree(100, 100);
        allocationMap.AddFree(400, 100);
        allocationMap.AddFree(900, 300);

        // act - get used ranges from offset 150 to 1000
        var usedRanges = allocationMap.GetUsedRanges(150, 850).ToList();

        // assert - used ranges exclude free ranges and are limited to offset and length
        Assert.Equal([new ByteRange(200, 200), new ByteRange(500, 400)], usedRanges);
    }

    [Fact]
    public void When_GettingUsedRangesCompletelyFree_Then_NoUsedRangesAreReturned()
    {
        // arrange - allocation map with free range
        var allocationMap = new AllocationMap();
        allocationMap.AddFree(0, 4096);

        // act - get used ranges within free range
        var usedRanges = allocationMap.GetUsedRanges(1024, 1024).ToList();

        // assert - no used ranges
        Assert.Empty(usedRanges);
    }
}
