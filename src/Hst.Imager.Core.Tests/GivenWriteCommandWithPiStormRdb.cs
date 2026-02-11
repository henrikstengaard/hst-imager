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
using Size = Hst.Imager.Core.Models.Size;

namespace Hst.Imager.Core.Tests;

public class GivenWriteCommandWithPiStormRdb : CommandTestBase
{
    [Theory]
    [InlineData("mbr", "rdb")]
    [InlineData("mbR", "rdB")]
    [InlineData("MBR", "RDB")]
    public async Task When_WriteSrcToDestMbrPartition2RdbPartition1_Then_DataIsIdentical(string mbrPartitionTablePart,
        string rdbPartitionTablePart)
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        const int mbrPartitionNumber = 2;
        const int rdbPartitionNumber = 1;
        var writePath = Path.Combine(destPath, mbrPartitionTablePart, mbrPartitionNumber.ToString(),
            rdbPartitionTablePart, rdbPartitionNumber.ToString());

        // arrange - create src data
        var srcData = new byte[10.MB().ToSectorSize()];
        Array.Fill<byte>(srcData, 1);

        // arrange - create test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        await testCommandHelper.AddTestMedia(srcPath, srcPath);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src disk with data
        await TestHelper.WriteData(testCommandHelper, srcPath, 0, srcData);

        // arrange - create dest pistorm rdb with 2 partitions
        await PiStormRdbTestHelper.CreatePiStormRdbDisk(testCommandHelper, destPath);

        // arrange - create write command to write mbr partition 2 and rdb partition 1
        var writeCommand = new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper,
            [], srcPath, writePath, new Size(0, Unit.Bytes), 0, false,
            false, false, 0);

        // act - execute write command
        var result = await writeCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // arrange - get dest media
        var destMediaResult = await testCommandHelper.GetReadableMedia([], destPath);
        Assert.True(destMediaResult.IsSuccess);
        using var destMedia = destMediaResult.Value;
        var destStream = destMedia.Stream;

        // arrange - get dest mbr partition start offset
        var diskInfo = await testCommandHelper.ReadDiskInfo(destMedia);
        Assert.NotNull(diskInfo.MbrPartitionTablePart);
        var mbrPartitionPart = diskInfo.MbrPartitionTablePart.Parts.FirstOrDefault(x =>
            x.PartType == PartType.Partition && x.PartitionNumber == mbrPartitionNumber);
        Assert.NotNull(mbrPartitionPart);

        // arrange - get mbr partition stream and media
        var mbrPartitionStream = new VirtualStream(destStream, mbrPartitionPart.StartOffset, mbrPartitionPart.Size,
            mbrPartitionPart.Size);
        var mbrPartitionMedia = new Media("rdb", "rdb", Media.MediaType.Raw, false, mbrPartitionStream,
            false);
        var mbrDiskInfo = await testCommandHelper.ReadDiskInfo(mbrPartitionMedia);
        Assert.NotNull(mbrDiskInfo.RdbPartitionTablePart);
        var rdbPartitionPart = mbrDiskInfo.RdbPartitionTablePart.Parts.FirstOrDefault(x =>
            x.PartType == PartType.Partition && x.PartitionNumber == rdbPartitionNumber);
        Assert.NotNull(rdbPartitionPart);

        // assert - src data read is identical to rdb partition 1 data
        mbrPartitionStream.Position = rdbPartitionPart.StartOffset;
        var rdbPartitionData = await destStream.ReadBytes(srcData.Length);
        Assert.Equal(srcData.Length, rdbPartitionData.Length);
        Assert.Equal(srcData, rdbPartitionData);
    }
}