using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.ExFat;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Amiga;
using Hst.Amiga.Extensions;
using Hst.Amiga.FileSystems;
using Hst.Amiga.FileSystems.FastFileSystem;
using Hst.Amiga.FileSystems.Pfs3;
using Hst.Amiga.FileSystems.SmartFileSystem;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Imager.Core.FileSystems.ExFat;
using Hst.Imager.Core.FileSystems.Fat32;
using Hst.Imager.Core.Optimization;
using Xunit;
using AmigaFileMode = Hst.Amiga.FileSystems.FileMode;
using File = System.IO.File;
using FileMode = System.IO.FileMode;

namespace Hst.Imager.Core.Tests.OptimizationTests;

/// <summary>
/// Tests examining file systems for free and used sectors. Each test writes a large file, which is deleted,
/// and a small file, which is kept. Data of the deleted file remains on disk, but is free in file system,
/// so compacting with file system allocation must not copy it and the kept file must still be readable.
/// </summary>
public class GivenDiskAllocationAnalyzer
{
    private const int DeletedFileSize = 1024 * 1024;
    private const int KeptFileSize = 100 * 1024;
    private static readonly byte[] Dos3DosType = [0x44, 0x4f, 0x53, 0x3];
    private static readonly byte[] Pfs3DosType = [0x50, 0x46, 0x53, 0x3];
    private static readonly byte[] Sfs0DosType = [0x53, 0x46, 0x53, 0x0];
    private static readonly byte[] DummySmartFileSystemBytes = Encoding.ASCII.GetBytes(
        "$VER: SmartFilesystem 0.1 (01/01/22)");
    private static readonly byte[] DummyFastFileSystemBytes = Encoding.ASCII.GetBytes(
        "$VER: FastFileSystem 0.1 (01/01/22) ");
    private static readonly string Pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio");

    [Fact]
    public async Task When_AnalyzingRdbWithPfs3_Then_FreeSectorsAreNotCopied()
    {
        // arrange - rdb disk with pfs3 formatted partition
        var stream = CreateStream(20 * 1024 * 1024);
        var partitionBlock = await CreateRdbPartition(stream, Pfs3DosType, await File.ReadAllBytesAsync(Pfs3AioPath));
        await Pfs3Formatter.FormatPartition(stream, partitionBlock, "Workbench");

        // arrange - write deleted and kept files
        await using (var volume = await Pfs3Volume.Mount(stream, partitionBlock))
        {
            await WriteFiles(volume);
        }

        // act and assert - analyze and compact
        var compactedStream = await AssertAnalyzeAndCompact(stream, "PFS3");

        // assert - kept file is readable from compacted disk
        await using var compactedVolume = await Pfs3Volume.Mount(compactedStream, partitionBlock);
        await AssertKeptFile(compactedVolume);
    }

    [Fact]
    public async Task When_AnalyzingRdbWithSmartFileSystem_Then_FreeSectorsAreNotCopied()
    {
        // arrange - rdb disk with sfs0 formatted partition
        var stream = CreateStream(20 * 1024 * 1024);
        var partitionBlock = await CreateRdbPartition(stream, Sfs0DosType, DummySmartFileSystemBytes);
        await SmartFileSystemFormatter.FormatPartition(stream, partitionBlock, "Workbench");

        // arrange - write deleted and kept files
        await using (var volume = await SmartFileSystemVolume.Mount(stream, partitionBlock))
        {
            await WriteFiles(volume);
        }

        // act and assert - analyze and compact
        var compactedStream = await AssertAnalyzeAndCompact(stream, "SFS");

        // assert - kept file is readable from compacted disk
        await using var compactedVolume = await SmartFileSystemVolume.Mount(compactedStream, partitionBlock);
        await AssertKeptFile(compactedVolume);
    }

    [Fact]
    public async Task When_AnalyzingRdbWithFastFileSystem_Then_FreeSectorsAreNotCopied()
    {
        // arrange - rdb disk with dos3 formatted partition
        var stream = CreateStream(20 * 1024 * 1024);
        var partitionBlock = await CreateRdbPartition(stream, Dos3DosType, DummyFastFileSystemBytes);
        await FastFileSystemFormatter.FormatPartition(stream, partitionBlock, "Workbench");

        // arrange - write deleted and kept files
        await using (var volume = await FastFileSystemVolume.MountPartition(stream, partitionBlock))
        {
            await WriteFiles(volume);
        }

        // act and assert - analyze and compact
        var compactedStream = await AssertAnalyzeAndCompact(stream, "DOS3");

        // assert - kept file is readable from compacted disk
        await using var compactedVolume = await FastFileSystemVolume.MountPartition(compactedStream, partitionBlock);
        await AssertKeptFile(compactedVolume);
    }

