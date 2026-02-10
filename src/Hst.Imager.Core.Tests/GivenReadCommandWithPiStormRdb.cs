using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenReadCommandWithPiStormRdb : CommandTestBase
{
    [Theory]
    [InlineData("mbr", "rdb")]
    [InlineData("mbR", "rdB")]
    [InlineData("MBR", "RDB")]
    public async Task When_ReadFromSrcMbrPartition2RdbPartition1ToDest_Then_DataIsIdentical(string mbrPartitionTablePart,
        string rdbPartitionTablePart)
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        const int mbrPartitionNumber = 2;
        const int rdbPartitionNumber = 1;
        var readPath = Path.Combine(srcPath, mbrPartitionTablePart, mbrPartitionNumber.ToString(),
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

        // arrange - create read command to read mbr partition 2 rdb partition 1
        var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
            [], readPath, destPath, new Size(0, Unit.Bytes), 0, false,
            false, 0);

        // act - execute read command
        var result = await readCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - data read is identical to mbr partition 1 data
        var destBytes = await testCommandHelper.ReadMediaData(destPath);
        Assert.True(destBytes.Length >= data.Length);
        Assert.Equal(data, destBytes.Take(data.Length));
    }
}