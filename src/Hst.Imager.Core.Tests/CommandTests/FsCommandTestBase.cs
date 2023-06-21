namespace Hst.Imager.Core.Tests.CommandTests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amiga;
using Amiga.Extensions;
using Amiga.FileSystems.FastFileSystem;
using Amiga.FileSystems.Pfs3;
using Amiga.RigidDiskBlocks;
using Commands;
using Hst.Core.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
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

    protected async Task CreateMbr(TestCommandHelper testCommandHelper, string path,
        long diskSize = 10 * 1024 * 1024)
    {
        var mediaResult = testCommandHelper.GetWritableFileMedia(path, diskSize, true);
        using (var media = mediaResult.Value)
        {
            var stream = media.Stream;
            if (!path.ToLower().EndsWith(".vhd"))
            {
                stream.SetLength(diskSize);
            }
        }
        
        var cancellationTokenSource = new CancellationTokenSource();
        var mbrInitCommand = new MbrInitCommand(new NullLogger<MbrInitCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), path);
        var result = await mbrInitCommand.Execute(cancellationTokenSource.Token);

        if (result.IsFaulted)
        {
            throw new IOException(result.Error.ToString());
        }
    }
    
    protected async Task CreateRdbWithPfs3(TestCommandHelper testCommandHelper, string path,
        long diskSize = 10 * 1024 * 1024, long rdbSize = 0, uint rdbBlockLo = 0)
    {
        var mediaResult = testCommandHelper.GetWritableFileMedia(path, diskSize, true);
        using var media = mediaResult.Value;
        var stream = media.Stream;
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

    protected async Task CreatePfs3FormattedDisk(TestCommandHelper testCommandHelper, string path,
        long diskSize = 10 * 1024 * 1024)
    {
        var mediaResult = testCommandHelper.GetWritableFileMedia(path, diskSize, true);
        using var media = mediaResult.Value;
        var stream = media.Stream;

        var rigidDiskBlock = RigidDiskBlock.Create(diskSize.ToUniversalSize());

        rigidDiskBlock.AddFileSystem(Pfs3DosType, await System.IO.File.ReadAllBytesAsync(Pfs3AioPath))
            .AddPartition("DH0", bootable: true);
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

        await Pfs3Formatter.FormatPartition(stream, partitionBlock, "Workbench");
    }

    protected async Task CreateDos7FormattedDisk(TestCommandHelper testCommandHelper, string path,
        long diskSize = 10 * 1024 * 1024)
    {
        var mediaResult = testCommandHelper.GetWritableFileMedia(path, diskSize, true);
        using var media = mediaResult.Value;
        var stream = media.Stream;

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