    [Fact]
    public async Task When_AnalyzingAdfWithoutPartitionTable_Then_FreeSectorsAreNotCopied()
    {
        // arrange - dos3 formatted adf
        var stream = CreateStream(FloppyDiskConstants.DoubleDensity.Size);
        await FastFileSystemFormatter.Format(stream, FloppyDiskConstants.DoubleDensity.LowCyl,
            FloppyDiskConstants.DoubleDensity.HighCyl, FloppyDiskConstants.DoubleDensity.ReservedBlocks,
            FloppyDiskConstants.DoubleDensity.Heads, FloppyDiskConstants.DoubleDensity.Sectors,
            FloppyDiskConstants.BlockSize, FloppyDiskConstants.BlockSize, Dos3DosType, "Floppy");

        // arrange - write deleted and kept files, deleted file is smaller to fit adf
        await using (var volume = await FastFileSystemVolume.MountAdf(stream))
        {
            await WriteFiles(volume, 500 * 1024);
        }

        // act and assert - analyze and compact
        var compactedStream = await AssertAnalyzeAndCompact(stream, "DOS3", 400 * 1024);

        // assert - kept file is readable from compacted disk
        await using var compactedVolume = await FastFileSystemVolume.MountAdf(compactedStream);
        await AssertKeptFile(compactedVolume);
    }

    [Fact]
    public async Task When_AnalyzingMbrWithFat32_Then_FreeSectorsAreNotCopied()
    {
        // arrange - mbr disk with fat32 formatted partition
        var stream = CreateStream(100 * 1024 * 1024);
        var disk = new DiscUtils.Raw.Disk(stream, Ownership.None);
        var biosPartitionTable = BiosPartitionTable.Initialize(disk);
        var partitionIndex = biosPartitionTable.CreatePrimaryBySector(2048, disk.Capacity / 512 - 1,
            BiosPartitionTypes.Fat32Lba, true);
        var partition = biosPartitionTable.Partitions[partitionIndex];
        await Fat32Formatter.FormatPartition(stream, partition.FirstSector * 512, partition.SectorCount * 512, 512,
            63, 255, "FAT32", 512);

        // arrange - write deleted and kept files
        using (var fatFileSystem = new FatFileSystem(partition.Open()))
        {
            WriteFiles(fatFileSystem);
        }

        // act and assert - analyze and compact
        var compactedStream = await AssertAnalyzeAndCompact(stream, "FAT32");

        // assert - kept file is readable from compacted disk
        var compactedDisk = new DiscUtils.Raw.Disk(compactedStream, Ownership.None);
        using var compactedFileSystem = new FatFileSystem(new BiosPartitionTable(compactedDisk)
            .Partitions[partitionIndex].Open());
        AssertKeptFile(compactedFileSystem);
    }

    [Fact]
    public async Task When_AnalyzingGptWithFat_Then_FreeSectorsAreNotCopied()
    {
        // arrange - gpt disk with fat formatted partition
        var stream = CreateStream(50 * 1024 * 1024);
        var disk = new DiscUtils.Raw.Disk(stream, Ownership.None);
        var guidPartitionTable = GuidPartitionTable.Initialize(disk);
        var partitionIndex = guidPartitionTable.Create(WellKnownPartitionType.WindowsFat, true);
        FatFileSystem.FormatPartition(disk, partitionIndex, "FATDISK").Dispose();

        // arrange - write deleted and kept files
        using (var fatFileSystem = new FatFileSystem(guidPartitionTable.Partitions[partitionIndex].Open()))
        {
            WriteFiles(fatFileSystem);
        }

        // act and assert - analyze and compact
        var compactedStream = await AssertAnalyzeAndCompact(stream, "FAT");

        // assert - kept file is readable from compacted disk
        var compactedDisk = new DiscUtils.Raw.Disk(compactedStream, Ownership.None);
        using var compactedFileSystem = new FatFileSystem(new GuidPartitionTable(compactedDisk)
            .Partitions[partitionIndex].Open());
        AssertKeptFile(compactedFileSystem);
    }

