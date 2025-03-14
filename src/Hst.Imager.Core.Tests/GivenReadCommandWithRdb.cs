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

public class GivenReadCommandWithRdb : CommandTestBase
{
    [Fact]
    public async Task When_ReadSrcRdbPartition1ToDest_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        
        // arrange - create rdb partition 1 and 2 data
        var cylinderSize = 16 * 63 * 512;
        var rdbPartition1Data = new byte[20.MB() + cylinderSize - (20.MB() % cylinderSize)];
        Array.Fill<byte>(rdbPartition1Data, 1);
        var rdbPartition2Data = new byte[40.MB() + cylinderSize- (40.MB() % cylinderSize)];
        Array.Fill<byte>(rdbPartition2Data, 2);
            
        // arrange - create read path and test command helper
        var readPath = Path.Combine(srcPath, "rdb", "1");
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        testCommandHelper.AddTestMedia(srcPath, 100.MB().ToSectorSize());
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src rdb disk with 2 partitions
        await TestHelper.CreateRdbDisk(testCommandHelper, srcPath, 100.MB().ToSectorSize());
        await TestHelper.AddRdbDiskPartition(testCommandHelper, srcPath, data: rdbPartition1Data);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, srcPath, data: rdbPartition2Data);

        // arrange - create read command to read rdb partition 1
        var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
            [], readPath, destPath, new Size(0, Unit.Bytes), 0, false,
            false, 0);

        // act - execute read command
        var result = await readCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - data read is identical to rdb partition 1 data
        var destBytes = await testCommandHelper.ReadMediaData(destPath);
        Assert.Equal(rdbPartition1Data.Length, destBytes.Length);
        Assert.Equal(rdbPartition1Data, destBytes);
    }

    [Fact]
    public async Task When_ReadSrcRdbPartition2ToDest_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";

        // arrange - create rdb partition 1 and 2 data
        var cylinderSize = 16 * 63 * 512;
        var rdbPartition1Data = new byte[20.MB() + cylinderSize - (20.MB() % cylinderSize)];
        Array.Fill<byte>(rdbPartition1Data, 1);
        var rdbPartition2Data = new byte[40.MB() + cylinderSize- (40.MB() % cylinderSize)];
        Array.Fill<byte>(rdbPartition2Data, 2);
            
        // arrange - create read path and test command helper
        var readPath = Path.Combine(srcPath, "rdb", "2");
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        testCommandHelper.AddTestMedia(srcPath, 100.MB().ToSectorSize());
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src rdb disk with 2 partitions
        await TestHelper.CreateRdbDisk(testCommandHelper, srcPath, 100.MB().ToSectorSize());
        await TestHelper.AddRdbDiskPartition(testCommandHelper, srcPath, data: rdbPartition1Data);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, srcPath, data: rdbPartition2Data);

        // arrange - create read command to read rdb partition 2
        var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
            [], readPath, destPath, new Size(0, Unit.Bytes), 0, false,
            false, 0);

        // act - execute read command
        var result = await readCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - data read is identical to rdb partition 2 data
        var destBytes = await testCommandHelper.ReadMediaData(destPath);
        Assert.Equal(rdbPartition2Data.Length, destBytes.Length);
        Assert.Equal(rdbPartition2Data, destBytes);
    }
}