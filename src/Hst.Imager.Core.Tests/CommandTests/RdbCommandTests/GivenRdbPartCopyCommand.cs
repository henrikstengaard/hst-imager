using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.RdbCommandTests;

public class GivenRdbPartCopyCommand
{
    [Fact]
    public async Task When_CopyingPartition_Then_PartitionIsCopied()
    {
        // arrange - paths
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}.vhd";
        
        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();
        
        // arrange - create src rdb disk with pfs3 filesystem and one partition
        testCommandHelper.AddTestMedia(srcPath, 0);
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, srcPath);

        // arrange - create dest rdb disk pfs3 filesystem and no partitions
        testCommandHelper.AddTestMedia(destPath, 0);
        await TestHelper.CreateRdbDisk(testCommandHelper, destPath);

        // arrange - create rdb part copy command to copy part 1 from src to dest
        var rdbPartCopyCommand = new RdbPartCopyCommand(new NullLogger<RdbPartCopyCommand>(), testCommandHelper,
            [], srcPath, 1, destPath, null, null);
        
        // act - execute rdb part copy command
        var result = await rdbPartCopyCommand.Execute(CancellationToken.None);
        
        // assert - rdb part copy command executed successfully
        Assert.True(result.IsSuccess);

        // assert - dest disk has one partition
        var destMediaResult = await testCommandHelper.GetReadableFileMedia(destPath);
        using var destMedia = destMediaResult.Value;
        var rigidDiskBlock = await RigidDiskBlockReader.Read(destMedia.Stream);
        Assert.NotNull(rigidDiskBlock);
        var partitions = rigidDiskBlock.PartitionBlocks.ToList();
        Assert.Single(partitions);
        Assert.Single(partitions, partition => partition.DriveName.Equals("DH0") &&
                                               partition.DosType.SequenceEqual(TestHelper.Pfs3DosType));
    }

    [Fact]
    public async Task When_CopyingPartitionChangingName_Then_CopiedPartitionHasNameChanged()
    {
        // arrange - paths
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}.vhd";
        const string name = "DH1";
        
        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - create src rdb disk with pfs3 filesystem and one partition
        testCommandHelper.AddTestMedia(srcPath, 0);
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, srcPath);

        // arrange - create dest rdb disk pfs3 filesystem and no partitions
        testCommandHelper.AddTestMedia(destPath, 0);
        await TestHelper.CreateRdbDisk(testCommandHelper, destPath);

        // arrange - create rdb part copy command to copy part 1 from src to dest changing name
        var rdbPartCopyCommand = new RdbPartCopyCommand(new NullLogger<RdbPartCopyCommand>(), testCommandHelper,
            [], srcPath, 1, destPath, name, null);
        
        // act - execute rdb part copy command
        var result = await rdbPartCopyCommand.Execute(CancellationToken.None);
        
        // assert - rdb part copy command executed successfully
        Assert.True(result.IsSuccess);

        // assert - dest disk has one partition with changed name
        var destMediaResult = await testCommandHelper.GetReadableFileMedia(destPath);
        using var destMedia = destMediaResult.Value;
        var rigidDiskBlock = await RigidDiskBlockReader.Read(destMedia.Stream);
        Assert.NotNull(rigidDiskBlock);
        var partitions = rigidDiskBlock.PartitionBlocks.ToList();
        Assert.Single(partitions);
        Assert.Single(partitions, partition => partition.DriveName.Equals(name) &&
                                               partition.DosType.SequenceEqual(TestHelper.Pfs3DosType));
    }
    
    [Fact]
    public async Task When_CopyingPartitionChangingDosType_Then_CopiedPartitionHasDosTypeChanged()
    {
        // arrange - paths
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}.vhd";
        
        // arrange - test command helper
        using var testCommandHelper = new TestCommandHelper();

        // arrange - create src rdb disk with pfs3 filesystem and one partition
        testCommandHelper.AddTestMedia(srcPath, 0);
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, srcPath);

        // arrange - create dest rdb disk pds3 filesystem and no partitions
        testCommandHelper.AddTestMedia(destPath, 0);
        await TestHelper.CreateRdbDisk(testCommandHelper, destPath);
        await Tests.RdbTestHelper.AddFileSystem(testCommandHelper, destPath, "PDS3", TestHelper.Pfs3AioBytes);

        // arrange - create rdb part copy command to copy part 1 from src to dest changing dos type
        var rdbPartCopyCommand = new RdbPartCopyCommand(new NullLogger<RdbPartCopyCommand>(), testCommandHelper,
            [], srcPath, 1, destPath, null, "PDS3");
        
        // act - execute rdb part copy command
        var result = await rdbPartCopyCommand.Execute(CancellationToken.None);
        
        // assert - rdb part copy command executed successfully
        Assert.True(result.IsSuccess);

        // assert - dest disk has one partition with dos type
        var destMediaResult = await testCommandHelper.GetReadableFileMedia(destPath);
        using var destMedia = destMediaResult.Value;
        var rigidDiskBlock = await RigidDiskBlockReader.Read(destMedia.Stream);
        Assert.NotNull(rigidDiskBlock);
        var partitions = rigidDiskBlock.PartitionBlocks.ToList();
        Assert.Single(partitions);
        Assert.Single(partitions, partition => partition.DriveName.Equals("DH0") &&
                                               partition.DosType.SequenceEqual(TestHelper.Pds3DosType));
    }
}