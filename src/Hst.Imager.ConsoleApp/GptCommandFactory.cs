using System.CommandLine;
using Hst.Imager.Core.Models;

namespace Hst.Imager.ConsoleApp;

public static class GptCommandFactory
{
    public static Command CreateGptCommand()
    {
        var command = new Command("gpt", "Guid Partition Table.");

        command.AddCommand(CreateGptInfo());
        command.AddCommand(CreateGptInit());
        command.AddCommand(CreateGptPart());

        return command;
    }

    private static Command CreateGptInfo()
    {
        var pathArgument = new Argument<string>(
            name: "Path",
            description: "Path to physical drive or image file.");

        var showUnallocatedOption = new Option<bool>(
            new[] { "--unallocated", "-u" },
            description: "Show unallocated.",
            getDefaultValue: () => true);
            
        var command = new Command("info", "Display info about Guid Partition Table.");
        command.SetHandler(CommandHandler.GptInfo, pathArgument, showUnallocatedOption);
        command.AddArgument(pathArgument);
        command.AddOption(showUnallocatedOption);

        return command;
    }

    private static Command CreateGptInit()
    {
        var pathArgument = new Argument<string>(
            name: "Path",
            description: "Path to physical drive or image file.");

        var mbrInitCommand = new Command("initialize", "Initialize disk with empty Guid Partition Table.");
        mbrInitCommand.AddAlias("init");
        mbrInitCommand.SetHandler(CommandHandler.GptInit, pathArgument);
        mbrInitCommand.AddArgument(pathArgument);

        return mbrInitCommand;
    }

    private static Command CreateGptPart()
    {
        var partCommand = new Command("part", "Partition.");

        partCommand.AddCommand(CreateGptPartAdd());
        partCommand.AddCommand(CreateGptPartDel());
        partCommand.AddCommand(CreateGptPartFormat());

        return partCommand;
    }

    private static Command CreateGptPartAdd()
    {
        var pathArgument = new Argument<string>(
            name: "Path",
            description: "Path to physical drive or image file.");

        var typeArgument = new Argument<GptPartType>(
            name: "Type",
            description: "Type of the partition (e.g. NTFS).");

        var nameArgument = new Argument<string>(
            name: "Name",
            description: "Name of the partition.");

        var sizeArgument = new Argument<string>(
            name: "Size",
            description: "Size of the partition.");

        var startSectorOption = new Option<long?>(
            new[] { "--start-sector", "-s" },
            description: "Start sector.");

        var command = new Command("add", "Add partition.");
        command.SetHandler(CommandHandler.GptPartAdd, pathArgument, typeArgument, nameArgument, sizeArgument,
            startSectorOption);
        command.AddArgument(pathArgument);
        command.AddArgument(typeArgument);
        command.AddArgument(nameArgument);
        command.AddArgument(sizeArgument);
        command.AddOption(startSectorOption);

        return command;
    }

    private static Command CreateGptPartDel()
    {
        var path = new Argument<string>(
            name: "Path",
            description: "Path to physical drive or image file.");

        var partitionNumber = new Argument<int>(
            name: "PartitionNumber",
            description: "Partition number to delete.");

        var command = new Command("delete", "Delete partition.");
        command.AddAlias("del");
        command.SetHandler(CommandHandler.GptPartDel, path, partitionNumber);
        command.AddArgument(path);
        command.AddArgument(partitionNumber);

        return command;
    }

    private static Command CreateGptPartFormat()
    {
        var pathArgument = new Argument<string>(
            name: "Path",
            description: "Path to physical drive or image file.");

        var partitionNumberArgument = new Argument<int>(
            name: "PartitionNumber",
            description: "Partition number to delete.");

        var typeArgument = new Argument<GptPartType>(
            name: "Type",
            description: "Type of partition to create.");
        
        var nameArgument = new Argument<string>(
            name: "Name",
            description: "Name of the partition.");

        var formatCommand = new Command("format", "Format partition.");
        formatCommand.SetHandler(CommandHandler.GptPartFormat, pathArgument, partitionNumberArgument,
            typeArgument, nameArgument);
        formatCommand.AddArgument(pathArgument);
        formatCommand.AddArgument(partitionNumberArgument);
        formatCommand.AddArgument(typeArgument);
        formatCommand.AddArgument(nameArgument);

        return formatCommand;
    }
}