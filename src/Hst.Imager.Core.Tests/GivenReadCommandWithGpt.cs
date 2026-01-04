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

public class GivenReadCommandWithGpt : CommandTestBase
{
    [Fact]
    public async Task When_ReadSrcGptPartition1ToDest_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";

        // arrange - create gpt partition 1 and 2 data
        var gptPartition1Data = new byte[20.MB().ToSectorSize()];
        Array.Fill<byte>(gptPartition1Data, 1);
        var gptPartition2Data = new byte[40.MB().ToSectorSize()];
        Array.Fill<byte>(gptPartition2Data, 2);
            
        // arrange - create read path and test command helper
        var readPath = Path.Combine(srcPath, "gpt", "1");
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        testCommandHelper.AddTestMedia(srcPath, 0);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src gpt disk with 2 partitions
        await TestHelper.CreateGptDisk(testCommandHelper, srcPath, 100.MB().ToSectorSize());
        await TestHelper.AddGptDiskPartition(testCommandHelper, srcPath, data: gptPartition1Data);
        await TestHelper.AddGptDiskPartition(testCommandHelper, srcPath, data: gptPartition2Data);

        // arrange - create read command to read gpt partition 1
        var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
            [], readPath, destPath, new Size(0, Unit.Bytes), 0, false,
            false, 0);

        // act - execute read command
        var result = await readCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - data read is identical to gpt partition 1 data
        var destBytes = await testCommandHelper.ReadMediaData(destPath);
        Assert.Equal(gptPartition1Data.Length, destBytes.Length);
        Assert.Equal(gptPartition1Data, destBytes);
    }

    [Fact]
    public async Task When_ReadSrcGptPartition2ToDest_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";

        // arrange - create gpt partition 1 and 2 data
        var gptPartition1Data = new byte[20.MB().ToSectorSize()];
        Array.Fill<byte>(gptPartition1Data, 1);
        var gptPartition2Data = new byte[40.MB().ToSectorSize()];
        Array.Fill<byte>(gptPartition2Data, 2);
            
        // arrange - create read path and test command helper
        var readPath = Path.Combine(srcPath, "gpt", "2");
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        testCommandHelper.AddTestMedia(srcPath, 0);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src gpt disk with 2 partitions
        await TestHelper.CreateGptDisk(testCommandHelper, srcPath, 100.MB().ToSectorSize());
        await TestHelper.AddGptDiskPartition(testCommandHelper, srcPath, data: gptPartition1Data);
        await TestHelper.AddGptDiskPartition(testCommandHelper, srcPath, data: gptPartition2Data);

        // arrange - create read command to read gpt partition 2
        var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
            [], readPath, destPath, new Size(0, Unit.Bytes), 0, false,
            false, 0);

        // act - execute read command
        var result = await readCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - data read is identical to gpt partition 2 data
        var destBytes = await testCommandHelper.ReadMediaData(destPath);
        Assert.Equal(gptPartition2Data.Length, destBytes.Length);
        Assert.Equal(gptPartition2Data, destBytes);
    }
}