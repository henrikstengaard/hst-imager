using System;
using System.DirectoryServices.Protocols;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Streams;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenWriteCommandWithRdb : CommandTestBase
{
    [Theory]
    [InlineData("rdb")]
    [InlineData("rdB")]
    [InlineData("RDB")]
    public async Task When_WriteSrcToDestRdbPartition1_Then_DataIsIdentical(string partitionTablePart)
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var writePath = Path.Combine(destPath, partitionTablePart, "1");

        // arrange - create partition sizes
        var cylinderSize = 16 * 63 * 512;
        var rdbPartition1Size = 20.MB() + cylinderSize - 20.MB() % cylinderSize;
        var rdbPartition2Size = 40.MB() + cylinderSize- 40.MB() % cylinderSize;
        var srcSize = rdbPartition1Size; 

        // arrange - create src data
        var srcData = new byte[srcSize];
        Array.Fill<byte>(srcData, 1);
            
        // arrange - create write path and test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        await testCommandHelper.AddTestMedia(srcPath, srcPath);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src disk with data
        await TestHelper.WriteData(testCommandHelper, srcPath, 0, srcData);
        
        // arrange - create dest rdb disk with 2 partitions
        await TestHelper.CreateRdbDisk(testCommandHelper, destPath, 100.MB().ToSectorSize());
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, rdbPartition1Size);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, rdbPartition2Size);

        // arrange - create write command to write rdb partition 1
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

        // arrange - get dest rdb partition 1 start offset
        var diskInfo = await testCommandHelper.ReadDiskInfo(destMedia);
        Assert.NotNull(diskInfo.RdbPartitionTablePart);
        var rdbPartition1Part = diskInfo.RdbPartitionTablePart.Parts.FirstOrDefault(x => 
            x.PartType == PartType.Partition && x.PartitionNumber == 1);
        Assert.NotNull(rdbPartition1Part);
        
        // arrange - get dest disk and stream
        var destDisk = destMedia is DiskMedia destDiskMedia
            ? destDiskMedia.Disk
            : new DiscUtils.Raw.Disk(destMedia.Stream, Ownership.None);
        var destStream = destDisk.Content;

        // assert - src data read is identical to rdb partition 1 data
        destStream.Position = rdbPartition1Part.StartOffset;
        var rdbPartition1Data = await destStream.ReadBytes(srcData.Length);
        Assert.Equal(srcData.Length, rdbPartition1Data.Length);
        Assert.Equal(srcData, rdbPartition1Data);
    }

    [Fact]
    public async Task When_WriteSrcToDestRdbPartition2_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var writePath = Path.Combine(destPath, "rdb", "2");

        // arrange - create partition sizes
        var cylinderSize = 16 * 63 * 512;
        var rdbPartition1Size = 20.MB() + cylinderSize - 20.MB() % cylinderSize;
        var rdbPartition2Size = 40.MB() + cylinderSize- 40.MB() % cylinderSize;
        var srcSize = rdbPartition2Size; 

        // arrange - create src data
        var srcData = new byte[srcSize];
        Array.Fill<byte>(srcData, 1);
            
        // arrange - create write path and test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        await testCommandHelper.AddTestMedia(srcPath, srcPath);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src disk with data
        await TestHelper.WriteData(testCommandHelper, srcPath, 0, srcData);
        
        // arrange - create dest rdb disk with 2 partitions
        await TestHelper.CreateRdbDisk(testCommandHelper, destPath, 100.MB().ToSectorSize());
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, rdbPartition1Size);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, rdbPartition2Size);

        // arrange - create write command to write rdb partition 2
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

        // arrange - get dest rdb partition 2 start offset
        var diskInfo = await testCommandHelper.ReadDiskInfo(destMedia);
        Assert.NotNull(diskInfo.RdbPartitionTablePart);
        var rdbPartition2Part = diskInfo.RdbPartitionTablePart.Parts.FirstOrDefault(x => 
            x.PartType == PartType.Partition && x.PartitionNumber == 2);
        Assert.NotNull(rdbPartition2Part);
        
        // arrange - get dest disk and stream
        var destDisk = destMedia is DiskMedia destDiskMedia
            ? destDiskMedia.Disk
            : new DiscUtils.Raw.Disk(destMedia.Stream, Ownership.None);
        var destStream = destDisk.Content;

        // assert - src data read is identical to rdb partition 2 data
        destStream.Position = rdbPartition2Part.StartOffset;
        var rdbPartition2Data = await destStream.ReadBytes(srcData.Length);
        Assert.Equal(srcData.Length, rdbPartition2Data.Length);
        Assert.Equal(srcData, rdbPartition2Data);
    }
    
    [Fact]
    public async Task When_WriteSrcLargerThanDestToDestRdbPartition_Then_ErrorIsReturned()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var writePath = Path.Combine(destPath, "rdb", "1");

        // arrange - create partition sizes
        var cylinderSize = 16 * 63 * 512;
        var rdbPartition1Size = 20.MB() + cylinderSize - 20.MB() % cylinderSize;
        var rdbPartition2Size = 40.MB() + cylinderSize- 40.MB() % cylinderSize;
        var srcSize = rdbPartition1Size * 2; 

        // arrange - create src data
        var srcData = new byte[srcSize];
        Array.Fill<byte>(srcData, 1);
            
        // arrange - create write path and test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        await testCommandHelper.AddTestMedia(srcPath, srcPath);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src disk with data
        await TestHelper.WriteData(testCommandHelper, srcPath, 0, srcData);
        
        // arrange - create dest rdb disk with 2 partitions
        await TestHelper.CreateRdbDisk(testCommandHelper, destPath, 100.MB().ToSectorSize());
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, rdbPartition1Size);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, rdbPartition2Size);

        // arrange - create write command to write rdb partition 1
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