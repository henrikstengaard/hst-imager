using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsDirCommandWithLocalDirectory
{
    [Fact]
    public async Task When_ListingEntriesInNonExistingDirectory_Then_ErrorIsReturned()
    {
        // arrange - paths
        var dirPath = $"local_{Guid.NewGuid()}";
        const bool recursive = false;

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();
        
        // arrange - create fs dir command
        var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(),
            dirPath, recursive);

        // act - execute fs dir command
        var result = await fsDirCommand.Execute(CancellationToken.None);
        
        // assert - result is faulted
        Assert.NotNull(result);
        Assert.True(result.IsFaulted);
    }
}