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

public class GivenWriteCommandWithGpt : CommandTestBase
{
    [Fact]
    public async Task When_WriteSrcToDestGptPartition1_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var writePath = Path.Combine(destPath, "gpt", "1");

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
        
        // arrange - create dest gpt disk with 2 partitions
        await TestHelper.CreateGptDisk(testCommandHelper, destPath, 100.MB().ToSectorSize());
        await TestHelper.AddGptDiskPartition(testCommandHelper, destPath, 20.MB().ToSectorSize());
        await TestHelper.AddGptDiskPartition(testCommandHelper, destPath, 40.MB().ToSectorSize());

        // arrange - create write command to write gpt partition 1
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

        // arrange - get dest gpt partition 1 start offset
        var diskInfo = await testCommandHelper.ReadDiskInfo(destMedia);
        Assert.NotNull(diskInfo.GptPartitionTablePart);
        var gptPartition1Part = diskInfo.GptPartitionTablePart.Parts.FirstOrDefault(x => 
            x.PartType == PartType.Partition && x.PartitionNumber == 1);
        Assert.NotNull(gptPartition1Part);
        
        // arrange - get dest disk and stream
        var destDisk = await MediaHelper.ResolveVirtualDisk(destMedia);
        var destStream = destDisk.Content;

        // assert - src data read is identical to gpt partition 1 data
        destStream.Position = gptPartition1Part.StartOffset;
        var gptPartition1Data = await destStream.ReadBytes(srcData.Length);
        Assert.Equal(srcData.Length, gptPartition1Data.Length);
        Assert.Equal(srcData, gptPartition1Data);
    }
    
    [Fact]
    public async Task When_WriteSrcToDestGptPartition2_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var writePath = Path.Combine(destPath, "gpt", "2");

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
        
        // arrange - create dest gpt disk with 2 partitions
        await TestHelper.CreateGptDisk(testCommandHelper, destPath, 100.MB().ToSectorSize());
        await TestHelper.AddGptDiskPartition(testCommandHelper, destPath, 20.MB().ToSectorSize());
        await TestHelper.AddGptDiskPartition(testCommandHelper, destPath, 40.MB().ToSectorSize());

        // arrange - create write command to write src to dest gpt partition 2
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

        // arrange - get dest gpt partition 2 start offset
        var diskInfo = await testCommandHelper.ReadDiskInfo(destMedia);
        Assert.NotNull(diskInfo.GptPartitionTablePart);
        var gptPartition2Part = diskInfo.GptPartitionTablePart.Parts.FirstOrDefault(x => 
            x.PartType == PartType.Partition && x.PartitionNumber == 2);
        Assert.NotNull(gptPartition2Part);
        
        // arrange - get dest disk and stream
        var destDisk = await MediaHelper.ResolveVirtualDisk(destMedia);
        var destStream = destDisk.Content;

        // assert - src data read is identical to gpt partition 2 data
        destStream.Position = gptPartition2Part.StartOffset;
        var gptPartition2Data = await destStream.ReadBytes(srcData.Length);
        Assert.Equal(srcData.Length, gptPartition2Data.Length);
        Assert.Equal(srcData, gptPartition2Data);
    }

    [Fact]
    public async Task When_WriteSrcLargerThanDestToDestGptPartition_Then_ErrorIsReturned()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var writePath = Path.Combine(destPath, "gpt", "1");

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
        
        // arrange - create dest gpt disk with 2 partitions
        await TestHelper.CreateGptDisk(testCommandHelper, destPath, 100.MB().ToSectorSize());
        await TestHelper.AddGptDiskPartition(testCommandHelper, destPath, 20.MB().ToSectorSize());
        await TestHelper.AddGptDiskPartition(testCommandHelper, destPath, 40.MB().ToSectorSize());

        // arrange - create write command to write gpt partition 1
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