    [Fact]
    public async Task When_AnalyzingGptWithExFat_Then_FreeSectorsAreNotCopied()
    {
        // arrange - gpt disk with exfat formatted partition
        var stream = CreateStream(50 * 1024 * 1024);
        var disk = new DiscUtils.Raw.Disk(stream, Ownership.None);
        var guidPartitionTable = GuidPartitionTable.Initialize(disk);
        var partitionIndex = guidPartitionTable.Create(WellKnownPartitionType.WindowsFat, true);
        var partition = guidPartitionTable.Partitions[partitionIndex];
        await ExFatFormatter.FormatPartition(stream, partition.FirstSector * 512, partition.SectorCount * 512, 512,
            "EXFAT", 4096);

        // arrange - write deleted and kept files
        using (var exFatFileSystem = new ExFatFileSystem(partition.Open()))
        {
            WriteFiles(exFatFileSystem);
        }

        // act and assert - analyze and compact
        var compactedStream = await AssertAnalyzeAndCompact(stream, "exFAT");

        // assert - kept file is readable from compacted disk
        var compactedDisk = new DiscUtils.Raw.Disk(compactedStream, Ownership.None);
        using var compactedFileSystem = new ExFatFileSystem(new GuidPartitionTable(compactedDisk)
            .Partitions[partitionIndex].Open());
        AssertKeptFile(compactedFileSystem);
    }

    [Fact]
    public async Task When_AnalyzingDiskWithUnknownFileSystem_Then_AllSectorsAreUsed()
    {
        // arrange - disk with random data and no partition table
        var bytes = new byte[1024 * 1024];
        new Random(42).NextBytes(bytes);
        bytes[0] = 0;
        var stream = new MemoryStream(bytes);

        // act - analyze disk
        var diskAllocation = await new DiskAllocationAnalyzer().Analyze(stream, stream.Length);

        // assert - disk is unknown and has no free ranges
        var volume = Assert.Single(diskAllocation.Volumes);
        Assert.False(volume.IsKnownFileSystem);
        Assert.Empty(diskAllocation.AllocationMap.FreeRanges);
    }

    [Fact]
    public async Task When_AnalyzingRdbWithCorruptPfs3Bitmap_Then_PartitionIsTreatedAsUnknown()
    {
        // arrange - rdb disk with pfs3 formatted partition
        var stream = CreateStream(20 * 1024 * 1024);
        var partitionBlock = await CreateRdbPartition(stream, Pfs3DosType, await File.ReadAllBytesAsync(Pfs3AioPath));
        await Pfs3Formatter.FormatPartition(stream, partitionBlock, "Workbench");

        // arrange - corrupt first bitmap index block pointer in root block
        var partitionOffset = (long)partitionBlock.LowCyl * partitionBlock.Surfaces * partitionBlock.BlocksPerTrack *
                              partitionBlock.BlockSize;
        stream.Position = partitionOffset + 2 * 512 + 0x60;
        stream.Write(new byte[] { 0xff, 0xff, 0xff, 0xff });

        // act - analyze disk
        var diskAllocation = await new DiskAllocationAnalyzer().Analyze(stream, stream.Length);

        // assert - partition is unknown, so all sectors are used
        var volume = Assert.Single(diskAllocation.Volumes);
        Assert.False(volume.IsKnownFileSystem);
        Assert.NotNull(volume.Message);
        Assert.Empty(diskAllocation.AllocationMap.FreeRanges);
    }

    [Fact]
    public async Task When_AnalyzingRdbWithCorruptSmartFileSystemRootBlock_Then_PartitionIsTreatedAsUnknown()
    {
        // arrange - rdb disk with sfs0 formatted partition
        var stream = CreateStream(20 * 1024 * 1024);
        var partitionBlock = await CreateRdbPartition(stream, Sfs0DosType, DummySmartFileSystemBytes);
        await SmartFileSystemFormatter.FormatPartition(stream, partitionBlock, "Workbench");

        // arrange - corrupt bitmap base in root block
        var partitionOffset = (long)partitionBlock.LowCyl * partitionBlock.Surfaces * partitionBlock.BlocksPerTrack *
                              partitionBlock.BlockSize;
        stream.Position = partitionOffset + 96;
        stream.Write(new byte[] { 0xff, 0xff, 0xff, 0xff });

        // act - analyze disk
        var diskAllocation = await new DiskAllocationAnalyzer().Analyze(stream, stream.Length);

        // assert - partition is unknown, so all sectors are used
        var volume = Assert.Single(diskAllocation.Volumes);
        Assert.False(volume.IsKnownFileSystem);
        Assert.NotNull(volume.Message);
        Assert.Empty(diskAllocation.AllocationMap.FreeRanges);
    }

