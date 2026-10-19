using System.CommandLine;
using System.Linq;
using Hst.Imager.Core.UaeMetadatas;

namespace Hst.Imager.ConsoleApp.Tests;

public class GivenFsAttribCommandParser
{
    [Theory]
    [InlineData("+s")]
    [InlineData("-d")]
    [InlineData("-r")]
    [InlineData("-a")]
    [InlineData("-rw")]
    [InlineData("-wd")]
    [InlineData("+rh")]
    [InlineData("rwed")]
    [InlineData("755")]
    public void When_ParsingAttribute_Then_AttributeIsParsedAsArgument(string attribute)
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse fs attrib command with attribute
        var parseResult = rootCommand.Parse(["fs", "attrib", "disk.img", attribute]);

        // assert - no errors and attribute is parsed as argument
        Assert.Empty(parseResult.Errors);
        var attributesArgument = parseResult.CommandResult.Command.Arguments.First(x => x.Name == "Attributes");
        Assert.Equal([attribute], parseResult.GetValue((Argument<string[]>)attributesArgument));
    }

    [Fact]
    public void When_ParsingRemoveHiddenAttribute_Then_HelpIsNotShown()
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse fs attrib command with remove hidden attribute
        var parseResult = rootCommand.Parse(["fs", "attrib", "disk.img", "-h", "+s"]);

        // assert - no errors, help is not shown and remove hidden option is parsed
        Assert.Empty(parseResult.Errors);
        Assert.IsNotType<System.CommandLine.Help.HelpAction>(parseResult.Action);
        var command = parseResult.CommandResult.Command;
        var attributesArgument = (Argument<string[]>)command.Arguments.First(x => x.Name == "Attributes");
        var removeHiddenOption = (Option<bool>)command.Options.First(x => x.Name == "-h");
        Assert.Equal(["+s"], parseResult.GetValue(attributesArgument));
        Assert.True(parseResult.GetValue(removeHiddenOption));
    }

    [Fact]
    public void When_ParsingMultipleAttributesAndRecursive_Then_AttributesAndRecursiveAreParsed()
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse fs attrib command with multiple attributes and recursive option
        var parseResult = rootCommand.Parse(["fs", "attrib", "disk.img", "+s", "-d", "-r", "--recursive"]);

        // assert - no errors, attributes and recursive are parsed
        Assert.Empty(parseResult.Errors);
        var command = parseResult.CommandResult.Command;
        var attributesArgument = (Argument<string[]>)command.Arguments.First(x => x.Name == "Attributes");
        var recursiveOption = (Option<bool>)command.Options.First(x => x.Name == "--recursive");
        Assert.Equal(["+s", "-d", "-r"], parseResult.GetValue(attributesArgument));
        Assert.True(parseResult.GetValue(recursiveOption));
    }

    [Fact]
    public void When_ParsingWithoutUaeMetadata_Then_UaeMetadataIsNone()
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse fs attrib command without uae metadata option
        var parseResult = rootCommand.Parse(["fs", "attrib", "dh0", "+s"]);

        // assert - no errors and uae metadata is none
        Assert.Empty(parseResult.Errors);
        var uaeMetadataOption = (Option<UaeMetadata>)parseResult.CommandResult.Command.Options
            .First(x => x.Name == "--uaemetadata");
        Assert.Equal(UaeMetadata.None, parseResult.GetValue(uaeMetadataOption));
    }

    [Fact]
    public void When_ParsingUaeMetadata_Then_UaeMetadataIsParsed()
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse fs attrib command with uae metadata option
        var parseResult = rootCommand.Parse(["fs", "attrib", "dh0", "+s", "--uaemetadata", "UaeMetafile"]);

        // assert - no errors and uae metadata is parsed
        Assert.Empty(parseResult.Errors);
        var uaeMetadataOption = (Option<UaeMetadata>)parseResult.CommandResult.Command.Options
            .First(x => x.Name == "--uaemetadata");
        Assert.Equal(UaeMetadata.UaeMetafile, parseResult.GetValue(uaeMetadataOption));
    }
}
