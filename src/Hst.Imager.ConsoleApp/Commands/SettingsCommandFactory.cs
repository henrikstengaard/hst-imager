using System.CommandLine;
using System.CommandLine.Parsing;
using Hst.Imager.Core.Models;

namespace Hst.Imager.ConsoleApp.Commands;

public static class SettingsCommandFactory
{
    public static Command CreateSettingsCommand()
    {
        var command = new Command("settings", "Settings.");

        command.Add(CreateSettingsListCommand());
        command.Add(CreateSettingsUpdateCommand());

        return command;
    }

    public static Command CreateSettingsListCommand()
    {
        var command = new Command("list", "List settings.");
        command.SetAction((ParseResult _) => CommandHandler.SettingsList());

        return command;
    }

    public static Command CreateSettingsUpdateCommand()
    {
        var allOption = new Option<bool?>("--all-physical-drives")
        {
            Description = "Use all physical drives."
        };

        var retriesOption = new Option<int?>("--retries", ["-r"])
        {
            Description = "Number of retries to try read or write data."
        };

        var verifyOption = new Option<bool?>("--verify", ["-v"])
        {
            Description = "Verify while reading and writing."
        };

        var forceOption = new Option<bool?>("--force", ["-f"])
        {
            Description = "Force and ignore errors when retries are exceeded."
        };

        var skipUnusedSectorsOption = new Option<bool?>("--skip-unused-sectors")
        {
            Description = "Skip unused sectors."
        };

        var useCacheOption = new Option<bool?>("--use-cache")
        {
            Description = "Use cache."
        };

        var cacheTypeOption = new Option<CacheType?>("--cache-type")
        {
            Description = "Type of cache to use."
        };

        var sparseFilesOption = new Option<bool?>("--sparse-files")
        {
            Description = "Create sparse files."
        };

        var command = new Command("update", "Update settings.");
        command.Add(allOption);
        command.Add(retriesOption);
        command.Add(forceOption);
        command.Add(verifyOption);
        command.Add(skipUnusedSectorsOption);
        command.Add(useCacheOption);
        command.Add(cacheTypeOption);
        command.Add(sparseFilesOption);
        command.Validators.Add((CommandResult validate) =>
        {
            if (validate.GetResult(allOption) is null &&
                validate.GetResult(retriesOption) is null &&
                validate.GetResult(forceOption) is null &&
                validate.GetResult(verifyOption) is null &&
                validate.GetResult(skipUnusedSectorsOption) is null &&
                validate.GetResult(useCacheOption) is null &&
                validate.GetResult(cacheTypeOption) is null &&
                validate.GetResult(sparseFilesOption) is null)
            {
                validate.AddError("At least one option must be specified");
            }
        });
        command.SetAction((ParseResult ctx) =>
        {
            var allPhysicalDrives = ctx.GetValue(allOption);
            var retries = ctx.GetValue(retriesOption);
            var force = ctx.GetValue(forceOption);
            var verify = ctx.GetValue(verifyOption);
            var skipUnusedSectors = ctx.GetValue(skipUnusedSectorsOption);
            var useCache = ctx.GetValue(useCacheOption);
            var cacheType = ctx.GetValue(cacheTypeOption);
            var sparseFiles = ctx.GetValue(sparseFilesOption);
            return CommandHandler.SettingsUpdate(allPhysicalDrives, retries, force, verify,
                skipUnusedSectors, useCache, cacheType, sparseFiles);
        });

        return command;
    }
}