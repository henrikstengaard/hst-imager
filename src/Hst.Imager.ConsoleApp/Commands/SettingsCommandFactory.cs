using System.CommandLine;

namespace Hst.Imager.ConsoleApp.Commands;

public static class SettingsCommandFactory
{
    public static Command CreateSettingsCommand()
    {
        var command = new Command("settings", "Settings.");

        command.AddCommand(CreateSettingsListCommand());
        command.AddCommand(CreateSettingsUpdateCommand());

        return command;
    }

    public static Command CreateSettingsListCommand()
    {
        var command = new Command("list", "List settings.");
        command.SetHandler(CommandHandler.SettingsList);

        return command;
    }

    public static Command CreateSettingsUpdateCommand()
    {
        var allOption = new Option<bool?>(
            ["--all-physical-drives"],
            description: "Use all physical drives.",
            getDefaultValue: () => false);
            
        var command = new Command("update", "Update settings.");
        command.AddOption(allOption);
        command.SetHandler(CommandHandler.SettingsUpdate, allOption);

        return command;
    }
}