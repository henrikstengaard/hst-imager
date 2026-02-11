using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenWriteCommandWithMbr : CommandTestBase
{
    [Theory]
    [InlineData("mbr")]
    [InlineData("mbR")]
    [InlineData("MBR")]
    public async Task When_WriteSrcToDestMbrPartition1_Then_DataIsIdentical(string partitionTablePart)
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var writePath = Path.Combine(destPath, partitionTablePart, "1");

        // arrange - create src data
        var srcData = new byte[20.MB().ToSectorSize()];
        Array.Fill<byte>(srcData, 1);
            
        // arrange - create write path and test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        await testCommandHelper.AddTestMedia(srcPath, srcPath);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src disk with data
        await TestHelper.WriteData(testCommandHelper, srcPath, 0, srcData);
        
        // arrange - create dest mbr disk with 2 partitions
        await TestHelper.CreateMbrDisk(testCommandHelper, destPath, 100.MB().ToSectorSize());
        await TestHelper.AddMbrDiskPartition(testCommandHelper, destPath, 20.MB().ToSectorSize());
        await TestHelper.AddMbrDiskPartition(testCommandHelper, destPath, 40.MB().ToSectorSize());

        // arrange - create write command to write mbr partition 1
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

        // arrange - get dest mbr partition 1 start offset
        var diskInfo = await testCommandHelper.ReadDiskInfo(destMedia);
        Assert.NotNull(diskInfo.MbrPartitionTablePart);
        var mbrPartition1Part = diskInfo.MbrPartitionTablePart.Parts.FirstOrDefault(x => 
            x.PartType == PartType.Partition && x.PartitionNumber == 1);
        Assert.NotNull(mbrPartition1Part);
        
        // arrange - get dest disk and stream
        var destDisk = await MediaHelper.ResolveVirtualDisk(destMedia);
        var destStream = destDisk.Content;

        // assert - src data read is identical to mbr partition 1 data
        destStream.Position = mbrPartition1Part.StartOffset;
        var mbrPartition1Data = await destStream.ReadBytes(srcData.Length);
        Assert.Equal(srcData.Length, mbrPartition1Data.Length);
        Assert.Equal(srcData, mbrPartition1Data);
    }
    
    [Fact]
    public async Task When_WriteSrcToDestMbrPartition2_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var writePath = Path.Combine(destPath, "mbr", "2");

        // arrange - create src data
        var srcData = new byte[40.MB().ToSectorSize()];
        Array.Fill<byte>(srcData, 2);
            
        // arrange - create write path and test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        await testCommandHelper.AddTestMedia(srcPath, srcPath);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src disk with data
        await TestHelper.WriteData(testCommandHelper, srcPath, 0, srcData);
        
        // arrange - create dest mbr disk with 2 partitions
        await TestHelper.CreateMbrDisk(testCommandHelper, destPath, 100.MB().ToSectorSize());
        await TestHelper.AddMbrDiskPartition(testCommandHelper, destPath, 20.MB().ToSectorSize());
        await TestHelper.AddMbrDiskPartition(testCommandHelper, destPath, 40.MB().ToSectorSize());

        // arrange - create write command to write src to dest mbr partition 2
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

        // arrange - get dest mbr partition 2 start offset
        var diskInfo = await testCommandHelper.ReadDiskInfo(destMedia);
        Assert.NotNull(diskInfo.MbrPartitionTablePart);
        var mbrPartition2Part = diskInfo.MbrPartitionTablePart.Parts.FirstOrDefault(x => 
            x.PartType == PartType.Partition && x.PartitionNumber == 2);
        Assert.NotNull(mbrPartition2Part);
        
        // arrange - get dest disk and stream
        var destDisk = await MediaHelper.ResolveVirtualDisk(destMedia);
        var destStream = destDisk.Content;

        // assert - src data read is identical to mbr partition 2 data
        destStream.Position = mbrPartition2Part.StartOffset;
        var mbrPartition2Data = await destStream.ReadBytes(srcData.Length);
        Assert.Equal(srcData.Length, mbrPartition2Data.Length);
        Assert.Equal(srcData, mbrPartition2Data);
    }

    [Fact]
    public async Task When_WriteSrcLargerThanDestToDestMbrPartition_Then_ErrorIsReturned()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var writePath = Path.Combine(destPath, "mbr", "1");

        // arrange - create src data
        var srcData = new byte[20.MB().ToSectorSize() * 2];
        Array.Fill<byte>(srcData, 1);
        
        // arrange - create write path and test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        await testCommandHelper.AddTestMedia(srcPath, srcPath);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src disk with data
        await TestHelper.WriteData(testCommandHelper, srcPath, 0, srcData);
    
        // arrange - create dest mbr disk with 2 partitions
        await TestHelper.CreateMbrDisk(testCommandHelper, destPath, 100.MB().ToSectorSize());
        await TestHelper.AddMbrDiskPartition(testCommandHelper, destPath, 20.MB().ToSectorSize());
        await TestHelper.AddMbrDiskPartition(testCommandHelper, destPath, 40.MB().ToSectorSize());

        // arrange - create write command to write mbr partition 1
        var writeCommand = new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper,
            [], srcPath, writePath, new Size(0, Unit.Bytes), 0, false,
            false, false, 0);

        // act - execute write command
        var result = await writeCommand.Execute(CancellationToken.None);
        
        // assert - write command returned error
        Assert.True(result.IsFaulted);
        Assert.NotNull(result.Error);
        Assert.IsType<WriteSizeTooLargeError>(result.Error);
    }
}