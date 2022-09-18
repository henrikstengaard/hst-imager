namespace Hst.Imager.ConsoleApp
{
    using System.CommandLine;

    public static class MbrCommandFactory
    {
        public static Command CreateMbrCommand()
        {
            var command = new Command("mbr", "Master Boot Record.");

            command.AddCommand(CreateMbrInfo());
            command.AddCommand(CreateMbrInit());
            command.AddCommand(CreateMbrPart());

            return command;
        }

        private static Command CreateMbrInfo()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var command = new Command("info", "Show Master Boot Record information.");
            command.SetHandler(CommandHandler.MbrInfo, pathArgument);
            command.AddArgument(pathArgument);

            return command;
        }

        private static Command CreateMbrInit()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to image file.");

            var mbrInitCommand = new Command("initialize", "Initialize disk with empty Master Boot Record.");
            mbrInitCommand.AddAlias("init");
            mbrInitCommand.SetHandler(CommandHandler.MbrInit, pathArgument);
            mbrInitCommand.AddArgument(pathArgument);

            return mbrInitCommand;
        }

        private static Command CreateMbrPart()
        {
            var partCommand = new Command("part", "Partition.");

            partCommand.AddCommand(CreateMbrPartAdd());
            partCommand.AddCommand(CreateMbrPartDel());
            partCommand.AddCommand(CreateMbrPartFormat());

            return partCommand;
        }

        private static Command CreateMbrPartAdd()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to image file.");

            var typeArgument = new Argument<string>(
                name: "Type",
                description: "Type for the partition to use (eg. FAT32).");

            var sizeArgument = new Argument<string>(
                name: "Size",
                description: "Size of the partition.");

            var startSectorOption = new Option<long>(
                new[] { "--start", "-s" },
                description: "Set start sector.",
                getDefaultValue: () => 1);

            var activeOption = new Option<bool>(
                new[] { "--active", "-a" },
                description: "Set partition active (bootable).",
                getDefaultValue: () => false);

            var mbrPartAddCommand = new Command("add", "Add partition.");
            mbrPartAddCommand.SetHandler(CommandHandler.MbrPartAdd, pathArgument, typeArgument, sizeArgument,
                startSectorOption, activeOption);
            mbrPartAddCommand.AddArgument(pathArgument);
            mbrPartAddCommand.AddArgument(typeArgument);
            mbrPartAddCommand.AddArgument(sizeArgument);
            mbrPartAddCommand.AddOption(startSectorOption);
            mbrPartAddCommand.AddOption(activeOption);

            return mbrPartAddCommand;
        }

        private static Command CreateMbrPartDel()
        {
            var path = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var partitionNumber = new Argument<int>(
                name: "PartitionNumber",
                description: "Partition number to delete.");

            var command = new Command("delete", "Delete partition.");
            command.AddAlias("del");
            command.SetHandler(CommandHandler.MbrPartDel, path, partitionNumber);
            command.AddArgument(path);
            command.AddArgument(partitionNumber);

            return command;
        }

        private static Command CreateMbrPartFormat()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to image file.");

            var partitionNumberArgument = new Argument<int>(
                name: "PartitionNumber",
                description: "Partition number to delete.");

            var nameArgument = new Argument<string>(
                name: "Name",
                description: "Name of the partition.");

            var formatCommand = new Command("format", "Format partition.");
            formatCommand.SetHandler(CommandHandler.MbrPartFormat, pathArgument, partitionNumberArgument, nameArgument);
            formatCommand.AddArgument(pathArgument);
            formatCommand.AddArgument(partitionNumberArgument);
            formatCommand.AddArgument(nameArgument);

            return formatCommand;
        }
    }
}