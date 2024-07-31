using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenMbrPartFormatCommand : FsCommandTestBase
{
    [Fact]
    public async Task WhenAddMbrPartitionOfSize0ThenPartitionIsAddedWithRemainingDiskSize()
    {
        // arrange - path, size and test command helper
        var imgPath = $"fat16format.img";
        var testCommandHelper = new TestCommandHelper();
        var size = 256.MB();

        var cancellationTokenSource = new CancellationTokenSource();

        var blank = new BlankCommand(new NullLogger<BlankCommand>(), testCommandHelper, imgPath,
            new Size(size, Unit.Bytes), false);
        var br = await blank.Execute(cancellationTokenSource.Token);
        Assert.True(br.IsSuccess);
        
        // arrange - create img media
        //testCommandHelper.AddTestMedia(imgPath, size);

        // arrange - create mbr disk
        await CreateMbrDisk(testCommandHelper, imgPath, size);
        
        
        testCommandHelper.ClearActiveMedias();
        
        // arrange - mbr partition add command with type FAT32 and size 0
        var mbrPartAddCommand = new MbrPartAddCommand(new NullLogger<MbrPartAddCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, MbrPartType.Fat16Small, new Size(0, Unit.Bytes), null, null);

        // act - execute mbr partition add
        var result = await mbrPartAddCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);
        
        testCommandHelper.ClearActiveMedias();
        
        var mbrPartFormatCommand = new MbrPartFormatCommand(new NullLogger<MbrPartFormatCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, 1, "CBM");

        // act - execute mbr partition add
        var result2 = await mbrPartFormatCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result2.IsSuccess);

    }

    [Fact]
    public void Test()
    {
        var memoryStream = new MemoryStream(new byte[1024 * 1024]);
        var sectorStream = new SectorStream(memoryStream);

        var buffer = new byte[1024 * 512];
        var read = sectorStream.Read(buffer, 0, 512);
        Assert.Equal(512, read);
        
        read = sectorStream.Read(buffer, 0, 1024);
        Assert.Equal(1024, read);
    }
}