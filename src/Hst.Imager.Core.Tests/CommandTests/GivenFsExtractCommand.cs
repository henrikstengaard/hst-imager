namespace Hst.Imager.Core.Tests.CommandTests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amiga.Extensions;
using Amiga.FileSystems.Pfs3;
using Amiga.RigidDiskBlocks;
using Commands;
using Hst.Core.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using File = System.IO.File;

public class GivenFsExtractCommand : CommandTestBase
{
    [Fact(Skip = "work in progress"), Trait("category","amiga-os-31")]
    public async Task WhenExtractingAmigaOs31WorkbenchAdfToHdfThenEntriesExist()
    {
        var sourcePath = @"c:\Users\Public\Documents\Amiga Files\Shared\adf\amiga-os-310-workbench.adf";
        var destinationPath = "dest.hdf";
        await CreatePfs3FormattedDisk(destinationPath);
        
        var fakeCommandHelper = new TestCommandHelper();
        var cancellationTokenSource = new CancellationTokenSource();

        var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
            new List<IPhysicalDrive>(),
            sourcePath, @$"{destinationPath}\rdb\dh0", true, true);
        var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        var pfs3Volume = await MountVolume(File.Open(destinationPath, FileMode.Open, FileAccess.Read));

        var entries = await pfs3Volume.ListEntries();
    }
    
    private static readonly byte[] Pfs3DosType = { 0x50, 0x44, 0x53, 0x3 };
    private static readonly string Pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio");
    
    protected async Task CreatePfs3FormattedDisk(string path, long diskSize = 10 * 1024 * 1024)
    {
        await using var stream = System.IO.File.Open(path, FileMode.Create, FileAccess.ReadWrite);
        
        var rigidDiskBlock = RigidDiskBlock.Create(diskSize.ToUniversalSize());
        stream.SetLength(rigidDiskBlock.DiskSize);
        
        rigidDiskBlock.AddFileSystem(Pfs3DosType, await System.IO.File.ReadAllBytesAsync(Pfs3AioPath))
            .AddPartition("DH0", bootable: true);
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
        
        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

        await Pfs3Formatter.FormatPartition(stream, partitionBlock, "Workbench");
    }
    
    protected async Task<Pfs3Volume> MountVolume(Stream stream)
    {
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

        return await Pfs3Volume.Mount(stream, partitionBlock);
    }
}