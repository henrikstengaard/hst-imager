namespace Hst.Imager.Core.Tests;

using System.Linq;
using Xunit;

public class GivenDataSectorReaderWithZeroFilledSectors : SectorTestBase
{
    [Fact]
    public void WhenReadSectorsExcludingZeroFilledThenNoSectorsAreReturned()
    {
        // arrange - zero filled sectors
        var zeroFilledSectorBytes = new byte[512 * 1024];

        // act - read data sectors
        var sectors = DataSectorReader.Read(zeroFilledSectorBytes);

        // assert - no sectors, all sectors are zero filled
        Assert.Empty(sectors);
    }
    
    [Fact]
    public void WhenReadSectorsIncludingZeroFilledThenAllSectorsAreReturned()
    {
        // arrange - zero filled sectors
        var data = new byte[512 * 1024];

        // act - read data sectors
        var sectors = DataSectorReader.Read(data, includeZeroFilled: true).ToList();

        // assert - sectors are returned
        Assert.NotEmpty(sectors);
        
        // assert - all sectors are zero filled
        Assert.True(sectors.All(x => x.IsZeroFilled));

        // assert - sum of sector sizes are equal to data length 
        Assert.Equal(data.Length, sectors.Sum(x => x.Size));
    }
}