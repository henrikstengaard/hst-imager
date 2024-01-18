using System.Collections.Generic;
using System.Linq;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.PartitionTables;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenDiskParts
{
    [Fact]
    public void WhenMergingOverlappingUnallocatedPartsThenPartsAreMerged()
    {
        // arrange
        var parts = new List<PartInfo>
        {
            new()
            {
                StartOffset = 0,
                EndOffset = 1000000,
                PartType = PartType.Unallocated
            },
            new()
            {
                StartOffset = 500000,
                EndOffset = 1000000,
                PartType = PartType.Unallocated
            }
        };
        
        // act
        var mergedParts = DiskPartHelper.MergeOverlappingParts(parts).ToList();
        
        // assert
        Assert.Single(mergedParts);
        Assert.Equal(0, mergedParts[0].StartOffset);
        Assert.Equal(1000000, mergedParts[0].EndOffset);
    }
}