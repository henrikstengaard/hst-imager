namespace Hst.Imager.ConsoleApp;

using System.CommandLine;
using Hst.Imager.Core.UaeMetadatas;

public static class FsCommandFactory
{
    public static Command CreateFsCommand()
    {
        var command = new Command("fs", "File system.");

        command.AddCommand(CreateFsDir());
        command.AddCommand(CreateFsCopy());
        command.AddCommand(CreateFsExtract());

        return command;
    }
        
    private static Command CreateFsDir()
    {
        var pathArgument = new Argument<string>(
            name: "DiskPath",
            description: "Path to physical drive or image file.");

        var recursiveOption = new Option<bool>(
            new[] { "--recursive", "-r" },
            description: "Recursively list sub-directories.",
            getDefaultValue: () => false);
        
        var command = new Command("dir", "List files and subdirectories in a directory.");
        command.AddAlias("d");
        command.SetHandler(CommandHandler.FsDir, pathArgument, recursiveOption, CommandFactory.FormatOption);
        command.AddArgument(pathArgument);
        command.AddOption(recursiveOption);
        command.AddOption(CommandFactory.FormatOption);

        return command;
    }
    
    private static Command CreateFsCopy()
    {
        var sourcePathArgument = new Argument<string>(
            name: "SourcePath",
            description: "Path to source physical drive or image file.");

        var destinationPathArgument = new Argument<string>(
            name: "DestinationPath",
            description: "Path to destination physical drive or image file.");

        var recursiveOption = new Option<bool>(
            new[] { "--recursive", "-r" },
            description: "Recursively copy sub-directories.",
            getDefaultValue: () => true);

        var skipAttributesOption = new Option<bool>(
            new[] { "--skip-attributes", "-sa" },
            description: "Attributes of directories and files are not set when copied.",
            getDefaultValue: () => false);
        
        var quietOption = new Option<bool>(
            new[] { "--quiet", "-q" },
            description: "Quiet mode.",
            getDefaultValue: () => false);

        var uaeMetadataOption = new Option<UaeMetadata>(
            new[] { "--uaemetadata", "-uae" },
            description: "Type of UAE metadata to read and write.",
            getDefaultValue: () => UaeMetadata.UaeFsDb);
        
        var command = new Command("copy", "Copy files or subdirectories from source to destination.");
        command.AddAlias("c");
        command.SetHandler(CommandHandler.FsCopy, sourcePathArgument, destinationPathArgument, recursiveOption, skipAttributesOption, quietOption, uaeMetadataOption);
        command.AddArgument(sourcePathArgument);
        command.AddArgument(destinationPathArgument);
        command.AddOption(recursiveOption);
        command.AddOption(skipAttributesOption);
        command.AddOption(quietOption);
        command.AddOption(uaeMetadataOption);

        return command;
    }
    
    private static Command CreateFsExtract()
    {
        var sourcePathArgument = new Argument<string>(
            name: "SourcePath",
            description: "Source path to extract from (lha, iso, adf file).");

        var destinationPathArgument = new Argument<string>(
            name: "DestinationPath",
            description: "Destination path to extract to (physical drive, image file or directory).");

        var recursiveOption = new Option<bool>(
            new[] { "--recursive", "-r" },
            description: "Recursively extract sub-directories.",
            getDefaultValue: () => true);
        
        var skipAttributesOption = new Option<bool>(
            new[] { "--skip-attributes", "-sa" },
            description: "Attributes of directories and files are not set when copied.",
            getDefaultValue: () => false);
        
        var quietOption = new Option<bool>(
            new[] { "--quiet", "-q" },
            description: "Quiet mode.",
            getDefaultValue: () => false);

        var command = new Command("extract", "Extract files or subdirectories from source to destination.");
        command.AddAlias("x");
        command.SetHandler(CommandHandler.FsExtract, sourcePathArgument, destinationPathArgument, recursiveOption, skipAttributesOption, quietOption);
        command.AddArgument(sourcePathArgument);
        command.AddArgument(destinationPathArgument);
        command.AddOption(recursiveOption);
        command.AddOption(skipAttributesOption);
        command.AddOption(quietOption);

        return command;
    }
}