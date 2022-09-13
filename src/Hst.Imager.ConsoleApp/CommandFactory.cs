namespace Hst.Imager.ConsoleApp
{
    using System.CommandLine;
    using System.IO;

    public static class CommandFactory
    {
        public static Command CreateRootCommand()
        {
            var rootCommand = new RootCommand
            {
                Description = "Hst Imager reads, writes and initializes image files and physical drives."
            };
            rootCommand.AddCommand(CreateScriptCommand());
            rootCommand.AddCommand(CreateBlankCommand());
            rootCommand.AddCommand(CreateRdbCommand());

            return rootCommand;
        }
        
        public static Command CreateScriptCommand()
        {
            var pathArgument = new Argument<string>(
                name: "path",
                description: "Path to script file.");
            
            var scriptCommand = new Command("script", "Run script.");
            scriptCommand.AddArgument(pathArgument);
            scriptCommand.SetHandler(CommandHandler.Script, pathArgument);

            return scriptCommand;
        }
        
        public static Command CreateBlankCommand()
        {
            var pathArgument = new Argument<string>(
                name: "path",
                description: "Path image file.");

            var sizeArgument = new Argument<string>(
                name: "size",
                description: "Size of image file.");

            var compatibleSizeOption = new Option<bool>(
                new[] { "--compatible", "-c" },
                description: "Full size.",
                getDefaultValue: () => true);
            
            var blankCommand = new Command("blank", "Blank image file.");
            blankCommand.AddArgument(pathArgument);
            blankCommand.AddArgument(sizeArgument);
            blankCommand.AddOption(compatibleSizeOption);
            blankCommand.SetHandler(CommandHandler.Blank, pathArgument, sizeArgument, compatibleSizeOption);

            return blankCommand;
        }

        public static Command CreateRdbCommand()
        {
            var rdbCommand = new Command("rdb", "Rigid disk block.");
            
            rdbCommand.AddCommand(CreateRdbInfo());
            rdbCommand.AddCommand(CreateRdbInit());
            rdbCommand.AddCommand(CreateRdbFs());
            rdbCommand.AddCommand(CreateRdbPart());
            
            return rdbCommand;
        }

        private static Command CreateRdbInfo()
        {
            var pathArgument = new Argument<string>(
                name: "path",
                description: "Path to physical drive or image file.");

            var rdbInfoCommand = new Command("info", "Initialize disk with empty Rigid Disk Block.");
            rdbInfoCommand.SetHandler(CommandHandler.RdbInfo, pathArgument);
            rdbInfoCommand.AddArgument(pathArgument);

            return rdbInfoCommand;
        }
        
        private static Command CreateRdbInit()
        {
            var pathArgument = new Argument<string>(
                name: "path",
                description: "Path to physical drive or image file.");

            var sizeOption = new Option<string>(
                new[] { "--size", "-s" },
                description: "Size of disk.");

            var nameOption = new Option<string>(
                new[] { "--name", "-n" },
                "Name of disk.");

            var rdbBlockLoOption = new Option<int>(
                new[] { "--rdbBlockLo" },
                "Low block reserved for Rigid Disk Block (0-15).");
            
            var rdbInitCommand = new Command("initialize", "Initialize disk with empty Rigid Disk Block.");
            rdbInitCommand.AddAlias("init");
            rdbInitCommand.SetHandler(CommandHandler.RdbInit, pathArgument, sizeOption, nameOption,
                rdbBlockLoOption);
            rdbInitCommand.AddArgument(pathArgument);
            rdbInitCommand.AddOption(sizeOption);
            rdbInitCommand.AddOption(nameOption);
            rdbInitCommand.AddOption(rdbBlockLoOption);

            return rdbInitCommand;
        }

        private static Command CreateRdbFs()
        {
            var rdbFsCommand = new Command("filesystem", "File system.");
            rdbFsCommand.AddAlias("fs");
            rdbFsCommand.AddCommand(CreateRdbFsAdd());
            rdbFsCommand.AddCommand(CreateRdbFsDel());

            return rdbFsCommand;
        }

        private static Command CreateRdbFsAdd()
        {
            var pathArgument = new Argument<string>(
                name: "path",
                description: "Path to physical drive or image file.");

            var fileSystemPathArgument = new Argument<string>(
                name: "fileSystemPath",
                description: "Path to file system to add.");

            var dosTypeArgument = new Option<string>(
                new[] { "--dosType", "-d" },
                description: "Dos Type for file system (eg. DOS3, PDS3).");

            var fileSystemNameOption = new Option<string>(
                new[] { "--name", "-n" },
                description: "Name of file system.");
            
            var rdbFsAddCommand = new Command("add", "Add file system.");
            rdbFsAddCommand.SetHandler(CommandHandler.RdbFsAdd, pathArgument, fileSystemPathArgument, dosTypeArgument, fileSystemNameOption);
            rdbFsAddCommand.AddArgument(pathArgument);
            rdbFsAddCommand.AddArgument(fileSystemPathArgument);
            rdbFsAddCommand.AddOption(dosTypeArgument);
            rdbFsAddCommand.AddOption(fileSystemNameOption);

            return rdbFsAddCommand;
        }

        private static Command CreateRdbFsDel()
        {
            var pathArgument = new Argument<string>(
                name: "path",
                description: "Path to physical drive or image file.");

            var fileSystemNumber = new Argument<int>(
                name: "fileSystemNumber",
                description: "File system number to delete.");
            
            var rdbFsDelCommand = new Command("delete", "Delete file system.");
            rdbFsDelCommand.AddAlias("del");
            rdbFsDelCommand.SetHandler(CommandHandler.RdbFsDel, pathArgument, fileSystemNumber);
            rdbFsDelCommand.AddArgument(pathArgument);
            rdbFsDelCommand.AddArgument(fileSystemNumber);

            return rdbFsDelCommand;
        }

        private static Command CreateRdbPart()
        {
            var rdbPartCommand = new Command("part", "Partition.");
            
            rdbPartCommand.AddCommand(CreateRdbPartAdd());
            rdbPartCommand.AddCommand(CreateRdbPartDel());
            rdbPartCommand.AddCommand(CreateRdbPartFormat());
            
            return rdbPartCommand;
        }

        private static Command CreateRdbPartAdd()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var dosTypeArgument = new Argument<string>(
                name: "DosType",
                description: "DOS type for the partition to use.");
            
            var nameArgument = new Argument<string>(
                name: "Name",
                description: "Name of the partition.");

            var sizeArgument = new Argument<string>(
                name: "Size",
                description: "Size of the partition.");
            
            var autoMountOption = new Option<bool>(
                new[] { "--automount", "-am" },
                description: "Set partition to auto mount on boot.",
                getDefaultValue: () => true);

            var bootableOption = new Option<bool>(
                new[] { "--bootable", "-b" },
                description: "Set bootable.",
                getDefaultValue: () => false);

            var priorityOption = new Option<int>(
                new[] { "--priority", "-p" },
                description: "Set boot priority.",
                getDefaultValue: () => 0);

            var blockSizeOption = new Option<int>(
                new[] { "--blockSize", "-bs" },
                description: "Block size for the partition.",
                getDefaultValue: () => 512);

            var rdbPartAddCommand = new Command("add", "Add partition.");
            rdbPartAddCommand.SetHandler(CommandHandler.RdbPartAdd, pathArgument, nameArgument, dosTypeArgument, sizeArgument, autoMountOption, bootableOption, priorityOption, blockSizeOption);
            rdbPartAddCommand.AddArgument(pathArgument);
            rdbPartAddCommand.AddArgument(nameArgument);
            rdbPartAddCommand.AddArgument(dosTypeArgument);
            rdbPartAddCommand.AddArgument(sizeArgument);
            rdbPartAddCommand.AddOption(autoMountOption);
            rdbPartAddCommand.AddOption(bootableOption);
            rdbPartAddCommand.AddOption(priorityOption);
            rdbPartAddCommand.AddOption(blockSizeOption);

            return rdbPartAddCommand;
        }

        private static Command CreateRdbPartDel()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var partitionNumber = new Argument<int>(
                name: "PartitionNumber",
                description: "Partition number to delete.");
            
            var rdbPartDelCommand = new Command("delete", "Delete partition.");
            rdbPartDelCommand.AddAlias("del");
            rdbPartDelCommand.SetHandler(CommandHandler.RdbPartDel, pathArgument, partitionNumber);
            rdbPartDelCommand.AddArgument(pathArgument);
            rdbPartDelCommand.AddArgument(partitionNumber);

            return rdbPartDelCommand;
        }

        private static Command CreateRdbPartFormat()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var partitionNumber = new Argument<int>(
                name: "PartitionNumber",
                description: "Partition number to format.");

            var volumeNameArgument = new Argument<string>(
                name: "VolumeName",
                description: "Name of the volume (eg. Workbench).");

            var rdbPartFormatCommand = new Command("format", "Format partition.");
            rdbPartFormatCommand.SetHandler(CommandHandler.RdbPartFormat, pathArgument, partitionNumber, volumeNameArgument);
            rdbPartFormatCommand.AddArgument(pathArgument);
            rdbPartFormatCommand.AddArgument(partitionNumber);
            rdbPartFormatCommand.AddArgument(volumeNameArgument);

            return rdbPartFormatCommand;
        }
    }
}