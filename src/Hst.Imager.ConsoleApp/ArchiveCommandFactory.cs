namespace Hst.Imager.ConsoleApp;

using System.CommandLine;

public static class ArchiveCommandFactory
{
    public static Command CreateArchiveCommand()
    {
        var command = new Command("archive", "Archive.");
        command.AddAlias("arc");
        command.AddCommand(CreateArcList());

        return command;
    }
        
    private static Command CreateArcList()
    {
        var archivePathArgument = new Argument<string>(
            name: "ArchivePath",
            description: "Path to archive file.");

        var recursiveOption = new Option<bool>(
            new[] { "--recursive", "-r" },
            description: "Recursively list sub-directories.",
            getDefaultValue: () => false);
        
        var command = new Command("list", "List files and subdirectories in archive.");
        command.AddAlias("l");
        command.SetHandler(CommandHandler.ArcList, archivePathArgument, recursiveOption);
        command.AddArgument(archivePathArgument);
        command.AddOption(recursiveOption);

        return command;
    }
    

}