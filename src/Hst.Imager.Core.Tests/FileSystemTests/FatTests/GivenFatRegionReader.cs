using System;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils.Partitions;
using Hst.Core.Extensions;
using Hst.Imager.Core.FileSystems.Fat;
using Hst.Imager.Core.FileSystems.Fat.Regions;
using Xunit;

namespace Hst.Imager.Core.Tests.FileSystemTests.FatTests;

public class GivenFatRegionReader
{
    [Fact]
    public async Task When_ReadingFatAreasFromFat12_Then_FatAreasAreRead()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.img";
        
        // arrange - file data
        var fileData = new byte[1024];
        Array.Fill<byte>(fileData, 1);

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - create mbr fat formatted floppy media
        await testCommandHelper.AddTestMedia(mediaPath);
        await MbrTestHelper.CreateFatFormattedFloppy(testCommandHelper, mediaPath);
        
        // arrange - create files
        await MbrTestHelper.CreateFile(testCommandHelper, mediaPath, ["file1.txt"], fileData);
        await MbrTestHelper.CreateFile(testCommandHelper, mediaPath, ["file2.txt"], fileData);

        // arrange - get media stream and partition offset
        var mediaResult = await testCommandHelper.GetReadableFileMedia(mediaPath);
        using var media = mediaResult.Value;
        
        // act - read fat regions from floppy sector 0
        var fatRegions = (await FatRegionReader.ReadFatRegions(media.Stream)).ToList();

        // assert - fat regions are read
        Assert.NotEmpty(fatRegions);
        
        // assert - fat regions contain reserved region
        Assert.Equal(1, fatRegions.Count(x => x.Type == FatRegionType.Reserved));
        
        // assert - fat regions contains 2 fat regions 
        Assert.Equal(2, fatRegions.Count(x => x.Type == FatRegionType.Fat));
        
        // assert - fat regions contain root region
        Assert.Equal(1, fatRegions.Count(x => x.Type == FatRegionType.Root));

        // assert - fat regions contain 4 file regions
        Assert.Equal(4, fatRegions.Count(x => x.Type == FatRegionType.File));
    }
    
    [Fact]
    public async Task When_ReadingFatAreasFromFat16_Then_FatAreasAreRead()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.img";
        var diskSize = 100.MB();
        
        // arrange - file data
        var fileData = new byte[1024];
        Array.Fill<byte>(fileData, 1);

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - create mbr fat formatted disk
        await testCommandHelper.AddTestMedia(mediaPath);
        await MbrTestHelper.CreateMbrFatFormattedDisk(testCommandHelper, mediaPath, diskSize);
        
        // arrange - create files
        await MbrTestHelper.CreateFile(testCommandHelper, mediaPath, ["file1.txt"], fileData);
        await MbrTestHelper.CreateFile(testCommandHelper, mediaPath, ["file2.txt"], fileData);

        // arrange - get media stream and partition offset
        var mediaResult = await testCommandHelper.GetReadableFileMedia(mediaPath);
        using var media = mediaResult.Value;

        // arrange - calculate partition offset
        var biosPartitionTable = new BiosPartitionTable(media.Stream);
        var partitionOffset = biosPartitionTable.Partitions[0].FirstSector *
                              biosPartitionTable.DiskGeometry.Value.BytesPerSector;

        // act - read fat regions
        var fatRegions = (await FatRegionReader.ReadFatRegions(media.Stream, partitionOffset)).ToList();

        // assert - fat regions are read
        Assert.NotEmpty(fatRegions);
        
        // assert - fat regions contain reserved region
        Assert.Equal(1, fatRegions.Count(x => x.Type == FatRegionType.Reserved));
        
        // assert - fat regions contains 2 fat regions 
        Assert.Equal(2, fatRegions.Count(x => x.Type == FatRegionType.Fat));
        
        // assert - fat regions contain root region
        Assert.Equal(1, fatRegions.Count(x => x.Type == FatRegionType.Root));

        // assert - fat regions contain 2 file regions
        Assert.Equal(2, fatRegions.Count(x => x.Type == FatRegionType.File));
    }

    [Fact]
    public async Task When_ReadingFatAreasFromFat16WithDeletedFile_Then_FatAreasAreReadWithoutDeletedFile()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.img";
        var diskSize = 100.MB();
        
        // arrange - file data
        var fileData = new byte[1024];
        Array.Fill<byte>(fileData, 1);

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - create mbr fat formatted disk
        await testCommandHelper.AddTestMedia(mediaPath);
        await MbrTestHelper.CreateMbrFatFormattedDisk(testCommandHelper, mediaPath, diskSize);
        
        // arrange - create files
        await MbrTestHelper.CreateFile(testCommandHelper, mediaPath, ["file1.txt"], fileData);
        await MbrTestHelper.CreateFile(testCommandHelper, mediaPath, ["file2.txt"], fileData);
        
        // arrange - delete file
        await MbrTestHelper.DeleteFile(testCommandHelper, mediaPath, ["file2.txt"]);

        // arrange - get media stream and partition offset
        var mediaResult = await testCommandHelper.GetReadableFileMedia(mediaPath);
        using var media = mediaResult.Value;

        // arrange - calculate partition offset
        var biosPartitionTable = new BiosPartitionTable(media.Stream);
        var partitionOffset = biosPartitionTable.Partitions[0].FirstSector *
                              biosPartitionTable.DiskGeometry.Value.BytesPerSector;

        // act - read fat regions
        var fatRegions = (await FatRegionReader.ReadFatRegions(media.Stream, partitionOffset)).ToList();

        // assert - fat regions are read
        Assert.NotEmpty(fatRegions);
        
        // assert - fat regions contain reserved region
        Assert.Equal(1, fatRegions.Count(x => x.Type == FatRegionType.Reserved));
        
        // assert - fat regions contains 2 fat regions 
        Assert.Equal(2, fatRegions.Count(x => x.Type == FatRegionType.Fat));
        
        // assert - fat regions contain root region
        Assert.Equal(1, fatRegions.Count(x => x.Type == FatRegionType.Root));

        // assert - fat regions contain 1 file regions
        Assert.Equal(1, fatRegions.Count(x => x.Type == FatRegionType.File));
    }
}