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

public class GivenCompareCommandWithRdb : CommandTestBase
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_CompareSrcToDestRdbDisk_Then_DataIsIdentical(bool skipZeroFilled)
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        
        // arrange - create write path and test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        await testCommandHelper.AddTestMedia(srcPath, srcPath);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src rdb disk with 2 partitions
        await TestHelper.CreateRdbDisk(testCommandHelper, srcPath, 100.MB().ToSectorSize());
        await TestHelper.AddRdbDiskPartition(testCommandHelper, srcPath, 20.MB().ToSectorSize());
        await TestHelper.AddRdbDiskPartition(testCommandHelper, srcPath, 40.MB().ToSectorSize());

        // arrange - create dest rdb disk cloned from src rdb disk
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

            // arrange - create src rdb disk with 2 partitions
            await TestHelper.CreateRdbDisk(testCommandHelper, srcPath, 100.MB().ToSectorSize());
            await TestHelper.AddRdbDiskPartition(testCommandHelper, srcPath, 20.MB().ToSectorSize());
            await TestHelper.AddRdbDiskPartition(testCommandHelper, srcPath, 40.MB().ToSectorSize());

            // arrange - create zip compressed dest rdb disk cloned from src rdb disk
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

            // arrange - create dest rdb disk with 2 partitions
            await TestHelper.CreateRdbDisk(testCommandHelper, destPath, 100.MB().ToSectorSize());
            await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, 20.MB().ToSectorSize());
            await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, 40.MB().ToSectorSize());

            // arrange - create zip compressed src rdb disk cloned from dest rdb disk
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
    public async Task When_CompareSrcToDestRdbPartition1_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var comparePath = Path.Combine(destPath, "rdb", "1");

        // arrange - create src data
        var cylinderSize = 16 * 63 * 512;
        var srcData = new byte[20.MB() + cylinderSize - (20.MB() % cylinderSize)];
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
        var destPartition1Size = 20.MB() + cylinderSize - (20.MB() % cylinderSize);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, destPartition1Size);
        var destPartition2Size = 40.MB() + cylinderSize - (40.MB() % cylinderSize);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, destPartition2Size);

        // arrange - write data to dest rdb partition 1
        var rdbPartition1Part = await TestHelper.GetRdbPartitionPart(testCommandHelper, destPath, 1);
        await TestHelper.WriteData(testCommandHelper, destPath, rdbPartition1Part.StartOffset, srcData);
        
        // arrange - create compare command to compare rdb partition 1
        var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper,
            [], srcPath, 0, comparePath, 0, new Size(0, Unit.Bytes), 0, false,
            false);

        // act - execute compare command
        var result = await compareCommand.Execute(CancellationToken.None);
        
        // assert - compare command returned success
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task When_CompareSrcWithUnusedSectorsToDestRdbPartition1_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var comparePath = Path.Combine(destPath, "rdb", "1");
        const bool skipZeroFilled = true;

        // arrange - create src data with 1024 bytes of used sector data and remaining unused sectors
        var cylinderSize = 16 * 63 * 512;
        var srcData = new byte[20.MB() + cylinderSize - (20.MB() % cylinderSize)];
        Array.Fill<byte>(srcData, 1, 0, 1024);
            
        // arrange - create write path and test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        await testCommandHelper.AddTestMedia(srcPath, srcPath);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src disk with data
        await TestHelper.WriteData(testCommandHelper, srcPath, 0, srcData);
        
        // arrange - create dest rdb disk with 2 partitions
        await TestHelper.CreateRdbDisk(testCommandHelper, destPath, 100.MB().ToSectorSize());
        var destPartition1Size = 20.MB() + cylinderSize - (20.MB() % cylinderSize);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, destPartition1Size);
        var destPartition2Size = 40.MB() + cylinderSize - (40.MB() % cylinderSize);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, destPartition2Size);

        // arrange - write data to dest rdb partition 1
        var rdbPartition1Part = await TestHelper.GetRdbPartitionPart(testCommandHelper, destPath, 1);
        await TestHelper.WriteData(testCommandHelper, destPath, rdbPartition1Part.StartOffset, srcData);
        
        // arrange - create compare command to compare rdb partition 1
        var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper,
            [], srcPath, 0, comparePath, 0, new Size(0, Unit.Bytes), 0, false,
            skipZeroFilled);

        // act - execute compare command
        var result = await compareCommand.Execute(CancellationToken.None);
        
        // assert - compare command returned success
        Assert.True(result.IsSuccess);
    }
    
    [Fact]
    public async Task When_CompareSrcToDestRdbPartition2_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var comparePath = Path.Combine(destPath, "rdb", "2");

        // arrange - create src data
        var cylinderSize = 16 * 63 * 512;
        var srcData = new byte[40.MB() + cylinderSize - (40.MB() % cylinderSize)];
        Array.Fill<byte>(srcData, 2);
            
        // arrange - create write path and test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        await testCommandHelper.AddTestMedia(srcPath, srcPath);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src disk with data
        await TestHelper.WriteData(testCommandHelper, srcPath, 0, srcData);
        
        // arrange - create dest rdb disk with 2 partitions
        await TestHelper.CreateRdbDisk(testCommandHelper, destPath, 100.MB().ToSectorSize());
        var destPartition1Size = 20.MB() + cylinderSize - (20.MB() % cylinderSize);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, destPartition1Size);
        var destPartition2Size = 40.MB() + cylinderSize - (40.MB() % cylinderSize);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, destPartition2Size);

        // arrange - write data to dest rdb partition 2
        var rdbPartition1Part = await TestHelper.GetRdbPartitionPart(testCommandHelper, destPath, 2);
        await TestHelper.WriteData(testCommandHelper, destPath, rdbPartition1Part.StartOffset, srcData);
        
        // arrange - create compare command to compare rdb partition 2
        var compareCommand = new CompareCommand(new NullLogger<CompareCommand>(), testCommandHelper,
            [], srcPath, 0, comparePath, 0, new Size(0, Unit.Bytes), 0, false,
            false);

        // act - execute compare command
        var result = await compareCommand.Execute(CancellationToken.None);
        
        // assert - compare command returned success
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task When_CompareSrcLargerThanDestToDestRdbPartition_Then_ErrorIsReturned()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";
        var comparePath = Path.Combine(destPath, "rdb", "1");

        // arrange - create src data
        var cylinderSize = 16 * 63 * 512;
        var srcData = new byte[40.MB() + cylinderSize - (40.MB() % cylinderSize)];
        Array.Fill<byte>(srcData, 1);

        // arrange - create test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        await testCommandHelper.AddTestMedia(srcPath, srcPath);
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src disk with data
        await TestHelper.WriteData(testCommandHelper, srcPath, 0, srcData);
        
        // arrange - create dest rdb disk with 2 partitions
        await TestHelper.CreateRdbDisk(testCommandHelper, destPath, 100.MB().ToSectorSize());
        var destPartition1Size = 20.MB() + cylinderSize - (20.MB() % cylinderSize);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, destPartition1Size);
        var destPartition2Size = 40.MB() + cylinderSize - (40.MB() % cylinderSize);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, destPartition2Size);
        
        // arrange - create compare command to compare rdb partition 1
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