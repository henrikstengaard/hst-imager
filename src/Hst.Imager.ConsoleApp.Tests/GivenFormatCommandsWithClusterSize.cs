using System.CommandLine;
using System.Linq;

namespace Hst.Imager.ConsoleApp.Tests;

public class GivenFormatCommandsWithClusterSize
{
    [Theory]
    [InlineData("format", "disk.img", "gpt", "exfat")]
    [InlineData("gpt", "part", "format", "disk.img", "1", "exfat", "Empty")]
    [InlineData("mbr", "part", "format", "disk.img", "1", "Empty", "--file-system", "exfat")]
    public void When_ParsingClusterSizeOption_Then_ClusterSizeIsParsed(params string[] args)
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse format command with cluster size option
        var parseResult = rootCommand.Parse(args.Concat(["--cluster-size", "64kb"]).ToArray());

        // assert - no errors and cluster size option is parsed
        Assert.Empty(parseResult.Errors);
        var clusterSizeOption = (Option<string>)parseResult.CommandResult.Command.Options
            .First(x => x.Name == "--cluster-size");
        Assert.Equal("64kb", parseResult.GetValue(clusterSizeOption));
    }

    [Fact]
    public void When_ParsingMbrPartFormatWithFileSystemOption_Then_FileSystemIsParsed()
    {
        // arrange - create root command
        var rootCommand = CommandFactory.CreateRootCommand();

        // act - parse mbr part format command with file system option
        var parseResult = rootCommand.Parse(["mbr", "part", "format", "disk.img", "1", "Empty", "-fs", "fat32"]);

        // assert - no errors and file system option is parsed
        Assert.Empty(parseResult.Errors);
        var fileSystemOption = (Option<string>)parseResult.CommandResult.Command.Options
            .First(x => x.Name == "--file-system");
        Assert.Equal("fat32", parseResult.GetValue(fileSystemOption));
    }
}
