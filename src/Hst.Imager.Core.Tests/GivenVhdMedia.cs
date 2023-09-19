using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenVhdMedia
{
    [Fact]
    public async Task WhenWritingSectorsInDescendingOrderThenSectorsAreInAscendingOrderWhenRead()
    {
        var path = $"{Guid.NewGuid()}.vhd";
        var sectorBytes = new byte[512];

        // arrange - list of sector number and value
        var sectors = new List<Tuple<int, int>>();
            
        try
        {
            var testCommandHelper = new TestCommandHelper();

            // arrange - create 100mb vha file media
            var mediaResult = await testCommandHelper.GetWritableFileMedia(path, 100.MB(), create: true);
            
            // act - write sectors in descending order
            using (var media = mediaResult.Value)
            {
                var mediaStream = media.Stream;
                for (var i = 255; i > 0; i--)
                {
                    Array.Fill<byte>(sectorBytes, (byte)i);
                    mediaStream.Seek(512 * i, SeekOrigin.Begin);
                    await mediaStream.WriteAsync(sectorBytes, 0, sectorBytes.Length);
                }
            }
            
            // assert - read sectors in ascending order
            await using (var fileStream = File.OpenRead(path))
            {
                int bytesRead = 0;
                var sector = 0;

                do
                {
                    bytesRead = fileStream.Read(sectorBytes, 0, sectorBytes.Length);
                    
                    if (bytesRead != sectorBytes.Length)
                    {
                        sector++;
                        continue;
                    }
                
                    byte? prev = null;
                    var isAllEqual = true;
                    for (var i = 0; i < bytesRead; i++)
                    {
                        if (prev.HasValue && prev != sectorBytes[i])
                        {
                            isAllEqual = false;
                            break;
                        }

                        prev = sectorBytes[i];
                    }

                    if (isAllEqual && sectorBytes[0] > 0)
                    {
                        sectors.Add(new Tuple<int, int>(sector, sectorBytes[0]));
                    }

                    sector++;
                } while (bytesRead == sectorBytes.Length);
            }
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        // assert - sectors read are in ascending order
        int? prevSector = null;
        int? prevValue = null;
        var sectorsInAscendingOrder = 0;
        foreach (var sector in sectors)
        {
            if (prevSector.HasValue)
            {
                Assert.True(sector.Item1 > prevSector);
                Assert.True(sector.Item2 > prevValue);
                sectorsInAscendingOrder++;
            }
            prevSector = sector.Item1;
            prevValue = sector.Item2;
        }
        
        // assert - 254 sectors found in ascending order
        Assert.Equal(254, sectorsInAscendingOrder);
    }
}