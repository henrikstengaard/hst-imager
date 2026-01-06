using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsDirCommandWithRdbPfs3FormattedDisk
{
    [Fact]
    public async Task When_ListingEntriesInNonExistingDirectory_Then_ErrorIsReturned()
    {
        // arrange - paths
        var mediaPath = $"rdb_{Guid.NewGuid()}.vhd";
        var dirPath = Path.Combine(mediaPath, "rdb", "1", Guid.NewGuid().ToString());
        const bool recursive = false;

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        // arrange - create rdb disk
        await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, mediaPath);

        // arrange - create fs dir command
        var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(),
            dirPath, recursive);

        // act - execute fs dir command
        var result = await fsDirCommand.Execute(CancellationToken.None);
        
        // assert - result is faulted with path not found error
        Assert.NotNull(result);
        Assert.True(result.IsFaulted); 
        Assert.IsType<PathNotFoundError>(result.Error);
    }
}