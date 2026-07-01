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
        // arrange - media path and size
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
        
        // arrange - read fat file system from floppy sector 0        
        var fatFileSystem = await FatReader.ReadFatFileSystem(media.Stream);
        
        // act - read fat regions from floppy sector 0
        var fatRegions = (await FatRegionReader.ReadFatRegions(media.Stream))
            .OrderBy(fatRegion => fatRegion.Offset).ToList();

        // assert - fat regions are read
        Assert.NotEmpty(fatRegions);
        
        // assert - fat regions contain reserved region
        Assert.Single(fatRegions, x => x.Type == FatRegionType.Reserved);
        var reservedRegion = fatRegions.FirstOrDefault(x => x.Type == FatRegionType.Reserved);
        Assert.NotNull(reservedRegion);
        Assert.Equal(0, reservedRegion.Offset);
        Assert.Equal(fatFileSystem.BiosParameterBlock.ReservedSectorCount *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, reservedRegion.Size);
        
        // assert - fat regions contains 2 fat regions 
        var fatFatRegions = fatRegions.Where(x => x.Type == FatRegionType.Fat).ToList();
        Assert.Equal(2, fatFatRegions.Count);
        var fatFatRegion1 = fatFatRegions[0];
        var fatFatRegion2 = fatFatRegions[1];
        Assert.Equal(fatFileSystem.BiosParameterBlock.ReservedSectorCount *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion1.Offset);
        Assert.Equal(fatFileSystem.BiosParameterBlock.FatSectors *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion1.Size);
        Assert.Equal((fatFileSystem.BiosParameterBlock.ReservedSectorCount + fatFileSystem.BiosParameterBlock.FatSectors) *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion2.Offset);
        Assert.Equal(fatFileSystem.BiosParameterBlock.FatSectors *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion2.Size);
        
        // assert - fat regions contain root region
        Assert.Equal(1, fatRegions.Count(x => x.Type == FatRegionType.Root));
        var rootFatRegion = fatRegions.FirstOrDefault(x => x.Type == FatRegionType.Root);
        Assert.NotNull(rootFatRegion);
        Assert.Equal(fatFileSystem.BiosParameterBlock.RootEntriesCount * FileSystems.Fat.Constants.FatEntrySize,
            rootFatRegion.Size);

        // assert - fat regions contain 2 file regions
        var fileRegions = fatRegions.Where(x => x.Type == FatRegionType.File).ToList(); 
        Assert.Equal(2, fileRegions.Count);
        Assert.True(fileRegions.All(x => x.Size >= fileData.Length));
        Assert.True(fileRegions.All(x => x.Offset >= fatFileSystem.FirstDataSector));
        var fileRegion1 = fileRegions[0];
        var fileRegion2 = fileRegions[1];
        Assert.True(fileRegion1.Offset < fileRegion2.Offset);
    }
    
    [Fact]
    public async Task When_ReadingFatAreasFromFat16_Then_FatAreasAreRead()
    {
        // arrange - media path and size
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

        // arrange - read fat file system        
        var fatFileSystem = await FatReader.ReadFatFileSystem(media.Stream, partitionOffset);
        
        // act - read fat regions
        var fatRegions = (await FatRegionReader.ReadFatRegions(media.Stream, partitionOffset))
            .OrderBy(fatRegion => fatRegion.Offset).ToList();

        // assert - fat regions are read
        Assert.NotEmpty(fatRegions);
        
        // assert - fat regions contain reserved region
        Assert.Single(fatRegions, x => x.Type == FatRegionType.Reserved);
        var reservedRegion = fatRegions.FirstOrDefault(x => x.Type == FatRegionType.Reserved);
        Assert.NotNull(reservedRegion);
        Assert.Equal(0, reservedRegion.Offset);
        Assert.Equal(fatFileSystem.BiosParameterBlock.ReservedSectorCount *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, reservedRegion.Size);
        
        // assert - fat regions contains 2 fat regions 
        var fatFatRegions = fatRegions.Where(x => x.Type == FatRegionType.Fat).ToList();
        Assert.Equal(2, fatFatRegions.Count);
        var fatFatRegion1 = fatFatRegions[0];
        var fatFatRegion2 = fatFatRegions[1];
        Assert.Equal(fatFileSystem.BiosParameterBlock.ReservedSectorCount *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion1.Offset);
        Assert.Equal(fatFileSystem.BiosParameterBlock.FatSectors *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion1.Size);
        Assert.Equal((fatFileSystem.BiosParameterBlock.ReservedSectorCount + fatFileSystem.BiosParameterBlock.FatSectors) *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion2.Offset);
        Assert.Equal(fatFileSystem.BiosParameterBlock.FatSectors *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion2.Size);
        
        // assert - fat regions contain root region
        Assert.Equal(1, fatRegions.Count(x => x.Type == FatRegionType.Root));
        var rootFatRegion = fatRegions.FirstOrDefault(x => x.Type == FatRegionType.Root);
        Assert.NotNull(rootFatRegion);
        Assert.Equal(fatFileSystem.BiosParameterBlock.RootEntriesCount * FileSystems.Fat.Constants.FatEntrySize,
            rootFatRegion.Size);

        // assert - fat regions contain 2 file regions
        var fileRegions = fatRegions.Where(x => x.Type == FatRegionType.File).ToList(); 
        Assert.Equal(2, fileRegions.Count);
        Assert.True(fileRegions.All(x => x.Size >= fileData.Length));
        Assert.True(fileRegions.All(x => x.Offset >= fatFileSystem.FirstDataSector));
        var fileRegion1 = fileRegions[0];
        var fileRegion2 = fileRegions[1];
        Assert.True(fileRegion1.Offset < fileRegion2.Offset);
    }

    [Fact]
    public async Task When_ReadingFatAreasFromFat32_Then_FatAreasAreRead()
    {
        // arrange - media path and size
        var mediaPath = $"{Guid.NewGuid()}.img";
        var diskSize = 1.GB();
        
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

        // arrange - read fat file system        
        var fatFileSystem = await FatReader.ReadFatFileSystem(media.Stream, partitionOffset);
        
        // act - read fat regions
        var fatRegions = (await FatRegionReader.ReadFatRegions(media.Stream, partitionOffset))
            .OrderBy(x => x.Offset).ToList();

        // assert - fat regions are read
        Assert.NotEmpty(fatRegions);
        
        // assert - fat regions contain reserved region
        Assert.Single(fatRegions, x => x.Type == FatRegionType.Reserved);
        var reservedRegion = fatRegions.FirstOrDefault(x => x.Type == FatRegionType.Reserved);
        Assert.NotNull(reservedRegion);
        Assert.Equal(0, reservedRegion.Offset);
        Assert.Equal(fatFileSystem.BiosParameterBlock.ReservedSectorCount *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, reservedRegion.Size);

        // assert - fat regions contains 2 fat regions
        var fatFatRegions = fatRegions.Where(x => x.Type == FatRegionType.Fat).ToList();
        Assert.Equal(2, fatFatRegions.Count);
        var fatFatRegion1 = fatFatRegions[0];
        var fatFatRegion2 = fatFatRegions[1];
        Assert.Equal(fatFileSystem.BiosParameterBlock.ReservedSectorCount *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion1.Offset);
        Assert.Equal(fatFileSystem.ExtendedBiosParameterBlock.FatSectorsFat32 *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion1.Size);
        Assert.Equal((fatFileSystem.BiosParameterBlock.ReservedSectorCount + fatFileSystem.ExtendedBiosParameterBlock.FatSectorsFat32) *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion2.Offset);
        Assert.Equal(fatFileSystem.ExtendedBiosParameterBlock.FatSectorsFat32 *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion2.Size);
        
        // assert - fat regions contain root region
        Assert.Equal(1, fatRegions.Count(x => x.Type == FatRegionType.Root));
        var rootRegion = fatRegions.FirstOrDefault(x => x.Type == FatRegionType.Root);
        Assert.NotNull(rootRegion);
        var rootRegionSector =
            (fatFileSystem.ExtendedBiosParameterBlock.RootCluster - FileSystems.Fat.Constants.ReservedClusters) *
            fatFileSystem.BiosParameterBlock.SectorsPerCluster;
        var rootRegionOffset = (fatFileSystem.FirstDataSector + rootRegionSector)
                         * fatFileSystem.BiosParameterBlock.BytesPerSector;
        Assert.Equal(rootRegionOffset, rootRegion.Offset);
        Assert.Equal(fatFileSystem.BiosParameterBlock.BytesPerSector * fatFileSystem.BiosParameterBlock.SectorsPerCluster,
            rootRegion.Size);

        // assert - fat regions contain 2 file regions
        var fileRegions = fatRegions.Where(x => x.Type == FatRegionType.File).ToList(); 
        Assert.Equal(2, fileRegions.Count);
        var fileRegion1 = fileRegions[0];
        var fileRegion2 = fileRegions[1];
        Assert.Equal(fileData.Length, fileRegion1.Size);
        Assert.True(fileRegion1.Offset > fatFileSystem.FirstDataSector * fatFileSystem.BiosParameterBlock.BytesPerSector);
        Assert.Equal(fileData.Length, fileRegion2.Size);
        Assert.True(fileRegion2.Offset > fatFileSystem.FirstDataSector * fatFileSystem.BiosParameterBlock.BytesPerSector);
    }

    [Fact]
    public async Task When_ReadingFatAreasFromFat16WithDeletedFile_Then_FatAreasAreReadWithoutDeletedFile()
    {
        // arrange - media path and size
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

        // arrange - read fat file system        
        var fatFileSystem = await FatReader.ReadFatFileSystem(media.Stream, partitionOffset);

        // act - read fat regions
        var fatRegions = (await FatRegionReader.ReadFatRegions(media.Stream, partitionOffset))
            .OrderBy(fatRegion => fatRegion.Offset).ToList();

        // assert - fat regions are read
        Assert.NotEmpty(fatRegions);
        
        // assert - fat regions contain reserved region
        Assert.Single(fatRegions, x => x.Type == FatRegionType.Reserved);
        var reservedRegion = fatRegions.FirstOrDefault(x => x.Type == FatRegionType.Reserved);
        Assert.NotNull(reservedRegion);
        Assert.Equal(0, reservedRegion.Offset);
        Assert.Equal(fatFileSystem.BiosParameterBlock.ReservedSectorCount *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, reservedRegion.Size);
        
        // assert - fat regions contains 2 fat regions 
        var fatFatRegions = fatRegions.Where(x => x.Type == FatRegionType.Fat).ToList();
        Assert.Equal(2, fatFatRegions.Count);
        var fatFatRegion1 = fatFatRegions[0];
        var fatFatRegion2 = fatFatRegions[1];
        Assert.Equal(fatFileSystem.BiosParameterBlock.ReservedSectorCount *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion1.Offset);
        Assert.Equal(fatFileSystem.BiosParameterBlock.FatSectors *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion1.Size);
        Assert.Equal((fatFileSystem.BiosParameterBlock.ReservedSectorCount + fatFileSystem.BiosParameterBlock.FatSectors) *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion2.Offset);
        Assert.Equal(fatFileSystem.BiosParameterBlock.FatSectors *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion2.Size);
        
        // assert - fat regions contain root region
        Assert.Equal(1, fatRegions.Count(x => x.Type == FatRegionType.Root));
        var rootFatRegion = fatRegions.FirstOrDefault(x => x.Type == FatRegionType.Root);
        Assert.NotNull(rootFatRegion);
        Assert.Equal(fatFileSystem.BiosParameterBlock.RootEntriesCount * FileSystems.Fat.Constants.FatEntrySize,
            rootFatRegion.Size);

        // assert - fat regions contain 1 file region
        Assert.Equal(1, fatRegions.Count(x => x.Type == FatRegionType.File));
        var fileRegion = fatRegions.FirstOrDefault(x => x.Type == FatRegionType.File);
        Assert.NotNull(fileRegion);
        Assert.Equal(fileData.Length, fileRegion.Size);
        Assert.True(fileRegion.Offset >= fatFileSystem.FirstDataSector);
    }

    [Fact]
    public async Task When_ReadingFatAreasFromFat32WithLongFileName_Then_FatAreasAreRead()
    {
        // arrange - media path and size
        var mediaPath = $"{Guid.NewGuid()}.img";
        var diskSize = 1.GB();

        // arrange - file data
        var fileData = new byte[1024];
        Array.Fill<byte>(fileData, 1);

        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - create mbr fat formatted disk
        await testCommandHelper.AddTestMedia(mediaPath);
        await MbrTestHelper.CreateMbrFatFormattedDisk(testCommandHelper, mediaPath, diskSize);

        // arrange - create files
        await MbrTestHelper.CreateFile(testCommandHelper, mediaPath, ["file with long filename.txt"], fileData);
        
        // arrange - get media stream and partition offset
        var mediaResult = await testCommandHelper.GetReadableFileMedia(mediaPath);
        using var media = mediaResult.Value;

        // arrange - calculate partition offset
        var biosPartitionTable = new BiosPartitionTable(media.Stream);
        var partitionOffset = biosPartitionTable.Partitions[0].FirstSector *
                              biosPartitionTable.DiskGeometry.Value.BytesPerSector;

        // arrange - read fat file system        
        var fatFileSystem = await FatReader.ReadFatFileSystem(media.Stream, partitionOffset);
        
        // act - read fat regions
        var fatRegions = (await FatRegionReader.ReadFatRegions(media.Stream, partitionOffset))
            .OrderBy(fatRegion => fatRegion.Offset).ToList();

        // assert - fat regions are read
        Assert.NotEmpty(fatRegions);
        
        // assert - fat regions contain reserved region
        Assert.Single(fatRegions, x => x.Type == FatRegionType.Reserved);
        var reservedRegion = fatRegions.FirstOrDefault(x => x.Type == FatRegionType.Reserved);
        Assert.NotNull(reservedRegion);
        Assert.Equal(0, reservedRegion.Offset);
        Assert.Equal(fatFileSystem.BiosParameterBlock.ReservedSectorCount *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, reservedRegion.Size);

        // assert - fat regions contains 2 fat regions
        var fatFatRegions = fatRegions.Where(x => x.Type == FatRegionType.Fat).ToList();
        Assert.Equal(2, fatFatRegions.Count);
        var fatFatRegion1 = fatFatRegions[0];
        var fatFatRegion2 = fatFatRegions[1];
        Assert.Equal(fatFileSystem.BiosParameterBlock.ReservedSectorCount *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion1.Offset);
        Assert.Equal(fatFileSystem.ExtendedBiosParameterBlock.FatSectorsFat32 *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion1.Size);
        Assert.Equal((fatFileSystem.BiosParameterBlock.ReservedSectorCount + fatFileSystem.ExtendedBiosParameterBlock.FatSectorsFat32) *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion2.Offset);
        Assert.Equal(fatFileSystem.ExtendedBiosParameterBlock.FatSectorsFat32 *
                     fatFileSystem.BiosParameterBlock.BytesPerSector, fatFatRegion2.Size);
        
        // assert - fat regions contain root region
        Assert.Equal(1, fatRegions.Count(x => x.Type == FatRegionType.Root));
        var rootFatRegion = fatRegions.FirstOrDefault(x => x.Type == FatRegionType.Root);
        Assert.NotNull(rootFatRegion);
        Assert.True(rootFatRegion.Size > 0);

        // assert - fat regions contain 1 file region
        Assert.Equal(1, fatRegions.Count(x => x.Type == FatRegionType.File));
        var fileRegion = fatRegions.FirstOrDefault(x => x.Type == FatRegionType.File);
        Assert.NotNull(fileRegion);
        Assert.Equal(fileData.Length, fileRegion.Size);
        Assert.True(fileRegion.Offset > fatFileSystem.FirstDataSector * fatFileSystem.BiosParameterBlock.BytesPerSector);
    }
}