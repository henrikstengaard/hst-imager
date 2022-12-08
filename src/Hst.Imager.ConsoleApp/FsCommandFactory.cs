namespace Hst.Imager.ConsoleApp;

using System.CommandLine;

public static class FsCommandFactory
{
    public static Command CreateFsCommand()
    {
        var command = new Command("fs", "File system.");

        command.AddCommand(CreateFsDir());

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
}