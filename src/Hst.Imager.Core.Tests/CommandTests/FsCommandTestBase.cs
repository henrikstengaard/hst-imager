namespace Hst.Imager.Core.Tests.CommandTests;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amiga;
using Amiga.Extensions;
using Amiga.FileSystems.FastFileSystem;
using Amiga.FileSystems.Pfs3;
using Amiga.RigidDiskBlocks;
using Hst.Core.Extensions;
using Directory = System.IO.Directory;
using File = System.IO.File;

public class FsCommandTestBase : CommandTestBase
{
    protected static readonly byte[] Dos3DosType = { 0x44, 0x4f, 0x53, 0x3 };
    protected static readonly byte[] Pfs3DosType = { 0x50, 0x44, 0x53, 0x3 };
    protected static readonly string Pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio");
    
    protected async Task CreatePfs3FormattedDisk(string path, long diskSize = 10 * 1024 * 1024)
    {
        await using var stream = File.Open(path, FileMode.Create, FileAccess.ReadWrite);
        
        var rigidDiskBlock = RigidDiskBlock.Create(diskSize.ToUniversalSize());
        stream.SetLength(rigidDiskBlock.DiskSize);
        
        rigidDiskBlock.AddFileSystem(Pfs3DosType, await System.IO.File.ReadAllBytesAsync(Pfs3AioPath))
            .AddPartition("DH0", bootable: true);
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
        
        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

        await Pfs3Formatter.FormatPartition(stream, partitionBlock, "Workbench");
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
    
    protected async Task<Pfs3Volume> MountVolume(Stream stream)
    {
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

        return await Pfs3Volume.Mount(stream, partitionBlock);
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