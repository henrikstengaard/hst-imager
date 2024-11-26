using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenMbrPartFormatCommand : FsCommandTestBase
{
    [Fact]
    public async Task WhenFatFormatMbrPartition_Then_PartitionIsFormattedAndEmpty()
    {
        // arrange - path, size and test command helper
        var imgPath = $"fat16format.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 100.MB();

        // arrange - create img media
        testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create mbr disk
        await CreateMbrDiskWithPartition(testCommandHelper, imgPath, size, BiosPartitionTypes.Fat16);

        // arrange - mbr partition format command with type volume name "TEST"
        var cancellationTokenSource = new CancellationTokenSource();
        var mbrPartFormatCommand = new MbrPartFormatCommand(new NullLogger<MbrPartFormatCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, 1, "TEST", "Fat16");

        // act - execute mbr partition format
        var result = await mbrPartFormatCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - partition is fat16 formatted
        var mediaResult = await testCommandHelper.GetReadableMedia(new List<IPhysicalDrive>(), imgPath);
        using var media = mediaResult.Value;
        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
        var biosPartitionTable = new BiosPartitionTable(disk);
        using var fatFileSystem = new FatFileSystem(biosPartitionTable.Partitions[0].Open());
        Assert.Equal(FatType.Fat16, fatFileSystem.FatVariant);
        Assert.NotEqual(0, fatFileSystem.Size);
        Assert.NotEqual(0, fatFileSystem.TotalSectors);
        Assert.Empty(fatFileSystem.GetDirectories(string.Empty));
        Assert.Empty(fatFileSystem.GetFiles(string.Empty));
    }
}