namespace Hst.Imager.ConsoleApp;

using System.CommandLine;

public static class AdfCommandFactory
{
    public static Command CreateAdfCommand()
    {
        var command = new Command("adf", "Amiga disk file.");

        command.AddCommand(CreateAdfCreate());

        return command;
    }
        
    private static Command CreateAdfCreate()
    {
        var adfPathArgument = new Argument<string>(
            name: "AdfPath",
            description: "Path to ADF file.");

        var formatOption = new Option<bool>(
            new[] { "--format", "-f" },
            description: "Format ADF.",
            getDefaultValue: () => false);

        var dosTypeOption = new Option<string>(
            new[] { "--dos-type", "-dt" },
            description: "DOS type for the ADF to use (e.g. DOS3).");

        var nameOption = new Option<string>(
            new[] { "--name", "-n" },
            description: "Name of the disk.");
        
        var bootableOption = new Option<bool>(
            new[] { "--bootable", "-b" },
            description: "Set bootable.",
            getDefaultValue: () => false);
        
        var adfCreateCommand = new Command("create", "Create ADF disk image file.");
        adfCreateCommand.SetHandler(CommandHandler.AdfCreate, adfPathArgument, formatOption, nameOption, dosTypeOption, bootableOption);
        adfCreateCommand.AddArgument(adfPathArgument);
        adfCreateCommand.AddOption(formatOption);
        adfCreateCommand.AddOption(nameOption);
        adfCreateCommand.AddOption(dosTypeOption);
        adfCreateCommand.AddOption(bootableOption);

        return adfCreateCommand;
    }
}