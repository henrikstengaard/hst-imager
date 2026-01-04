using System.CommandLine;
using Hst.Imager.Core.Models;

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
            description: "Use all physical drives.");
            
        var retriesOption = new Option<int?>(
            ["--retries", "-r"],
            description: "Number of retries to try read or write data.");

        var verifyOption = new Option<bool?>(
            ["--verify", "-v"],
            description: "Verify while reading and writing.");
            
        var forceOption = new Option<bool?>(
            ["--force", "-f"],
            description: "Force and ignore errors when retries are exceeded.");

        var skipUnusedSectorsOption = new Option<bool?>(
            ["--skip-unused-sectors"],
            description: "Skip unused sectors.");
        
        var useCacheOption = new Option<bool?>(
            ["--use-cache"],
            description: "Use cache.");

        var cacheTypeOption = new Option<CacheType?>(
            ["--cache-type"],
            description: "Type of cache to use.");
        
        var command = new Command("update", "Update settings.");
        command.AddOption(allOption);
        command.AddOption(retriesOption);
        command.AddOption(forceOption);
        command.AddOption(verifyOption);
        command.AddOption(skipUnusedSectorsOption);
        command.AddOption(useCacheOption);
        command.AddOption(cacheTypeOption);
        command.AddValidator(validate =>
        {
            if (validate.FindResultFor(allOption) is null &&
                validate.FindResultFor(retriesOption) is null &&
                validate.FindResultFor(forceOption) is null &&
                validate.FindResultFor(verifyOption) is null &&
                validate.FindResultFor(skipUnusedSectorsOption) is null &&
                validate.FindResultFor(useCacheOption) is null &&
                validate.FindResultFor(cacheTypeOption) is null)
            {
                validate.ErrorMessage = "At least one option must be specified";
            }
        });
        command.SetHandler(CommandHandler.SettingsUpdate, allOption, retriesOption, forceOption,
            verifyOption, skipUnusedSectorsOption, useCacheOption, cacheTypeOption);

        return command;
    }
}