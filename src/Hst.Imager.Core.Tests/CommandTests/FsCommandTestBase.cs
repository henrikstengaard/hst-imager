using DiscUtils;
using DiscUtils.Ntfs;

namespace Hst.Imager.Core.Tests.CommandTests;

using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amiga;
using Amiga.Extensions;
using Amiga.FileSystems.FastFileSystem;
using Amiga.FileSystems.Pfs3;
using Amiga.RigidDiskBlocks;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Core.Extensions;
using Models;
using Directory = System.IO.Directory;
using File = System.IO.File;

public class FsCommandTestBase : CommandTestBase
{
    protected static readonly byte[] Dos3DosType = { 0x44, 0x4f, 0x53, 0x3 };
    protected static readonly byte[] Dos7DosType = { 0x44, 0x4f, 0x53, 0x7 };
    protected static readonly byte[] DummyFastFileSystemBytes = Encoding.ASCII.GetBytes(
        "$VER: FastFileSystem 0.1 (01/01/22) ");
    protected static readonly byte[] Pfs3DosType = { 0x50, 0x46, 0x53, 0x3 };
    protected static readonly string Pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio");

    protected async Task CreateMbrDisk(TestCommandHelper testCommandHelper, string path,
        long diskSize = 10 * 1024 * 1024)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, diskSize, true);
        using var media = mediaResult.Value;
        var stream = media.Stream;
        if (!path.ToLower().EndsWith(".vhd"))
        {
            stream.SetLength(diskSize);
        }

        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
            
        BiosPartitionTable.Initialize(disk);
    }

    protected async Task CreateGptDisk(TestCommandHelper testCommandHelper, string path,
        long diskSize = 10 * 1024 * 1024)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, diskSize, true);
        using var media = mediaResult.Value;
        var stream = media.Stream;
        if (!path.ToLower().EndsWith(".vhd"))
        {
            stream.SetLength(diskSize);
        }
            
        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
            
        GuidPartitionTable.Initialize(disk.Content, Geometry.FromCapacity(disk.Capacity));
    }

    protected async Task CreateRdbWithPfs3(TestCommandHelper testCommandHelper, string path,
        long diskSize = 10 * 1024 * 1024, long rdbSize = 0, uint rdbBlockLo = 0)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, diskSize, true);
        using var media = mediaResult.Value;
        var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;
        
        if (!path.ToLower().EndsWith(".vhd"))
        {
            stream.SetLength(diskSize);
        }

        var rigidDiskBlock = RigidDiskBlock.Create((rdbSize == 0 ? diskSize : rdbSize).ToSectorSize());
        if (rdbBlockLo > 0)
        {
            rigidDiskBlock.RdbBlockLo = rdbBlockLo;
        }

        rigidDiskBlock.AddFileSystem(Pfs3DosType, await File.ReadAllBytesAsync(Pfs3AioPath));
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
    }

    protected async Task CreateMbrFatFormattedDisk(TestCommandHelper testCommandHelper, string path,
        long diskSize = 10 * 1024 * 1024)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, diskSize, true);
        using var media = mediaResult.Value;
        var stream = media.Stream;

        var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(stream, Ownership.None);
        var biosPartitionTable = BiosPartitionTable.Initialize(disk);
        var partitionIndex = biosPartitionTable.CreatePrimaryBySector(1, biosPartitionTable.DiskGeometry.TotalSectorsLong,
            BiosPartitionTypes.Fat32Lba, true);
        FatFileSystem.FormatPartition(disk, partitionIndex, "FATDISK");
    }

    protected async Task CreateMbrNtfsFormattedDisk(TestCommandHelper testCommandHelper, string path,
        long diskSize = 10 * 1024 * 1024)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, diskSize, true);
        using var media = mediaResult.Value;
        var stream = media.Stream;

        var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(stream, Ownership.None);
        var biosPartitionTable = BiosPartitionTable.Initialize(disk);
        var partitionIndex = biosPartitionTable.CreatePrimaryBySector(1, biosPartitionTable.DiskGeometry.TotalSectorsLong,
            BiosPartitionTypes.Ntfs, true);
        var partition = biosPartitionTable.Partitions[partitionIndex];
        NtfsFileSystem.Format(partition.Open(), "NTFSDISK", Geometry.FromCapacity(partition.SectorCount * 512), 
            partition.FirstSector, partition.SectorCount);
    }

    protected async Task CreateGptFatFormattedDisk(TestCommandHelper testCommandHelper, string path,
        long diskSize = 10 * 1024 * 1024)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, diskSize, true);
        using var media = mediaResult.Value;
        var stream = media.Stream;

        var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(stream, Ownership.None);
        var guidPartitionTable = GuidPartitionTable.Initialize(disk);
        var partitionIndex = guidPartitionTable.Create(WellKnownPartitionType.WindowsFat, true);
        FatFileSystem.FormatPartition(disk, partitionIndex, "FATDISK");
    }

    protected async Task CreateGptNtfsFormattedDisk(TestCommandHelper testCommandHelper, string path,
        long diskSize = 10 * 1024 * 1024)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, diskSize, true);
        using var media = mediaResult.Value;
        var stream = media.Stream;

        var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(stream, Ownership.None);
        var guidPartitionTable = GuidPartitionTable.Initialize(disk);
        var partitionIndex = guidPartitionTable.Create(WellKnownPartitionType.WindowsFat, true);
        var partition = guidPartitionTable.Partitions[partitionIndex];
        NtfsFileSystem.Format(partition.Open(), "NTFSDISK", Geometry.FromCapacity(partition.SectorCount * 512), 
            partition.FirstSector, partition.SectorCount);
    }
    
    protected async Task CreatePfs3FormattedDisk(TestCommandHelper testCommandHelper, string path,
        long diskSize = 10 * 1024 * 1024)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, diskSize, true);
        using var media = mediaResult.Value;
        var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

        var rigidDiskBlock = RigidDiskBlock.Create(diskSize.ToUniversalSize());

        rigidDiskBlock.AddFileSystem(Pfs3DosType, await File.ReadAllBytesAsync(Pfs3AioPath))
            .AddPartition("DH0", bootable: true);
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

        await Pfs3Formatter.FormatPartition(stream, partitionBlock, "Workbench");
    }

    protected async Task CreateAdfDisk(TestCommandHelper testCommandHelper, string path)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, 0, true);
        using var media = mediaResult.Value;
        var stream = media.Stream;

        stream.SetLength(FloppyDiskConstants.DoubleDensity.Size);    
        
        await FastFileSystemFormatter.Format(stream, FloppyDiskConstants.DoubleDensity.LowCyl,
            FloppyDiskConstants.DoubleDensity.HighCyl, FloppyDiskConstants.DoubleDensity.ReservedBlocks,
            FloppyDiskConstants.DoubleDensity.Heads, FloppyDiskConstants.DoubleDensity.Sectors,
            FloppyDiskConstants.BlockSize, FloppyDiskConstants.BlockSize, Dos3DosType, "Workbench");        
    }
    
    protected async Task CreateDos7FormattedDisk(TestCommandHelper testCommandHelper, string path,
        long diskSize = 10 * 1024 * 1024)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, diskSize, true);
        using var media = mediaResult.Value;
        var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

        if (!path.ToLower().EndsWith(".vhd"))
        {
            stream.SetLength(diskSize);
        }
        
        var rigidDiskBlock = RigidDiskBlock.Create(diskSize.ToUniversalSize());

        rigidDiskBlock.AddFileSystem(Dos7DosType, DummyFastFileSystemBytes)
            .AddPartition("DH0", bootable: true);
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

        await FastFileSystemFormatter.FormatPartition(stream, partitionBlock, "Workbench");
    }
    
    protected async Task CreateDos3FormattedAdf(string path)
    {
        await using var stream = File.Open(path, FileMode.Create, FileAccess.ReadWrite);

        stream.SetLength(FloppyDiskConstants.DoubleDensity.Size);

        await FastFileSystemFormatter.Format(stream, FloppyDiskConstants.DoubleDensity.LowCyl,
            FloppyDiskConstants.DoubleDensity.HighCyl, FloppyDiskConstants.DoubleDensity.ReservedBlocks,
            FloppyDiskConstants.DoubleDensity.Heads, FloppyDiskConstants.DoubleDensity.Sectors,
            FloppyDiskConstants.BlockSize,
            FloppyDiskConstants.BlockSize, Dos3DosType, "Floppy");
    }

    protected async Task CreateDos3AdfFiles(string path)
    {
        await using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);

        await using var volume = await FastFileSystemVolume.MountAdf(stream);

        await volume.CreateFile("file1.txt");
        await volume.CreateFile("file2.txt");

        await volume.CreateDirectory("dir1");
        await volume.ChangeDirectory("dir1");

        await volume.CreateFile("file3.txt");
        await volume.CreateFile("test.txt");
    }

    protected async Task<Pfs3Volume> MountPfs3Volume(Stream stream)
    {
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

        return await Pfs3Volume.Mount(stream, partitionBlock);
    }

    protected async Task<FastFileSystemVolume> MountFastFileSystemVolume(Stream stream)
    {
        if (stream.Length == FloppyDiskConstants.DoubleDensity.Size)
        {
            return await FastFileSystemVolume.MountAdf(stream);
        }
        
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

        return await FastFileSystemVolume.MountPartition(stream, partitionBlock);
    }
    
    protected void DeletePaths(params string[] paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                continue;
            }

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}