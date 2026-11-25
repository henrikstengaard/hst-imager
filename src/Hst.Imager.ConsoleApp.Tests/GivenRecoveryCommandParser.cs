using System.CommandLine;
using System.Linq;
using Hst.Amiga.RigidDiskBlocks.Recovery;
using Hst.Imager.Core.UaeMetadatas;
using Xunit;

namespace Hst.Imager.ConsoleApp.Tests;

public class GivenRecoveryCommandParser
{
    private static T GetOption<T>(ParseResult parseResult, string name) =>
        parseResult.GetValue((Option<T>)parseResult.CommandResult.Command.Options.First(x => x.Name == name));

    private static string GetArgument(ParseResult parseResult, string name) =>
        parseResult.GetValue((Argument<string>)parseResult.CommandResult.Command.Arguments.First(x => x.Name == name));

    [Theory]
    [InlineData("partitiontable")]
    [InlineData("pt")]
    public void When_ParsingPartitionTableScan_Then_PathAndTypeAreParsed(string partitionTableCommand)
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse recovery partition table scan command
        var parseResult = rootCommand.Parse(["recovery", partitionTableCommand, "scan", "disk.img", "--type", "Gpt",
            "--recovery-log", "scan.log"]);

        // assert - no errors and path, type and recovery log are parsed
        Assert.Empty(parseResult.Errors);
        Assert.Equal("scan", parseResult.CommandResult.Command.Name);
        Assert.Equal("disk.img", GetArgument(parseResult, "Path"));
        Assert.Equal(RecoveryCommandFactory.RecoveryPartitionTableType.Gpt,
            GetOption<RecoveryCommandFactory.RecoveryPartitionTableType>(parseResult, "--type"));
        Assert.Equal("scan.log", GetOption<string>(parseResult, "--recovery-log"));
    }

    [Fact]
    public void When_ParsingPartitionTableRepair_Then_ModeAndYesAreParsed()
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse recovery partition table repair command
        var parseResult = rootCommand.Parse(["recovery", "pt", "repair", "disk.img", "-m", "Rebuild", "-y"]);

        // assert - no errors and mode and yes are parsed
        Assert.Empty(parseResult.Errors);
        Assert.Equal("repair", parseResult.CommandResult.Command.Name);
        Assert.Equal(RecoveryMode.Rebuild, GetOption<RecoveryMode>(parseResult, "--mode"));
        Assert.True(GetOption<bool>(parseResult, "--yes"));
        Assert.Equal(RecoveryCommandFactory.RecoveryPartitionTableType.Auto,
            GetOption<RecoveryCommandFactory.RecoveryPartitionTableType>(parseResult, "--type"));
        Assert.Null(GetOption<string>(parseResult, "--recovery-log"));
    }

    [Fact]
    public void When_ParsingFsRepairWithoutYes_Then_YesIsFalse()
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse recovery fs repair command
        var parseResult = rootCommand.Parse(["recovery", "fs", "repair", "disk.img\\rdb\\dh0", "-rl", "repair.log"]);

        // assert - no errors and yes is false
        Assert.Empty(parseResult.Errors);
        Assert.Equal("disk.img\\rdb\\dh0", GetArgument(parseResult, "Path"));
        Assert.False(GetOption<bool>(parseResult, "--yes"));
        Assert.Equal("repair.log", GetOption<string>(parseResult, "--recovery-log"));
    }

    [Fact]
    public void When_ParsingFsSalvageWithDefaults_Then_UaeFsDbIsUsed()
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse recovery fs salvage command
        var parseResult = rootCommand.Parse(["recovery", "fs", "salvage", "disk.img\\rdb\\dh0", "salvaged"]);

        // assert - no errors, output path is parsed and uae metadata defaults to uaefsdb
        Assert.Empty(parseResult.Errors);
        Assert.Equal("salvaged", GetArgument(parseResult, "OutputPath"));
        Assert.Equal(UaeMetadata.UaeFsDb, GetOption<UaeMetadata>(parseResult, "--uaemetadata"));
        Assert.False(GetOption<bool>(parseResult, "--no-orphans"));
    }

    [Fact]
    public void When_ParsingFsSalvageWithUaeMetafile_Then_UaeMetafileIsParsed()
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse recovery fs salvage command with uae metafile and no orphans
        var parseResult = rootCommand.Parse(["recovery", "fs", "salvage", "disk.adf", "salvaged", "-uae",
            "UaeMetafile", "--no-orphans"]);

        // assert - no errors and options are parsed
        Assert.Empty(parseResult.Errors);
        Assert.Equal(UaeMetadata.UaeMetafile, GetOption<UaeMetadata>(parseResult, "--uaemetadata"));
        Assert.True(GetOption<bool>(parseResult, "--no-orphans"));
    }

    [Fact]
    public void When_ParsingFsSalvageWithoutOutputPath_Then_ErrorIsReturned()
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse recovery fs salvage command without output path
        var parseResult = rootCommand.Parse(["recovery", "fs", "salvage", "disk.img\\rdb\\dh0"]);

        // assert - errors are returned
        Assert.NotEmpty(parseResult.Errors);
    }
}
