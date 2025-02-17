namespace Hst.Imager.ConsoleApp
{
    using System.CommandLine;
    using System.IO;
    using Core.Commands;
    using Core.Models;

    public static class CommandFactory
    {
        public static readonly Option<FileInfo> LogFileOption = new(
            new[] { "--log-file" },
            description: "Write log file.");

        public static readonly Option<bool> VerboseOption = new(
            new[] { "--verbose" },
            description: "Verbose output.");

        public static readonly Option<FormatEnum> FormatOption = new(
            new[] { "--format", "-f" },
            description: "Format of output.",
            getDefaultValue: () => FormatEnum.Table);
        
        public static Command CreateRootCommand()
        {
            var rootCommand = new RootCommand
            {
                Description = "Hst Imager reads, writes and initializes image files and physical drives."
            };

            rootCommand.AddGlobalOption(LogFileOption);
            rootCommand.AddGlobalOption(VerboseOption);
            rootCommand.AddCommand(CreateBlankCommand());
            rootCommand.AddCommand(CreateConvertCommand());
            rootCommand.AddCommand(CreateFormatCommand());
            rootCommand.AddCommand(CreateInfoCommand());
            rootCommand.AddCommand(CreateListCommand());
            rootCommand.AddCommand(CreateOptimizeCommand());
            rootCommand.AddCommand(CreateReadCommand());
            rootCommand.AddCommand(CreateScriptCommand());
            rootCommand.AddCommand(CreateBlockCommand());
            rootCommand.AddCommand(CreateCompareCommand());
            rootCommand.AddCommand(CreateWriteCommand());
            rootCommand.AddCommand(GptCommandFactory.CreateGptCommand());
            rootCommand.AddCommand(MbrCommandFactory.CreateMbrCommand());
            rootCommand.AddCommand(RdbCommandFactory.CreateRdbCommand());
            rootCommand.AddCommand(FsCommandFactory.CreateFsCommand());
            rootCommand.AddCommand(AdfCommandFactory.CreateAdfCommand());

            return rootCommand;
        }

        public static Command CreateScriptCommand()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to script file.");

            var scriptCommand = new Command("script", "Run script.");
            scriptCommand.AddArgument(pathArgument);
            scriptCommand.SetHandler(CommandHandler.Script, pathArgument);

            return scriptCommand;
        }

        public static Command CreateInfoCommand()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var showUnallocatedOption = new Option<bool>(
                new[] { "--unallocated", "-u" },
                description: "Show unallocated.",
                getDefaultValue: () => true);
            
            var command = new Command("info", "Display info about physical drive or image file.");
            command.AddArgument(pathArgument);
            command.AddOption(showUnallocatedOption);
            command.SetHandler(CommandHandler.Info, pathArgument, showUnallocatedOption);

            return command;
        }

        public static Command CreateListCommand()
        {
            var allOption = new Option<bool>(
                new[] { "--all", "-a" },
                description: "Show all physical drives.",
                getDefaultValue: () => false);
            
            var listCommand = new Command("list", "Display list of physical drives.");
            listCommand.AddOption(allOption);
            listCommand.SetHandler(CommandHandler.List, allOption);

            return listCommand;
        }

        public static Command CreateWriteCommand()
        {
            var sourceArgument = new Argument<string>(
                name: "Source",
                description: "Path to source image file.");

            var destinationArgument = new Argument<string>(
                name: "Destination",
                description: "Path to destination physical drive.");

            var sizeOption = new Option<string>(
                new[] { "--size", "-s" },
                description: "Size of image file to write.");

            var retriesOption = new Option<int>(
                new[] { "--retries", "-r" },
                description: "Number of retries to try read or write data.",
                getDefaultValue: () => 5);

            var verifyOption = new Option<bool>(
                new[] { "--verify", "-v" },
                description: "Verify data written.");
            
            var forceOption = new Option<bool>(
                new[] { "--force", "-f" },
                description: "Force write to ignore write errors.",
                getDefaultValue: () => false);
            
            var writeCommand = new Command("write", "Write image file to physical drive.");
            writeCommand.AddArgument(sourceArgument);
            writeCommand.AddArgument(destinationArgument);
            writeCommand.AddOption(sizeOption);
            writeCommand.AddOption(retriesOption);
            writeCommand.AddOption(verifyOption);
            writeCommand.AddOption(forceOption);
            writeCommand.SetHandler(CommandHandler.Write, sourceArgument, destinationArgument, sizeOption,
                retriesOption, verifyOption, forceOption);

            return writeCommand;
        }

        public static Command CreateReadCommand()
        {
            var sourceArgument = new Argument<string>(
                name: "Source",
                description: "Path to source physical drive.");

            var destinationArgument = new Argument<string>(
                name: "Destination",
                description: "Path to destination image file.");

            var sizeOption = new Option<string>(
                new[] { "--size", "-s" },
                description: "Size of physical drive to read.");

            var retriesOption = new Option<int>(
                new[] { "--retries", "-r" },
                description: "Number of retries to try read or write data.",
                getDefaultValue: () => 5);
            
            var verifyOption = new Option<bool>(
                new[] { "--verify", "-v" },
                description: "Verify data read.");

            var forceOption = new Option<bool>(
                new[] { "--force", "-f" },
                description: "Force read to ignore read errors.",
                getDefaultValue: () => false);
            
            var startOption = new Option<long?>(
                new[] { "--start", "-st" },
                description: "Start offset.");
            
            var readCommand = new Command("read", "Read physical drive to image file.");
            readCommand.AddArgument(sourceArgument);
            readCommand.AddArgument(destinationArgument);
            readCommand.AddOption(sizeOption);
            readCommand.AddOption(retriesOption);
            readCommand.AddOption(verifyOption);
            readCommand.AddOption(forceOption);
            readCommand.AddOption(startOption);
            readCommand.SetHandler(CommandHandler.Read, sourceArgument, destinationArgument, sizeOption, retriesOption, 
                verifyOption, forceOption, startOption);

            return readCommand;
        }

        public static Command CreateConvertCommand()
        {
            var sourceArgument = new Argument<string>(
                name: "Source",
                description: "Path to source image file.");

            var destinationArgument = new Argument<string>(
                name: "Destination",
                description: "Path to destination image file.");

            var sizeOption = new Option<string>(
                new[] { "--size", "-s" },
                description: "Size of image file convert.");

            var verifyOption = new Option<bool>(
                new[] { "--verify", "-v" },
                description: "Verify data converted.");
            
            var convertCommand = new Command("convert", "Convert image file.");
            convertCommand.AddArgument(sourceArgument);
            convertCommand.AddArgument(destinationArgument);
            convertCommand.AddOption(sizeOption);
            convertCommand.AddOption(verifyOption);
            convertCommand.SetHandler(CommandHandler.Convert, sourceArgument, destinationArgument, sizeOption, verifyOption);

            return convertCommand;
        }

        public static Command CreateFormatCommand()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var partitionTableArgument = new Argument<FormatType>(
                name: "FormatType",
                description: "Type of disk to format.");

            var fileSystemArgument = new Argument<string>(
                name: "FileSystem",
                description: "File system to format partition(s) created.");

            var assetActionOption = new Option<AssetAction>(
                ["--asset-action"],
                description: "Asset action for formatting (only for RDB and PiStorm).",
                getDefaultValue: () => AssetAction.DownloadPfs3Aio);

            var assetPathOption = new Option<string>(
                ["--asset-path"],
                description: "Path to asset file used to format (only for RDB and PiStorm).");

            var sizeOption = new Option<string>(
                ["--size", "-s"],
                description: "Size of disk to format.");

            var command = new Command("format", "Format physical drive or image file.");
            command.AddArgument(pathArgument);
            command.AddArgument(partitionTableArgument);
            command.AddArgument(fileSystemArgument);
            command.AddOption(assetActionOption);
            command.AddOption(assetPathOption);
            command.AddOption(sizeOption);
            command.SetHandler(CommandHandler.Format, pathArgument, partitionTableArgument, fileSystemArgument,
                assetActionOption, assetPathOption, sizeOption);

            return command;
        }

        public static Command CreateBlankCommand()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path image file.");

            var sizeArgument = new Argument<string>(
                name: "Size",
                description: "Size of image file.");

            var compatibleSizeOption = new Option<bool>(
                new[] { "--compatible", "-c" },
                description: "Make size compatible by reducing it with 5%.",
                getDefaultValue: () => false);

            var blankCommand = new Command("blank", "Blank image file.");
            blankCommand.AddArgument(pathArgument);
            blankCommand.AddArgument(sizeArgument);
            blankCommand.AddOption(compatibleSizeOption);
            blankCommand.SetHandler(CommandHandler.Blank, pathArgument, sizeArgument, compatibleSizeOption);

            return blankCommand;
        }

        public static Command CreateOptimizeCommand()
        {
            var pathArgument = new Argument<string>(
                name: "Source",
                description: "Path to image file.");

            var sizeOption = new Option<string>(
                new[] { "--size", "-s" },
                description: "Size to optimize to.");

            var partitionTableOption = new Option<PartitionTable>(
                new[] { "--partition-table", "-pt" },
                description: "Optimize to size of partition table.",
                getDefaultValue: () => PartitionTable.None);
            
            var convertCommand = new Command("optimize", "Optimize image file size.");
            convertCommand.AddArgument(pathArgument);
            convertCommand.AddOption(sizeOption);
            convertCommand.AddOption(partitionTableOption);
            convertCommand.SetHandler(CommandHandler.Optimize, pathArgument, sizeOption, partitionTableOption);

            return convertCommand;
        }

        public static Command CreateCompareCommand()
        {
            var sourceArgument = new Argument<string>(
                name: "Source",
                description: "Path to source physical drive or image file.");

            var destinationArgument = new Argument<string>(
                name: "Destination",
                description: "Path to destination physical drive or image file.");

            var sizeOption = new Option<string>(
                new[] { "--size", "-s" },
                description: "Size to verify.");
            
            var retriesOption = new Option<int>(
                new[] { "--retries", "-r" },
                description: "Number of retries to try read or write data.",
                getDefaultValue: () => 5);
            
            var forceOption = new Option<bool>(
                new[] { "--force", "-f" },
                description: "Force compare to ignore read errors.",
                getDefaultValue: () => false);
            
            var convertCommand = new Command("compare", "Compare source and destination.");
            convertCommand.AddArgument(sourceArgument);
            convertCommand.AddArgument(destinationArgument);
            convertCommand.AddOption(sizeOption);
            convertCommand.AddOption(retriesOption);
            convertCommand.AddOption(forceOption);
            convertCommand.SetHandler(CommandHandler.Compare, sourceArgument, destinationArgument, sizeOption, retriesOption, forceOption);

            return convertCommand;
        }

        public static Command CreateBlockCommand()
        {
            var command = new Command("block", "Block.");
            command.AddCommand(CreateBlockReadCommand());
            command.AddCommand(CreateBlockViewCommand());
            return command;
        }

        public static Command CreateBlockReadCommand()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var outputPathArgument = new Argument<string>(
                name: "OutputPath",
                description: "Output path to write sectors.");

            var blockSizeOption = new Option<int>(
                new[] { "--block-size", "-bs" },
                description: "Block size.",
                getDefaultValue: () => 512);

            var usedOption = new Option<bool>(
                new[] { "--used", "-u" },
                description: "Only used blocks.");
            
            var startOption = new Option<long?>(
                new[] { "--start", "-s" },
                description: "Start offset.");

            var endOption = new Option<long?>(
                new[] { "--end", "-e" },
                description: "End offset.");
            
            var blankCommand = new Command("read", "Read blocks to file per block.");
            blankCommand.SetHandler(CommandHandler.BlockRead, pathArgument, outputPathArgument, blockSizeOption,
                usedOption, startOption, endOption);
            blankCommand.AddArgument(pathArgument);
            blankCommand.AddArgument(outputPathArgument);
            blankCommand.AddOption(blockSizeOption);
            blankCommand.AddOption(usedOption);
            blankCommand.AddOption(startOption);
            blankCommand.AddOption(endOption);

            return blankCommand;
        }
        
        public static Command CreateBlockViewCommand()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var blockSizeOption = new Option<int>(
                new[] { "--block-size", "-bs" },
                description: "Block size.",
                getDefaultValue: () => 512);

            var startOption = new Option<long?>(
                new[] { "--start", "-s" },
                description: "Start offset.");

            var blankCommand = new Command("view", "View block as hex.");
            blankCommand.SetHandler(CommandHandler.BlockView, pathArgument, blockSizeOption, startOption);
            blankCommand.AddArgument(pathArgument);
            blankCommand.AddOption(blockSizeOption);
            blankCommand.AddOption(startOption);

            return blankCommand;
        }
    }
}