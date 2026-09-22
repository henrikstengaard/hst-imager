using System.CommandLine;
using System.CommandLine.Parsing;
using Hst.Imager.Core.Models;

namespace Hst.Imager.ConsoleApp;

public static class GptCommandFactory
{
    public static Command CreateGptCommand()
    {
        var command = new Command("gpt", "Guid Partition Table.");

        command.Add(CreateGptInfo());
        command.Add(CreateGptInit());
        command.Add(CreateGptPart());

        return command;
    }

    private static Command CreateGptInfo()
    {
        var pathArgument = new Argument<string>("Path")
        {
            Description = "Path to physical drive or image file."
        };

        var showUnallocatedOption = new Option<bool>("--unallocated", ["-u"])
        {
            Description = "Show unallocated.",
            DefaultValueFactory = (ArgumentResult _) => true
        };

        var command = new Command("info", "Display info about Guid Partition Table.");
        command.SetAction((ParseResult ctx) =>
        {
            var path = ctx.GetValue(pathArgument);
            var showUnallocated = ctx.GetValue(showUnallocatedOption);
            return CommandHandler.GptInfo(path, showUnallocated);
        });
        command.Add(pathArgument);
        command.Add(showUnallocatedOption);

        return command;
    }

    private static Command CreateGptInit()
    {
        var pathArgument = new Argument<string>("Path")
        {
            Description = "Path to physical drive or image file."
        };

        var mbrInitCommand = new Command("initialize", "Initialize disk with empty Guid Partition Table.");
        mbrInitCommand.Aliases.Add("init");
        mbrInitCommand.SetAction((ParseResult ctx) =>
        {
            var path = ctx.GetValue(pathArgument);
            return CommandHandler.GptInit(path);
        });
        mbrInitCommand.Add(pathArgument);

        return mbrInitCommand;
    }

    private static Command CreateGptPart()
    {
        var partCommand = new Command("part", "Partition.");

        partCommand.Add(CreateGptPartAdd());
        partCommand.Add(CreateGptPartDel());
        partCommand.Add(CreateGptPartFormat());

        return partCommand;
    }

    private static Command CreateGptPartAdd()
    {
        var pathArgument = new Argument<string>("Path")
        {
            Description = "Path to physical drive or image file."
        };

        var typeArgument = new Argument<string>("Type")
        {
            Description = "Type of the partition as name or guid (e.g. name NTFS or value EBD0A0A2-B9E5-4433-87C0-68B6B72699C7 for WindowsBasicData)."
        };

        var nameArgument = new Argument<string>("Name")
        {
            Description = "Name of the partition."
        };

        var sizeArgument = new Argument<string>("Size")
        {
            Description = "Size of the partition."
        };

        var startSectorOption = new Option<long?>("--start-sector", ["-s"])
        {
            Description = "Start sector."
        };

        var command = new Command("add", "Add partition.");
        command.SetAction((ParseResult ctx) =>
        {
            var path = ctx.GetValue(pathArgument);
            var type = ctx.GetValue(typeArgument);
            var name = ctx.GetValue(nameArgument);
            var size = ctx.GetValue(sizeArgument);
            var startSector = ctx.GetValue(startSectorOption);
            return CommandHandler.GptPartAdd(path, type, name, size, startSector);
        });
        command.Add(pathArgument);
        command.Add(typeArgument);
        command.Add(nameArgument);
        command.Add(sizeArgument);
        command.Add(startSectorOption);

        return command;
    }

    private static Command CreateGptPartDel()
    {
        var path = new Argument<string>("Path")
        {
            Description = "Path to physical drive or image file."
        };

        var partitionNumber = new Argument<int>("PartitionNumber")
        {
            Description = "Partition number to delete."
        };

        var command = new Command("delete", "Delete partition.");
        command.Aliases.Add("del");
        command.SetAction((ParseResult ctx) =>
        {
            var pathVal = ctx.GetValue(path);
            var partitionNum = ctx.GetValue(partitionNumber);
            return CommandHandler.GptPartDel(pathVal, partitionNum);
        });
        command.Add(path);
        command.Add(partitionNumber);

        return command;
    }

    private static Command CreateGptPartFormat()
    {
        var pathArgument = new Argument<string>("Path")
        {
            Description = "Path to physical drive or image file."
        };

        var partitionNumberArgument = new Argument<int>("PartitionNumber")
        {
            Description = "Partition number to delete."
        };

        var typeArgument = new Argument<GptPartType>("Type")
        {
            Description = "Type of partition to create."
        };

        var nameArgument = new Argument<string>("Name")
        {
            Description = "Name of the partition."
        };

        var formatCommand = new Command("format", "Format partition.");
        formatCommand.SetAction((ParseResult ctx) =>
        {
            var path = ctx.GetValue(pathArgument);
            var partitionNumber = ctx.GetValue(partitionNumberArgument);
            var type = ctx.GetValue(typeArgument);
            var name = ctx.GetValue(nameArgument);
            return CommandHandler.GptPartFormat(path, partitionNumber, type, name);
        });
        formatCommand.Add(pathArgument);
        formatCommand.Add(partitionNumberArgument);
        formatCommand.Add(typeArgument);
        formatCommand.Add(nameArgument);

        return formatCommand;
    }
}