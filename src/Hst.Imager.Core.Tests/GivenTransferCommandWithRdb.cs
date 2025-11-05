using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenTransferCommandWithRdb
{
    [Fact]
    public async Task When_TransferExportingRdbPart2_Then_DataTransferredIsIdentical()
    {
        // arrange - paths
        var srcPath = $"{Guid.NewGuid()}.img";
        var destPath = $"{Guid.NewGuid()}.img";
        var srcExportPath = Path.Combine(srcPath, "rdb", "2");

        // arrange - create partition 1 data
        var cylinderSize = 16 * 63 * 512;
        var part1Size = 30.MB() + cylinderSize - 30.MB() % cylinderSize;
        var part1Data = new byte[part1Size];
        Array.Fill<byte>(part1Data, 1);

        // arrange - create partition 2 data
        var part2Size = 60.MB() + cylinderSize - 60.MB() % cylinderSize;
        var part2Data = new byte[part2Size];
        Array.Fill<byte>(part2Data, 2);
            
        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();
            
        // arrange - create src rdb disk with 2 partitions
        await testCommandHelper.CreateTestMedia(srcPath, 100.MB());
        await TestHelper.CreateRdbDisk(testCommandHelper, srcPath, 100.MB());
        await TestHelper.AddRdbDiskPartition(testCommandHelper, srcPath, part1Size, part1Data);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, srcPath, part2Size, part2Data);

        // arrange - create dest empty
        await testCommandHelper.AddTestMedia(destPath);
            
        // act - transfer src to dest exporting rdb partition 2
        var convertCommand = new TransferCommand(testCommandHelper, srcExportPath,
            destPath, new Size(0, Unit.Bytes), false, 0, 0);
        var result = await convertCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);
            
        // arrange - get dest bytes
        var destBytes = (await testCommandHelper.GetTestMedia(destPath).ReadData()).ToArray();

        // assert - dest is identical to part 2 data
        Assert.Equal(part2Data, destBytes);
    }

    [Fact]
    public async Task When_TransferExportingRdbPart2WithSize_Then_DataTransferredIsIdentical()
    {
        // arrange - paths
        var srcPath = $"{Guid.NewGuid()}.img";
        var destPath = $"{Guid.NewGuid()}.img";
        var srcExportPath = Path.Combine(srcPath, "rdb", "2");
        var size = 20.MB();

        // arrange - create partition 1 data
        var cylinderSize = 16 * 63 * 512;
        var part1Size = 30.MB() + cylinderSize - 30.MB() % cylinderSize;
        var part1Data = new byte[part1Size];
        Array.Fill<byte>(part1Data, 1);

        // arrange - create partition 2 data
        var part2Size = 60.MB() + cylinderSize - 60.MB() % cylinderSize;
        var part2Data = new byte[part2Size];
        Array.Fill<byte>(part2Data, 2);
            
        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();
            
        // arrange - create src rdb disk with 2 partitions
        await testCommandHelper.CreateTestMedia(srcPath, 100.MB());
        await TestHelper.CreateRdbDisk(testCommandHelper, srcPath, 100.MB());
        await TestHelper.AddRdbDiskPartition(testCommandHelper, srcPath, part1Size, part1Data);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, srcPath, part2Size, part2Data);

        // arrange - create dest empty
        await testCommandHelper.AddTestMedia(destPath);
            
        // act - transfer src to dest exporting rdb partition 2
        var convertCommand = new TransferCommand(testCommandHelper, srcExportPath,
            destPath, new Size(0, Unit.Bytes), false, 0, 0);
        var result = await convertCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);
            
        // arrange - get dest bytes
        var destBytes = (await testCommandHelper.GetTestMedia(destPath).ReadData()).ToArray();

        // assert - dest is identical to part 2 data
        Assert.Equal(part2Data, destBytes);
    }
    
    [Fact]
    public async Task When_TransferImportingToRdbPart2_Then_DataTransferredIsIdentical()
    {
        // arrange - paths
        var srcPath = $"{Guid.NewGuid()}.img";
        var destPath = $"{Guid.NewGuid()}.img";
        var destImportPath = Path.Combine(destPath, "rdb", "2");

        // arrange - calculate partition sizes
        var cylinderSize = 16 * 63 * 512;
        var part1Size = 30.MB() + cylinderSize - 30.MB() % cylinderSize;
        var part2Size = 60.MB() + cylinderSize - 60.MB() % cylinderSize;
            
        // arrange - create src data
        var srcData = new byte[40.MB()];
        Array.Fill<byte>(srcData, 1);
        
        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();
            
        // arrange - create src media with img data
        await testCommandHelper.CreateTestMedia(srcPath, srcData.Length, srcData);
            
        // arrange - create dest media with rdb disk with 2 partitions
        await testCommandHelper.CreateTestMedia(destPath, 100.MB());
        await TestHelper.CreateRdbDisk(testCommandHelper, destPath, 100.MB());
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, part1Size);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, part2Size);

        // act - transfer import src to dest rdb partition 2
        var convertCommand = new TransferCommand(testCommandHelper, srcPath,
            destImportPath, new Size(0, Unit.Bytes), false, 0, 0);
        var result = await convertCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);
 
        // arrange - get disk info from media
        var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), destPath);
        using var diskMedia = diskMediaResult.Value;
        var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);
        
        // assert - disk info is not null and rdb partition table exists
        Assert.NotNull(diskInfo);
        Assert.NotNull(diskInfo.RdbPartitionTablePart);

        // assert - get part 2 info
        var part2Info = diskInfo.RdbPartitionTablePart.Parts.FirstOrDefault(x => x.PartitionNumber == 2);
        Assert.NotNull(part2Info);
        
        // arrange - get dest rdb partition 2 bytes
        var destBytes = (await TestHelper.ReadMediaBytes(testCommandHelper, destPath))
            .Skip((int)part2Info.StartOffset).Take((int)part2Info.Size).ToArray();

        // assert - dest is identical to part 2 data
        var expectedDestBytes = new byte[part2Info.Size];
        Array.Copy(srcData, 0, expectedDestBytes, 0, srcData.Length);
        Assert.Equal(expectedDestBytes.Length, destBytes.Length);
        Assert.Equal(expectedDestBytes, destBytes);
    }

    [Fact]
    public async Task When_TransferImportingToRdbPart2WithSize_Then_DataTransferredIsIdentical()
    {
        // arrange - paths
        var srcPath = $"{Guid.NewGuid()}.img";
        var destPath = $"{Guid.NewGuid()}.img";
        var destImportPath = Path.Combine(destPath, "rdb", "2");
        var size = 20.MB();

        // arrange - calculate partition sizes
        var cylinderSize = 16 * 63 * 512;
        var part1Size = 30.MB() + cylinderSize - 30.MB() % cylinderSize;
        var part2Size = 60.MB() + cylinderSize - 60.MB() % cylinderSize;
            
        // arrange - create src data
        var srcData = new byte[40.MB()];
        Array.Fill<byte>(srcData, 1);
        
        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();
            
        // arrange - create src media with img data
        await testCommandHelper.CreateTestMedia(srcPath, srcData.Length, srcData);
            
        // arrange - create dest media with rdb disk with 2 partitions
        await testCommandHelper.CreateTestMedia(destPath, 100.MB());
        await TestHelper.CreateRdbDisk(testCommandHelper, destPath, 100.MB());
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, part1Size);
        await TestHelper.AddRdbDiskPartition(testCommandHelper, destPath, part2Size);

        // act - transfer import src to dest rdb partition 2
        var convertCommand = new TransferCommand(testCommandHelper, srcPath,
            destImportPath, new Size(size, Unit.Bytes), false, 0, 0);
        var result = await convertCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);
 
        // arrange - get disk info from media
        var diskMediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), destPath);
        using var diskMedia = diskMediaResult.Value;
        var diskInfo = await testCommandHelper.ReadDiskInfo(diskMedia);
        
        // assert - disk info is not null and rdb partition table exists
        Assert.NotNull(diskInfo);
        Assert.NotNull(diskInfo.RdbPartitionTablePart);

        // assert - get part 2 info
        var part2Info = diskInfo.RdbPartitionTablePart.Parts.FirstOrDefault(x => x.PartitionNumber == 2);
        Assert.NotNull(part2Info);
        
        // arrange - get dest rdb partition 2 bytes
        var destBytes = (await TestHelper.ReadMediaBytes(testCommandHelper, destPath))
            .Skip((int)part2Info.StartOffset).Take((int)size).ToArray();

        // assert - dest is identical to part 2 data
        var expectedDestBytes = new byte[size];
        Array.Copy(srcData, 0, expectedDestBytes, 0, size);
        Assert.Equal(expectedDestBytes.Length, destBytes.Length);
        Assert.Equal(expectedDestBytes, destBytes);
    }
}