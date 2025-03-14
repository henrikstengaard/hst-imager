using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenReadCommandWithMbr : CommandTestBase
{
    [Fact]
    public async Task When_ReadSrcMbrPartition1ToDest_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";

        // arrange - create mbr partition 1 and 2 data
        var mbrPartition1Data = new byte[20.MB().ToSectorSize()];
        Array.Fill<byte>(mbrPartition1Data, 1);
        var mbrPartition2Data = new byte[40.MB().ToSectorSize()];
        Array.Fill<byte>(mbrPartition2Data, 2);
            
        // arrange - create read path and test command helper
        var readPath = Path.Combine(srcPath, "mbr", "1");
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        testCommandHelper.AddTestMedia(srcPath, 100.MB().ToSectorSize());
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src mbr disk with 2 partitions
        await CreateMbrDisk(testCommandHelper, srcPath, 100.MB().ToSectorSize());
        await AddMbrDiskPartition(testCommandHelper, srcPath, data: mbrPartition1Data);
        await AddMbrDiskPartition(testCommandHelper, srcPath, data: mbrPartition2Data);

        // arrange - create read command to read mbr partition 2
        var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
            [], readPath, destPath, new Size(0, Unit.Bytes), 0, false,
            false, 0);

        // act - execute read command
        var result = await readCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - data read is identical to mbr partition 1 data
        var destBytes = await testCommandHelper.ReadMediaData(destPath);
        Assert.Equal(mbrPartition1Data.Length, destBytes.Length);
        Assert.Equal(mbrPartition1Data, destBytes);
    }

    [Fact]
    public async Task When_ReadSrcMbrPartition2ToDest_Then_DataIsIdentical()
    {
        // arrange - create src and dest paths
        var srcPath = $"src-{Guid.NewGuid()}.vhd";
        var destPath = $"dest-{Guid.NewGuid()}.vhd";

        // arrange - create mbr partition 1 and 2 data
        var mbrPartition1Data = new byte[20.MB().ToSectorSize()];
        Array.Fill<byte>(mbrPartition1Data, 1);
        var mbrPartition2Data = new byte[40.MB().ToSectorSize()];
        Array.Fill<byte>(mbrPartition2Data, 2);
            
        // arrange - create read path and test command helper
        var readPath = Path.Combine(srcPath, "mbr", "2");
        var testCommandHelper = new TestCommandHelper();

        // arrange - create src and dest medias
        testCommandHelper.AddTestMedia(srcPath, 100.MB().ToSectorSize());
        await testCommandHelper.AddTestMedia(destPath, destPath);

        // arrange - create src mbr disk with 2 partitions
        await CreateMbrDisk(testCommandHelper, srcPath, 100.MB().ToSectorSize());
        await AddMbrDiskPartition(testCommandHelper, srcPath, data: mbrPartition1Data);
        await AddMbrDiskPartition(testCommandHelper, srcPath, data: mbrPartition2Data);

        // arrange - create read command to read mbr partition 2
        var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
            [], readPath, destPath, new Size(0, Unit.Bytes), 0, false,
            false, 0);

        // act - execute read command
        var result = await readCommand.Execute(CancellationToken.None);
        Assert.True(result.IsSuccess);

        // assert - data read is identical to mbr partition 2 data
        var destBytes = await testCommandHelper.ReadMediaData(destPath);
        Assert.Equal(mbrPartition2Data.Length, destBytes.Length);
        Assert.Equal(mbrPartition2Data, destBytes);
    }
        
    private async Task CreateMbrDisk(TestCommandHelper testCommandHelper, string path,
        long diskSize)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path, size: diskSize, create: true);
        using var media = mediaResult.Value;
        var stream = media.Stream;

        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new DiscUtils.Raw.Disk(stream, Ownership.None);
        BiosPartitionTable.Initialize(disk);
    }

    private async Task AddMbrDiskPartition(TestCommandHelper testCommandHelper, string path,
        long partitionSize = 0, byte[] data = null)
    {
        if (partitionSize == 0 && data == null)
        {
            throw new ArgumentException("Partition size or data must be provided");
        }
            
        var dataSize = data?.Length ?? 0;
            
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
        using var media = mediaResult.Value;
        var stream = media.Stream;
            
        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new DiscUtils.Raw.Disk(stream, Ownership.None);
        var biosPartitionTable = new BiosPartitionTable(disk);

        var size = partitionSize > 0 ? partitionSize : dataSize; 
        var sectors = Convert.ToInt64(Math.Ceiling((double)size / 512));
            
        var startSector = biosPartitionTable.Partitions.Count == 0
            ? 1
            : biosPartitionTable.Partitions.Max(x => x.LastSector) + 1;
        var endSector = startSector + sectors - 1;
            
        var partitionIndex = biosPartitionTable.CreatePrimaryBySector(startSector, endSector,
            BiosPartitionTypes.Fat32Lba, biosPartitionTable.Partitions.Count == 0);

        if (data == null || data.Length == 0)
        {
            return;
        }
            
        var partition = biosPartitionTable.Partitions[partitionIndex];

        await using var partitionStream = partition.Open();

        partitionStream.Position = 0;
        await partitionStream.WriteAsync(data.AsMemory(0, (int)size));
    }
}