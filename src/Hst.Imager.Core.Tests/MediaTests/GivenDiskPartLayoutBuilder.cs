using System.Collections.Generic;
using System.Linq;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Medias;
using Xunit;

namespace Hst.Imager.Core.Tests.MediaTests;

public class GivenDiskPartLayoutBuilder
{
    [Fact]
    public void When_CreatingUnallocatedPartsWithOverlappingParts_Then_UnallocatedPartsAreCreatedAroundMergedParts()
    {
        // arrange
        const long diskSize = 8192;
        var parts = new List<PartInfo>
        {
            new() { StartOffset = 1024, EndOffset = 3071, StartSector = 2, EndSector = 5 },
            new() { StartOffset = 2048, EndOffset = 4095, StartSector = 4, EndSector = 7 }
        };

        // act
        var unallocatedParts = DiskPartLayoutBuilder.CreateUnallocatedParts(diskSize, diskSize / 512, 0,
            PartitionTableType.None, parts, true, false).ToList();

        // assert
        Assert.Equal(2, unallocatedParts.Count);
        Assert.All(unallocatedParts, x => Assert.Equal(PartType.Unallocated, x.PartType));

        Assert.Equal(0, unallocatedParts[0].StartOffset);
        Assert.Equal(1023, unallocatedParts[0].EndOffset);
        Assert.Equal(1024, unallocatedParts[0].Size);

        Assert.Equal(4096, unallocatedParts[1].StartOffset);
        Assert.Equal(8191, unallocatedParts[1].EndOffset);
        Assert.Equal(4096, unallocatedParts[1].Size);
        Assert.Equal(8, unallocatedParts[1].StartSector);
        Assert.Equal(15, unallocatedParts[1].EndSector);
    }

    [Fact]
    public void When_CreatingUnallocatedPartsForZeroSizeDisk_Then_NoPartsAreCreated()
    {
        // act
        var unallocatedParts = DiskPartLayoutBuilder.CreateUnallocatedParts(0, 0, 0,
            PartitionTableType.None, new List<PartInfo>(), true, false).ToList();

        // assert
        Assert.Empty(unallocatedParts);
    }
}
