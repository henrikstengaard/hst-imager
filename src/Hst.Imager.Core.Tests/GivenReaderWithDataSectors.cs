namespace Hst.Imager.Core.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Core;
    using Xunit;

    public class GivenDataSectorReaderWithDataSectors : SectorTestBase
    {
        [Fact]
        public void WhenReadSectorsThenDataSectorsAreReturned()
        {
            var sector1 = CreateSector();
            var sector2 = CreateSector();
            var sector3 = CreateSector();
            var sector4 = CreateSector();
            var sector5 = CreateSector(1);
            var sector6 = CreateSector(2);

            var diskBytes = new List<byte>(6 * SectorSize);
            diskBytes.AddRange(sector1);
            diskBytes.AddRange(sector2);
            diskBytes.AddRange(sector3);
            diskBytes.AddRange(sector4);
            diskBytes.AddRange(sector5);
            diskBytes.AddRange(sector6);

            // act - read sectors including zero filled
            var sectors = DataSectorReader.Read(diskBytes.ToArray(), includeZeroFilled: true).ToList();

            // assert - 6 sectors are read
            Assert.Equal(6, sectors.Count);

            // assert sector 1
            var sector = sectors[0];
            Assert.Equal(0, sector.Start);
            Assert.Equal(SectorSize - 1, sector.End);
            Assert.True(sector.IsZeroFilled);

            // assert sector 2
            sector = sectors[1];
            Assert.Equal(SectorSize, sector.Start);
            Assert.Equal(2 * SectorSize - 1, sector.End);
            Assert.True(sector.IsZeroFilled);
            
            // assert sector 3
            sector = sectors[2];
            Assert.Equal(2 * SectorSize, sector.Start);
            Assert.Equal(3 * SectorSize - 1, sector.End);
            Assert.True(sector.IsZeroFilled);

            // assert sector 4
            sector = sectors[3];
            Assert.Equal(3 * SectorSize, sector.Start);
            Assert.Equal(4 * SectorSize - 1, sector.End);
            Assert.True(sector.IsZeroFilled);

            // assert sector 5
            sector = sectors[4];
            Assert.Equal(4 * SectorSize, sector.Start);
            Assert.Equal(5 * SectorSize - 1, sector.End);
            Assert.False(sector.IsZeroFilled);

            // assert sector 6
            sector = sectors[5];
            Assert.Equal(5 * SectorSize, sector.Start);
            Assert.Equal(6 * SectorSize - 1, sector.End);
            Assert.False(sector.IsZeroFilled);
        }
        
        [Fact]
        public void WhenByteIsNotZeroAtAnyOffsetInSectorThenSectorIsNotZeroFilled()
        {
            const int sectorSize = 512;
            
            for (var i = 0; i < sectorSize; i++)
            {
                // arrange - sector bytes with value 1 at offset i
                var sectorBytes = new byte[sectorSize];
                sectorBytes[i] = 1;
                
                // act - check if sector bytes are zero filled
                var isZeroFilled = DataSectorReader.IsZeroFilled(sectorBytes, 0, sectorSize);
                
                // assert - sector is not zero filled
                Assert.False(isZeroFilled);
            }
        }
    }
}