    private static MemoryStream CreateStream(long size)
    {
        var stream = new MemoryStream();
        stream.SetLength(size);
        return stream;
    }

    private static async Task<PartitionBlock> CreateRdbPartition(Stream stream, byte[] dosType,
        byte[] fileSystemBytes)
    {
        var rigidDiskBlock = RigidDiskBlock.Create(stream.Length);
        rigidDiskBlock.AddFileSystem(dosType, fileSystemBytes)
            .AddPartition("DH0", bootable: true);
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
        return rigidDiskBlock.PartitionBlocks.First();
    }

    /// <summary>
    /// Analyze disk, assert file system is found with free sectors and compact disk with and without
    /// file system allocation, asserting free sectors of deleted file are not copied.
    /// </summary>
    private static async Task<MemoryStream> AssertAnalyzeAndCompact(MemoryStream stream, string fileSystem,
        long minimumFreeBytesSkipped = DeletedFileSize * 9 / 10)
    {
        // act - analyze disk
        var diskAllocation = await new DiskAllocationAnalyzer().Analyze(stream, stream.Length);

        // assert - volume with file system is found with free sectors
        var volume = Assert.Single(diskAllocation.Volumes, x => x.IsKnownFileSystem);
        Assert.StartsWith(fileSystem, volume.FileSystemAllocation.FileSystem);
        Assert.True(volume.FileSystemAllocation.FreeBytes > 0);
        Assert.All(volume.FileSystemAllocation.FreeRanges,
            x => Assert.True(x.Offset >= volume.Offset && x.End <= volume.Offset + volume.Size));

        // act - compact with and without file system allocation
        var compactedStream = CreateStream(stream.Length);
        var imageCompactor = new ImageCompactor();
        var withFileSystems = await imageCompactor.Compact(stream, compactedStream, stream.Length,
            diskAllocation.AllocationMap, false, CancellationToken.None);
        var withoutFileSystems = await imageCompactor.Compact(stream, CreateStream(stream.Length), stream.Length,
            new AllocationMap(), false, CancellationToken.None);

        // assert - data of deleted file is not copied, when compacting with file system allocation
        Assert.True(withFileSystems.IsSuccess);
        Assert.True(withoutFileSystems.IsSuccess);
        Assert.True(withoutFileSystems.Value.BytesWritten - withFileSystems.Value.BytesWritten >= minimumFreeBytesSkipped,
            $"Expected at least {minimumFreeBytesSkipped} bytes skipped, but {withoutFileSystems.Value.BytesWritten - withFileSystems.Value.BytesWritten} bytes was skipped");

        return compactedStream;
    }

    private static byte[] CreateData(int size, int seed)
    {
        var data = new byte[size];
        new Random(seed).NextBytes(data);
        return data;
    }

    private static async Task WriteFiles(IFileSystemVolume volume, int deletedFileSize = DeletedFileSize)
    {
        await using (var stream = await volume.OpenFile("deleted.bin", AmigaFileMode.Write, true, false))
        {
            await stream.WriteAsync(CreateData(deletedFileSize, 1));
        }

        await using (var stream = await volume.OpenFile("kept.bin", AmigaFileMode.Write, true, false))
        {
            await stream.WriteAsync(CreateData(KeptFileSize, 2));
        }

        await volume.Delete("deleted.bin", false);
        await volume.Flush();
    }

    private static async Task AssertKeptFile(IFileSystemVolume volume)
    {
        await using var stream = await volume.OpenFile("kept.bin", AmigaFileMode.Read, false, false);
        var data = new byte[KeptFileSize];
        await stream.ReadExactlyAsync(data);
        Assert.Equal(CreateData(KeptFileSize, 2), data);
    }

    private static void WriteFiles(IFileSystem fileSystem)
    {
        using (var stream = fileSystem.OpenFile("deleted.bin", FileMode.Create))
        {
            stream.Write(CreateData(DeletedFileSize, 1));
        }

        using (var stream = fileSystem.OpenFile("kept.bin", FileMode.Create))
        {
            stream.Write(CreateData(KeptFileSize, 2));
        }

        fileSystem.DeleteFile("deleted.bin");
    }

    private static void AssertKeptFile(IFileSystem fileSystem)
    {
        using var stream = fileSystem.OpenFile("kept.bin", FileMode.Open);
        var data = new byte[KeptFileSize];
        stream.ReadExactly(data);
        Assert.Equal(CreateData(KeptFileSize, 2), data);
    }
}
