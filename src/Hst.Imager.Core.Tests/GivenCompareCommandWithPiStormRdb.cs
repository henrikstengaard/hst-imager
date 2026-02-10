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

public class GivenCompareCommandWithPiStormRdb : CommandTestBase
{
    [Theory]
    [InlineData("mbr", "rdb")]
    [InlineData("mbR", "rdB")]
    [InlineData("MBR", "RDB")]
    public async Task When_CompareSrcToDestMbrPartition2RdbPartition1_Then_DataIsIdentical(string mbrPartitionTablePart,
        string rdbPartitionTablePart)
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        const int mbrPartitionNumber = 2;
        const int rdbPartitionNumber = 1;
        var comparePath = Path.Combine(destPath, mbrPartitionTablePart, mbrPartitionNumber.ToString(),
            rdbPartitionTablePart, rdbPartitionNumber.ToString());

        // arrange - create data
        var data = new byte[10.MB().ToSectorSize()];
        Array.Fill<byte>(data, 1);
        
        // arrange - create test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        await testCommandHelper.AddTestMedia(srcPath, srcPath);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - write data to src disk
        await TestHelper.WriteData(testCommandHelper, srcPath, 0, data);
        
        // arrange - create dest pistorm rdb disk with 2 partitions
        await PiStormRdbTestHelper.CreatePiStormRdbDisk(testCommandHelper, destPath);

        // arrange - write data to dest mbr partition 2 rdb partition 1
        await PiStormRdbTestHelper.WriteDataToPiStormRdbPartition(testCommandHelper, destPath, 
            mbrPartitionNumber, rdbPartitionNumber, data);

        // arrange - create compare command to compare src and dest mbr partition 2 rdb partition 1
        var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper, [],
            srcPath, 0, comparePath, 0, new Size(data.Length, Unit.Bytes), 0,
            false, false);

        // act - execute compare command
        var result = await compareCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);
    }
    
    [Theory]
    [InlineData("mbr", "rdb")]
    [InlineData("mbR", "rdB")]
    [InlineData("MBR", "RDB")]
    public async Task When_CompareSrcMbrPartition2RdbPartition1ToDest_Then_DataIsIdentical(string mbrPartitionTablePart,
        string rdbPartitionTablePart)
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        const int mbrPartitionNumber = 2;
        const int rdbPartitionNumber = 1;
        var comparePath = Path.Combine(srcPath, mbrPartitionTablePart, mbrPartitionNumber.ToString(),
            rdbPartitionTablePart, rdbPartitionNumber.ToString());

        // arrange - create data
        var data = new byte[10.MB().ToSectorSize()];
        Array.Fill<byte>(data, 1);
        
        // arrange - create test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        await testCommandHelper.AddTestMedia(srcPath, srcPath);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src pistorm rdb disk with 2 partitions
        await PiStormRdbTestHelper.CreatePiStormRdbDisk(testCommandHelper, srcPath);

        // arrange - write data to src mbr partition 2 rdb partition 1
        await PiStormRdbTestHelper.WriteDataToPiStormRdbPartition(testCommandHelper, srcPath, 
            mbrPartitionNumber, rdbPartitionNumber, data);

        // arrange - write data to dest disk
        await TestHelper.WriteData(testCommandHelper, destPath, 0, data);
        
        // arrange - create compare command to compare src and dest mbr partition 2 rdb partition 1
        var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper, [],
            comparePath, 0, destPath, 0, new Size(data.Length, Unit.Bytes), 0,
            false, false);

        // act - execute compare command
        var result = await compareCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);
    }
}