using System.Linq;
using System.Threading.Tasks;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Core.IO;
using Hst.Imager.Core.FileSystems.Fat32;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenFat32Formatter
{
    private const long DiskSize1Gb = 1000 * 1024 * 1024;
    private const long DiskSize16Gb = DiskSize1Gb * 16;
    private const long DiskSize64Gb = DiskSize1Gb * 64;

    [Theory]
    [InlineData(DiskSize1Gb)]
    [InlineData(DiskSize16Gb)]
    [InlineData(DiskSize64Gb)]
    public async Task WhenFormatPartitionThenFat32BlocksAreWrittenAndAreEqual(long diskSize)
    {
        // arrange - blank stream of disk size
        await using var stream = new BlockMemoryStream();
        stream.SetLength(diskSize);

        // arrange - initialize mbr
        var disk = new DiscUtils.Raw.Disk(stream, Ownership.None);
        BiosPartitionTable.Initialize(disk);

        // arrange - create fat32 lba partition
        var biosPartitionTable = new BiosPartitionTable(disk);
        var partitionIndex = biosPartitionTable.CreatePrimaryBySector(1,
            (disk.Capacity / disk.SectorSize) - 1,
            BiosPartitionTypes.Fat32Lba, true);
        var partition = biosPartitionTable.Partitions[partitionIndex];

        // act - fat32 format partition
        var partitionOffset = partition.FirstSector * disk.Geometry.Value.BytesPerSector;
        await Fat32Formatter.FormatPartition(stream, partitionOffset,
            partition.SectorCount * disk.Geometry.Value.BytesPerSector,
            disk.Geometry.Value.BytesPerSector, disk.Geometry.Value.SectorsPerTrack, disk.Geometry.Value.HeadsPerCylinder, 
            "UNITTEST", 4096);

        // assert - partition sector 0 (offset 512) contains fat32 boot sector block
        Assert.True(stream.Blocks.ContainsKey(partitionOffset));
        var fat32BootSectorBytes = stream.Blocks[partitionOffset];
        Assert.Equal(0xeb, fat32BootSectorBytes[0]);
        Assert.Equal(0x58, fat32BootSectorBytes[1]);
        Assert.Equal(0x90, fat32BootSectorBytes[2]);
        Assert.Equal(0x55, fat32BootSectorBytes[510]);
        Assert.Equal(0xaa, fat32BootSectorBytes[511]);

        // assert - partition sector 1 (offset 1024) contains fat fs info block
        var sectorOffset = partitionOffset + 512;
        Assert.True(stream.Blocks.ContainsKey(sectorOffset));
        var fatFsInfoBytes = stream.Blocks[sectorOffset];
        Assert.Equal(0x52, fatFsInfoBytes[0]);
        Assert.Equal(0x52, fatFsInfoBytes[1]);
        Assert.Equal(0x61, fatFsInfoBytes[2]);
        Assert.Equal(0x41, fatFsInfoBytes[3]);
        Assert.Equal(0x55, fatFsInfoBytes[510]);
        Assert.Equal(0xaa, fatFsInfoBytes[511]);

        // assert - partition sector 6 (offset 3072) contains backup fat32 boot sector block
        sectorOffset = partitionOffset + 6 * 512;
        Assert.True(stream.Blocks.ContainsKey(sectorOffset));
        fat32BootSectorBytes = stream.Blocks[sectorOffset];
        Assert.Equal(0xeb, fat32BootSectorBytes[0]);
        Assert.Equal(0x58, fat32BootSectorBytes[1]);
        Assert.Equal(0x90, fat32BootSectorBytes[2]);
        Assert.Equal(0x55, fat32BootSectorBytes[510]);
        Assert.Equal(0xaa, fat32BootSectorBytes[511]);

        // assert - partition sector 7 (offset 3584) contains backup fat fs info block
        sectorOffset = partitionOffset + 7 * 512;
        Assert.True(stream.Blocks.ContainsKey(sectorOffset));
        fatFsInfoBytes = stream.Blocks[sectorOffset];
        Assert.Equal(0x52, fatFsInfoBytes[0]);
        Assert.Equal(0x52, fatFsInfoBytes[1]);
        Assert.Equal(0x61, fatFsInfoBytes[2]);
        Assert.Equal(0x41, fatFsInfoBytes[3]);
        Assert.Equal(0x55, fatFsInfoBytes[510]);
        Assert.Equal(0xaa, fatFsInfoBytes[511]);

        // assert - partition sector 32 (offset 16384) contains first fat block
        var reservedSectors = 32;
        sectorOffset = partitionOffset + reservedSectors * 512;
        Assert.True(stream.Blocks.ContainsKey(sectorOffset));
        var firstFatBytes = stream.Blocks[sectorOffset];
        Assert.Equal(0xf8, firstFatBytes[0]);
        Assert.Equal(0xff, firstFatBytes[1]);
        Assert.Equal(0xff, firstFatBytes[2]);
        Assert.Equal(0x0f, firstFatBytes[3]);
        Assert.Equal(0xff, firstFatBytes[4]);
        Assert.Equal(0xff, firstFatBytes[5]);
        Assert.Equal(0xff, firstFatBytes[6]);
        Assert.Equal(0xff, firstFatBytes[7]);
        Assert.Equal(0xff, firstFatBytes[8]);
        Assert.Equal(0xff, firstFatBytes[9]);
        Assert.Equal(0xff, firstFatBytes[10]);
        Assert.Equal(0x0f, firstFatBytes[11]);

        // assert - partition sector depending on size contains second fat block
        var sectorsPerCluster = 4096 / 512;
        var fatSize = Fat32Formatter.CalculateFatSizeSectors((uint)partition.SectorCount, 32,
            (uint)sectorsPerCluster, 2, 512);
        sectorOffset = partitionOffset + ((reservedSectors + fatSize) * 512);
        Assert.True(stream.Blocks.ContainsKey(sectorOffset));
        firstFatBytes = stream.Blocks[sectorOffset];
        Assert.Equal(0xf8, firstFatBytes[0]);
        Assert.Equal(0xff, firstFatBytes[1]);
        Assert.Equal(0xff, firstFatBytes[2]);
        Assert.Equal(0x0f, firstFatBytes[3]);
        Assert.Equal(0xff, firstFatBytes[4]);
        Assert.Equal(0xff, firstFatBytes[5]);
        Assert.Equal(0xff, firstFatBytes[6]);
        Assert.Equal(0xff, firstFatBytes[7]);
        Assert.Equal(0xff, firstFatBytes[8]);
        Assert.Equal(0xff, firstFatBytes[9]);
        Assert.Equal(0xff, firstFatBytes[10]);
        Assert.Equal(0x0f, firstFatBytes[11]);

        // arrange - mount fat file system
        var partitionStream = partition.Open();
        var fatFileSystem = new FatFileSystem(partitionStream, Ownership.None);

        // assert - volume name, oem name and cluster size are equal
        Assert.Equal("UNITTEST", fatFileSystem.VolumeLabel.Substring(0, 8));
        Assert.Equal("MSWIN4.1", fatFileSystem.OemName);
        Assert.Equal(4096, fatFileSystem.ClusterSize);

        // assert - dirs and files are empty for formatted fat file system
        var dirs = fatFileSystem.GetDirectories(string.Empty).ToList();
        var files = fatFileSystem.GetFiles(string.Empty).ToList();
        Assert.Empty(dirs);
        Assert.Empty(files);
    }
}