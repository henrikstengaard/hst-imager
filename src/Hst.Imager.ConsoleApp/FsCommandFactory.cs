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

        var fsPathArgument = new Argument<string>(
            name: "FileSystemPath",
            description: "Path in file system.")
        {
            Arity = ArgumentArity.ZeroOrOne
        };
            
        var fsDirCommand = new Command("dir", "Displays a list of files and subdirectories in a directory.");
        fsDirCommand.SetHandler(CommandHandler.FsDir, pathArgument, fsPathArgument);
        fsDirCommand.AddArgument(pathArgument);
        fsDirCommand.AddArgument(fsPathArgument);

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
        
        var fsDirCommand = new Command("copy", "Copy files or subdirectories from source to destination.");
        fsDirCommand.SetHandler(CommandHandler.FsCopy, sourcePathArgument, destinationPathArgument, recursiveOption);
        fsDirCommand.AddArgument(sourcePathArgument);
        fsDirCommand.AddArgument(destinationPathArgument);
        fsDirCommand.AddOption(recursiveOption);

        return fsDirCommand;
    }
}