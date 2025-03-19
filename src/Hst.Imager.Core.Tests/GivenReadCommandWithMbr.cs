using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenReadCommandWithMbr : CommandTestBase
{
    [Fact]
    public async Task When_ReadSrcMbrPartition1ToDest_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";

        // arrange - create mbr partition 1 and 2 data
        var mbrPartition1Data = new byte[20.MB().ToSectorSize()];
        Array.Fill<byte>(mbrPartition1Data, 1);
        var mbrPartition2Data = new byte[40.MB().ToSectorSize()];
        Array.Fill<byte>(mbrPartition2Data, 2);
            
        // arrange - create read path and test command helper
        var readPath = Path.Combine(srcPath, "mbr", "1");
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        testCommandHelper.AddTestMedia(srcPath, 100.MB().ToSectorSize());
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src mbr disk with 2 partitions
        await TestHelper.CreateMbrDisk(testCommandHelper, srcPath, 100.MB().ToSectorSize());
        await TestHelper.AddMbrDiskPartition(testCommandHelper, srcPath, data: mbrPartition1Data);
        await TestHelper.AddMbrDiskPartition(testCommandHelper, srcPath, data: mbrPartition2Data);

        // arrange - create read command to read mbr partition 2
        var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
            [], readPath, destPath, new Size(0, Unit.Bytes), 0, false,
            false, 0);

        // act - execute read command
        var result = await readCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - data read is identical to mbr partition 1 data
        var destBytes = await testCommandHelper.ReadMediaData(destPath);
        Assert.Equal(mbrPartition1Data.Length, destBytes.Length);
        Assert.Equal(mbrPartition1Data, destBytes);
    }

    [Fact]
    public async Task When_ReadSrcMbrPartition2ToDest_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";

        // arrange - create mbr partition 1 and 2 data
        var mbrPartition1Data = new byte[20.MB().ToSectorSize()];
        Array.Fill<byte>(mbrPartition1Data, 1);
        var mbrPartition2Data = new byte[40.MB().ToSectorSize()];
        Array.Fill<byte>(mbrPartition2Data, 2);
            
        // arrange - create read path and test command helper
        var readPath = Path.Combine(srcPath, "mbr", "2");
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        testCommandHelper.AddTestMedia(srcPath, 100.MB().ToSectorSize());
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src mbr disk with 2 partitions
        await TestHelper.CreateMbrDisk(testCommandHelper, srcPath, 100.MB().ToSectorSize());
        await TestHelper.AddMbrDiskPartition(testCommandHelper, srcPath, data: mbrPartition1Data);
        await TestHelper.AddMbrDiskPartition(testCommandHelper, srcPath, data: mbrPartition2Data);

        // arrange - create read command to read mbr partition 2
        var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
            [], readPath, destPath, new Size(0, Unit.Bytes), 0, false,
            false, 0);

        // act - execute read command
        var result = await readCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - data read is identical to mbr partition 2 data
        var destBytes = await testCommandHelper.ReadMediaData(destPath);
        Assert.Equal(mbrPartition2Data.Length, destBytes.Length);
        Assert.Equal(mbrPartition2Data, destBytes);
    }
}