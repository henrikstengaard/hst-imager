using System;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils.Partitions;
using Hst.Core.Extensions;
using Hst.Imager.Core.FileSystems.Fat;
using Xunit;

namespace Hst.Imager.Core.Tests.FileSystemTests.FatTests;

public class GivenFatRegionReaderCopyImgToImg
{
    [Fact]
    public async Task When_CopyingFatRegionsFromSrcImgToDestImg_Then_DestImgContainsFatFileSystem()
    {
        // arrange - medias
        var srcMediaPath = $"src_{Guid.NewGuid()}.img";
        var destMediaPath = $"dest_{Guid.NewGuid()}.img";
        var diskSize = 100.MB();

        // arrange - file 1 data
        var file1Data = new byte[3024];
        Array.Fill<byte>(file1Data, 1);

        // arrange - file 2 data
        var file2Data = new byte[1024];
        Array.Fill<byte>(file2Data, 2);

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - create src mbr fat formatted media
        await testCommandHelper.AddTestMedia(srcMediaPath);
        await MbrTestHelper.CreateMbrFatFormattedDisk(testCommandHelper, srcMediaPath, diskSize);

        // arrange - create directory and files
        await MbrTestHelper.CreateFile(testCommandHelper, srcMediaPath, ["file1.txt"], file1Data);
        await MbrTestHelper.CreateDirectory(testCommandHelper, srcMediaPath, 0, ["dir1"]);
        await MbrTestHelper.CreateFile(testCommandHelper, srcMediaPath, ["dir1", "file2.txt"], file2Data);

        // arrange - create dest media
        await testCommandHelper.AddTestMedia(destMediaPath);
        
        // arrange - get src media stream
        var srcMediaResult = await testCommandHelper.GetReadableFileMedia(srcMediaPath);
        using var srcMedia = srcMediaResult.Value;

        // arrange - calculate partition offset
        var biosPartitionTable = new BiosPartitionTable(srcMedia.Stream);
        var partitionOffset = biosPartitionTable.Partitions[0].FirstSector *
                              biosPartitionTable.DiskGeometry.Value.BytesPerSector;

        // arrange - read fat regions from src media
        var fatRegions = (await FatRegionReader.ReadFatRegions(srcMedia.Stream, partitionOffset)).ToList();

        // arrange - get dest media stream
        var destMediaResult = await testCommandHelper.GetReadableFileMedia(destMediaPath);
        using var destMedia = destMediaResult.Value;

        // media size
        destMedia.Stream.SetLength(srcMedia.Size);

        // act - copy fat regions from src to dest media
        var sectorData = new byte[512];
        foreach (var fatRegion in fatRegions)
        {
            for (var sectorOffset = 0; sectorOffset < fatRegion.Size; sectorOffset += sectorData.Length)
            {
                // read sector data from src media stream
                srcMedia.Stream.Position = fatRegion.Offset + sectorOffset;
                await srcMedia.Stream.ReadExactlyAsync(sectorData, 0, sectorData.Length);

                // write sector data to dest media stream
                destMedia.Stream.Position = fatRegion.Offset + sectorOffset;
                await destMedia.Stream.WriteAsync(sectorData, 0, sectorData.Length);
            }
        }

        // assert - dest media contains root entries
        var entries = (await MbrTestHelper.GetEntriesFromFileSystemVolume(testCommandHelper, destMediaPath,
                0, [])).ToList();
        Assert.NotEmpty(entries);
        Assert.Equal(["dir1", "file1.txt"], entries.Select(entry => entry.Name).OrderBy(entry => entry));
    }
}