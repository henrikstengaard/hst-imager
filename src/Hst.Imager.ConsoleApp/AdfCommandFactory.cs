namespace Hst.Imager.ConsoleApp;

using System.CommandLine;
using System.CommandLine.Parsing;

public static class AdfCommandFactory
{
    public static Command CreateAdfCommand()
    {
        var command = new Command("adf", "Amiga disk file.");
        command.Add(CreateAdfCreate());

        return command;
    }

    private static Command CreateAdfCreate()
    {
        var adfPathArgument = new Argument<string>("AdfPath")
        {
            Description = "Path to ADF file."
        };

        var formatOption = new Option<bool>("--format", ["-f"])
        {
            Description = "Format ADF.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var dosTypeOption = new Option<string>("--dos-type", ["-dt"])
        {
            Description = "DOS type for the ADF to use (e.g. DOS3)."
        };

        var nameOption = new Option<string>("--name", ["-n"])
        {
            Description = "Name of the disk."
        };

        var bootableOption = new Option<bool>("--bootable", ["-b"])
        {
            Description = "Set bootable.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var adfCreateCommand = new Command("create", "Create ADF disk image file.");
        adfCreateCommand.SetAction((ParseResult ctx) =>
        {
            var adfPath = ctx.GetValue(adfPathArgument);
            var format = ctx.GetValue(formatOption);
            var name = ctx.GetValue(nameOption);
            var dosType = ctx.GetValue(dosTypeOption);
            var bootable = ctx.GetValue(bootableOption);
            return CommandHandler.AdfCreate(adfPath, format, name, dosType, bootable);
        });
        adfCreateCommand.Add(adfPathArgument);
        adfCreateCommand.Add(formatOption);
        adfCreateCommand.Add(nameOption);
        adfCreateCommand.Add(dosTypeOption);
        adfCreateCommand.Add(bootableOption);

        return adfCreateCommand;
    }
}