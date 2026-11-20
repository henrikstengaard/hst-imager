using System.CommandLine;
using System.Linq;

namespace Hst.Imager.ConsoleApp.Tests;

public class GivenOptimizeCommandParser
{
    [Theory]
    [InlineData("--compact")]
    [InlineData("-c")]
    public void When_ParsingCompactOption_Then_CompactIsTrue(string option)
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse optimize command with compact option
        var parseResult = rootCommand.Parse(["optimize", "disk.img", option]);

        // assert - no errors, compact is true and file systems is false
        Assert.Empty(parseResult.Errors);
        Assert.True(GetBoolOption(parseResult, "--compact"));
        Assert.False(GetBoolOption(parseResult, "--file-systems"));
    }

    [Theory]
    [InlineData("--file-systems")]
    [InlineData("-fs")]
    public void When_ParsingFileSystemsOption_Then_FileSystemsIsTrue(string option)
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse optimize command with file systems option
        var parseResult = rootCommand.Parse(["optimize", "disk.img", option]);

        // assert - no errors and file systems is true
        Assert.Empty(parseResult.Errors);
        Assert.True(GetBoolOption(parseResult, "--file-systems"));
    }

    [Fact]
    public void When_ParsingOptimizeWithoutOptions_Then_CompactAndFileSystemsAreFalse()
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse optimize command without options
        var parseResult = rootCommand.Parse(["optimize", "disk.img"]);

        // assert - no errors, compact and file systems are false
        Assert.Empty(parseResult.Errors);
        Assert.False(GetBoolOption(parseResult, "--compact"));
        Assert.False(GetBoolOption(parseResult, "--file-systems"));
    }

    [Fact]
    public void When_ParsingCompactWithPartitionTableAndFileSystems_Then_AllOptionsAreParsed()
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse optimize command with partition table, compact and file systems options
        var parseResult = rootCommand.Parse(["optimize", "16gb.img", "-pt", "rdb", "-c", "-fs"]);

        // assert - no errors and all options are parsed
        Assert.Empty(parseResult.Errors);
        Assert.True(GetBoolOption(parseResult, "--compact"));
        Assert.True(GetBoolOption(parseResult, "--file-systems"));
        var partitionTableOption = (Option<Core.Models.PartitionTable?>)parseResult.CommandResult.Command.Options
            .First(x => x.Name == "--partition-table");
        Assert.Equal(Core.Models.PartitionTable.Rdb, parseResult.GetValue(partitionTableOption));
    }

    private static bool GetBoolOption(ParseResult parseResult, string name)
    {
        var option = (Option<bool>)parseResult.CommandResult.Command.Options.First(x => x.Name == name);
        return parseResult.GetValue(option);
    }
}
