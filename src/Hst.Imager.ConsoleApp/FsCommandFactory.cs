namespace Hst.Imager.ConsoleApp;

using System.CommandLine;

public static class FsCommandFactory
{
    public static Command CreateFsCommand()
    {
        var command = new Command("fs", "File system.");

        command.AddCommand(CreateFsDir());
        command.AddCommand(CreateFsCopy());

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
        
        var fsDirCommand = new Command("dir", "List files and subdirectories in a directory.");
        fsDirCommand.SetHandler(CommandHandler.FsDir, pathArgument, recursiveOption);
        fsDirCommand.AddArgument(pathArgument);
        fsDirCommand.AddOption(recursiveOption);

        return fsDirCommand;
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
            getDefaultValue: () => false);

        var quietOption = new Option<bool>(
            new[] { "--quiet", "-q" },
            description: "Quiet mode.",
            getDefaultValue: () => false);

        var fsDirCommand = new Command("copy", "Copy files or subdirectories from source to destination.");
        fsDirCommand.SetHandler(CommandHandler.FsCopy, sourcePathArgument, destinationPathArgument, recursiveOption, quietOption);
        fsDirCommand.AddArgument(sourcePathArgument);
        fsDirCommand.AddArgument(destinationPathArgument);
        fsDirCommand.AddOption(recursiveOption);
        fsDirCommand.AddOption(quietOption);

        return fsDirCommand;
    }
}