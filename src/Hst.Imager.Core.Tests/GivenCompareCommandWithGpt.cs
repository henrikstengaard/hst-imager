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

public class GivenCompareCommandWithGpt : CommandTestBase
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_CompareSrcToDestGptDisk_Then_DataIsIdentical(bool skipZeroFilled)
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        
        // arrange - create write path and test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        await testCommandHelper.AddTestMedia(srcPath, srcPath);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src gpt disk with 2 partitions
        await TestHelper.CreateGptDisk(testCommandHelper, srcPath, 100.MB().ToSectorSize());
        await TestHelper.AddGptDiskPartition(testCommandHelper, srcPath, 20.MB().ToSectorSize());
        await TestHelper.AddGptDiskPartition(testCommandHelper, srcPath, 40.MB().ToSectorSize());

        // arrange - create dest gpt disk cloned from src gpt disk
        var srcData = await TestHelper.ReadData(testCommandHelper, srcPath);
        await TestHelper.WriteData(testCommandHelper, destPath, 0, srcData);

        // arrange - create compare command
        var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper,
            [], srcPath, 0, destPath, 0, new Size(0, Unit.Bytes), 0, false,
            skipZeroFilled);

        // act - execute compare command
        var result = await compareCommand.Execute(CancellationToken.None);
        
        // assert - compare command returned success
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_CompareSrcToZipCompressedDestGptDisk_Then_DataIsIdentical(bool skipZeroFilled)
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.zip";

        try
        {
            // arrange - create write path and test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create src media
            await testCommandHelper.AddTestMedia(srcPath, srcPath);

            // arrange - create src gpt disk with 2 partitions
            await TestHelper.CreateGptDisk(testCommandHelper, srcPath, 100.MB().ToSectorSize());
            await TestHelper.AddGptDiskPartition(testCommandHelper, srcPath, 20.MB().ToSectorSize());
            await TestHelper.AddGptDiskPartition(testCommandHelper, srcPath, 40.MB().ToSectorSize());

            // arrange - create zip compressed dest gpt disk cloned from src gpt disk
            var srcData = await TestHelper.ReadData(testCommandHelper, srcPath);
            var zipCompressedImgData = await TestHelper.CreateZipCompressedImgData(srcData);
            await File.WriteAllBytesAsync(destPath, zipCompressedImgData);

            // arrange - create compare command
            var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper,
                [], srcPath, 0, destPath, 0, new Size(0, Unit.Bytes), 0, false,
                skipZeroFilled);

            // act - execute compare command
            var result = await compareCommand.Execute(CancellationToken.None);
        
            // assert - compare command returned success
            Assert.True(result.IsSuccess);
        }
        finally
        {
            TestHelper.DeletePaths(srcPath, destPath);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_CompareZipCompressedSrcToDestGptDisk_Then_DataIsIdentical(bool skipZeroFilled)
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.zip";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";

        try
        {
            // arrange - create write path and test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create dest media
            await testCommandHelper.AddTestMedia(destPath, srcPath);

            // arrange - create dest gpt disk with 2 partitions
            await TestHelper.CreateGptDisk(testCommandHelper, destPath, 100.MB().ToSectorSize());
            await TestHelper.AddGptDiskPartition(testCommandHelper, destPath, 20.MB().ToSectorSize());
            await TestHelper.AddGptDiskPartition(testCommandHelper, destPath, 40.MB().ToSectorSize());

            // arrange - create zip compressed src gpt disk cloned from dest gpt disk
            var destData = await TestHelper.ReadData(testCommandHelper, destPath);
            var zipCompressedImgData = await TestHelper.CreateZipCompressedImgData(destData);
            await File.WriteAllBytesAsync(srcPath, zipCompressedImgData);

            // arrange - create compare command
            var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper,
                [], srcPath, 0, destPath, 0, new Size(0, Unit.Bytes), 0, false,
                skipZeroFilled);

            // act - execute compare command
            var result = await compareCommand.Execute(CancellationToken.None);
        
            // assert - compare command returned success
            Assert.True(result.IsSuccess);
        }
        finally
        {
            TestHelper.DeletePaths(srcPath, destPath);
        }
    }

    [Fact]
    public async Task When_CompareSrcToDestGptPartition1_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var comparePath = Path.Combine(destPath, "gpt", "1");

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

        // arrange - write data to dest gpt partition 1
        var gptPartition1Part = await TestHelper.GetGptPartitionPart(testCommandHelper, destPath, 1);
        await TestHelper.WriteData(testCommandHelper, destPath, gptPartition1Part.StartOffset, srcData);
        
        // arrange - create compare command to compare gpt partition 1
        var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper,
            [], srcPath, 0, comparePath, 0, new Size(0, Unit.Bytes), 0, false,
            false);

        // act - execute compare command
        var result = await compareCommand.Execute(CancellationToken.None);
        
        // assert - compare command returned success
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task When_CompareSrcWithUnusedSectorsToDestGptPartition1_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var comparePath = Path.Combine(destPath, "gpt", "1");
        const bool skipZeroFilled = true;

        // arrange - create src data with 1024 bytes of used sector data and remaining unused sectors
        var srcData = new byte[20.MB().ToSectorSize()];
        Array.Fill<byte>(srcData, 1, 0, 1024);
            
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

        // arrange - write data to dest gpt partition 1
        var gptPartition1Part = await TestHelper.GetGptPartitionPart(testCommandHelper, destPath, 1);
        await TestHelper.WriteData(testCommandHelper, destPath, gptPartition1Part.StartOffset, srcData);
        
        // arrange - create compare command to compare gpt partition 1
        var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper,
            [], srcPath, 0, comparePath, 0, new Size(0, Unit.Bytes), 0, false,
            skipZeroFilled);

        // act - execute compare command
        var result = await compareCommand.Execute(CancellationToken.None);
        
        // assert - compare command returned success
        Assert.True(result.IsSuccess);
    }
    
    [Fact]
    public async Task When_CompareSrcToDestGptPartition2_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var comparePath = Path.Combine(destPath, "gpt", "2");

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

        // arrange - write data to dest gpt partition 2
        var gptPartition1Part = await TestHelper.GetGptPartitionPart(testCommandHelper, destPath, 2);
        await TestHelper.WriteData(testCommandHelper, destPath, gptPartition1Part.StartOffset, srcData);
        
        // arrange - create compare command to compare gpt partition 2
        var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper,
            [], srcPath, 0, comparePath, 0, new Size(0, Unit.Bytes), 0, false,
            false);

        // act - execute compare command
        var result = await compareCommand.Execute(CancellationToken.None);
        
        // assert - compare command returned success
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task When_CompareSrcLargerThanDestToDestGptPartition_Then_ErrorIsReturned()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var comparePath = Path.Combine(destPath, "gpt", "1");

        // arrange - create src data
        var srcData = new byte[20.MB().ToSectorSize() * 2];
        Array.Fill<byte>(srcData, 1);

        // arrange - create test command helper
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
        
        // arrange - create compare command to compare gpt partition 1
        var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper,
            [], srcPath, 0, comparePath, 0, new Size(0, Unit.Bytes), 0, false,
            false);

        // act - execute compare command
        var result = await compareCommand.Execute(CancellationToken.None);
        
        // assert - compare command returned error
        Assert.True(result.IsFaulted);
        Assert.NotNull(result.Error);
        Assert.IsType<CompareSizeTooLargeError>(result.Error);
    }
}