using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenTransferCommandWithPiStormRdb : CommandTestBase
{
    [Theory]
    [InlineData("mbr", "rdb")]
    [InlineData("mbR", "rdB")]
    [InlineData("MBR", "RDB")]
    public async Task When_TransferFromSrcMbrPartition2RdbPartition1ToDest_Then_DataIsIdentical(string mbrPartitionTablePart,
        string rdbPartitionTablePart)
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        const int mbrPartitionNumber = 2;
        const int rdbPartitionNumber = 1;
        var transferPath = Path.Combine(srcPath, mbrPartitionTablePart, mbrPartitionNumber.ToString(),
            rdbPartitionTablePart, rdbPartitionNumber.ToString());

        // arrange - create data
        var data = new byte[10.MB().ToSectorSize()];
        Array.Fill<byte>(data, 1);

        try
        {
            // arrange - create test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create src and dest medias
            await testCommandHelper.AddTestMedia(srcPath, srcPath);

            // arrange - create src pistorm rdb disk with 2 partitions
            await PiStormRdbTestHelper.CreatePiStormRdbDisk(testCommandHelper, srcPath);
        
            // arrange - write data to src mbr partition 2 rdb partition 1
            await PiStormRdbTestHelper.WriteDataToPiStormRdbPartition(testCommandHelper, srcPath, 
                mbrPartitionNumber, rdbPartitionNumber, data);

            // arrange - create transfer command to transfer src mbr partition 2 rdb partition 1 to dest
            var transferCommand = new TransferCommand(testCommandHelper, transferPath, destPath, 
                new Size(data.Length, Unit.Bytes), false, 0, 0);

            // act - execute transfer command
            var result = await transferCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);

            // assert - data read is identical to mbr partition 1 data
            var destBytes = await testCommandHelper.ReadMediaData(destPath);
            Assert.True(destBytes.Length >= data.Length);
            Assert.Equal(data, destBytes.Take(data.Length));
        }
        finally
        {
            TestHelper.DeletePaths(srcPath, destPath);
        }
    }
}