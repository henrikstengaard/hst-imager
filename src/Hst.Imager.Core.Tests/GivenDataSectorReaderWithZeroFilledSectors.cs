namespace Hst.Imager.Core.Tests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

public class GivenDataSectorReaderWithZeroFilledSectors : SectorTestBase
{
    [Fact]
    public async Task WhenReadSectorsThenAllSectorsAreZeroFilled()
    {
        // arrange - zero filled sectors
        var zeroFilledSectorBytes = new byte[512 * 1024];
        var stream = new MemoryStream(zeroFilledSectorBytes);

        // create data sector reader
        var reader = new DataSectorReader(stream, bufferSize: SectorSize);

        // act - read sectors
        var sectors = new List<Sector>();
        SectorResult result;
        do
        {
            result = await reader.ReadNext();
            sectors.AddRange(result.Sectors);
        } while (!result.EndOfSectors);

        // assert - all sectors are zero filled
        Assert.True(sectors.All(x => x.IsZeroFilled));

        // assert - sum of sectors length is equal to  
        Assert.Equal(zeroFilledSectorBytes.Length, sectors.Sum(x => x.Data.Length));
    }
}