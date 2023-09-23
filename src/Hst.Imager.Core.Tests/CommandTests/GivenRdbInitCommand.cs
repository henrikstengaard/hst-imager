using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenRdbInitCommand : FsCommandTestBase
{
    [Fact]
    public async Task WhenInitRdbThenRdbPartitionTableExist()
    {
        // arrange - path, size and test command helper
        var imgPath = $"{Guid.NewGuid()}.img";
        var testCommandHelper = new TestCommandHelper();

        // arrange - create img media
        await testCommandHelper.AddTestMedia(imgPath, imgPath, new byte[10.MB()]);

        // arrange - rdb init command
        var cancellationTokenSource = new CancellationTokenSource();
        var rdbInitCommand = new RdbInitCommand(new NullLogger<RdbInitCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(), imgPath, "Test", new Size(), null, 0);

        // act - execute rdb init
        var result = await rdbInitCommand.Execute(cancellationTokenSource.Token);
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        await AssertRdb(testCommandHelper, imgPath);
    }
    
    private async Task AssertRdb(ICommandHelper commandHelper, string path)
    {
        // assert - read disk info
        var mediaResult = await commandHelper.GetReadableMedia(new List<IPhysicalDrive>(), path);
        Assert.True(mediaResult.IsSuccess);
        using var media = mediaResult.Value;
        var diskInfo = await commandHelper.ReadDiskInfo(media);

        // assert - disk contains rdb and not mbr or gpt
        Assert.NotNull(diskInfo?.RdbPartitionTablePart);
        Assert.Null(diskInfo.GptPartitionTablePart);
        Assert.Null(diskInfo.MbrPartitionTablePart);

        // assert - rdb doesn't contain any partitions
        Assert.Equal(0, diskInfo.RdbPartitionTablePart.Parts.Count(
            x => x.PartType == PartType.Partition));
    }
}