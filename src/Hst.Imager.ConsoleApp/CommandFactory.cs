using Hst.Imager.ConsoleApp.Commands;

namespace Hst.Imager.ConsoleApp
{
    using System.CommandLine;
    using System.CommandLine.Parsing;
    using System.IO;
    using Core.Commands;
    using Core.Models;

    public static class CommandFactory
    {
        public static readonly Option<FileInfo> LogFileOption = new("--log-file")
        {
            Description = "Write log file.",
            Recursive = true
        };

        public static readonly Option<bool> VerboseOption = new("--verbose")
        {
            Description = "Verbose output.",
            Recursive = true
        };

        public static readonly Option<FormatEnum> FormatOption = new("--format", ["-f"])
        {
            Description = "Format of output.",
            DefaultValueFactory = (ArgumentResult _) => FormatEnum.Table
        };

        public static Command CreateRootCommand()
        {
            var rootCommand = new RootCommand("Hst Imager reads, writes and initializes image files and physical disks.");

            rootCommand.Add(LogFileOption);
            rootCommand.Add(VerboseOption);
            rootCommand.Add(CreateBlankCommand());
            rootCommand.Add(CreateConvertCommand());
            rootCommand.Add(CreateTransferCommand());
            rootCommand.Add(CreateFormatCommand());
            rootCommand.Add(CreateInfoCommand());
            rootCommand.Add(CreateListCommand());
            rootCommand.Add(CreateOptimizeCommand());
            rootCommand.Add(CreateReadCommand());
            rootCommand.Add(CreateScriptCommand());
            rootCommand.Add(CreateBlockCommand());
            rootCommand.Add(CreateCompareCommand());
            rootCommand.Add(CreateWriteCommand());
            rootCommand.Add(GptCommandFactory.CreateGptCommand());
            rootCommand.Add(MbrCommandFactory.CreateMbrCommand());
            rootCommand.Add(RdbCommandFactory.CreateRdbCommand());
            rootCommand.Add(FsCommandFactory.CreateFsCommand());
            rootCommand.Add(AdfCommandFactory.CreateAdfCommand());
            rootCommand.Add(SettingsCommandFactory.CreateSettingsCommand());

            return rootCommand;
        }

        private static Command CreateScriptCommand()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to script file."
            };

            var scriptCommand = new Command("script", "Run a script.");
            scriptCommand.Add(pathArgument);
            scriptCommand.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                return CommandHandler.Script(path);
            });

            return scriptCommand;
        }

        private static Command CreateInfoCommand()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical disk or image file."
            };

            var showUnallocatedOption = new Option<bool>("--unallocated", ["-u"])
            {
                Description = "Show unallocated.",
                DefaultValueFactory = (ArgumentResult _) => true
            };

            var command = new Command("info", "Display information about an image file or a physical disk.");
            command.Add(pathArgument);
            command.Add(showUnallocatedOption);
            command.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var showUnallocated = ctx.GetValue(showUnallocatedOption);
                return CommandHandler.Info(path, showUnallocated);
            });

            return command;
        }

        private static Command CreateListCommand()
        {
            var listCommand = new Command("list", "Display list of physical disks.");
            listCommand.SetAction((ParseResult _) => CommandHandler.List());

            return listCommand;
        }

        private static Command CreateWriteCommand()
        {
            var sourceArgument = new Argument<string>("Source")
            {
                Description = "Path to source image file."
            };

            var destinationArgument = new Argument<string>("Destination")
            {
                Description = "Path to destination image file or physical disk."
            };

            var sizeOption = new Option<string>("--size", ["-s"])
            {
                Description = "Size to write."
            };

            var retriesOption = new Option<int?>("--retries", ["-r"])
            {
                Description = "Number of retries to try read or write data."
            };

            var verifyOption = new Option<bool?>("--verify", ["-v"])
            {
                Description = "Verify data written."
            };

            var forceOption = new Option<bool?>("--force", ["-f"])
            {
                Description = "Force write to ignore write errors."
            };

            var skipUnusedSectorsOption = new Option<bool?>("--skip-unused-sectors")
            {
                Description = "Skip unused sectors. Sectors containing zeroes are skipped to improve write speed. However, not all operating systems or file systems support this. As an example ChromeOS images will not be able to recover properly if unused sectors are skipped!"
            };

            var startOption = new Option<long?>("--start", ["-st"])
            {
                Description = "Destination start offset."
            };

            var writeCommand = new Command("write", "Write an image file or part of to a physical disk.");
            writeCommand.Add(sourceArgument);
            writeCommand.Add(destinationArgument);
            writeCommand.Add(sizeOption);
            writeCommand.Add(retriesOption);
            writeCommand.Add(verifyOption);
            writeCommand.Add(forceOption);
            writeCommand.Add(skipUnusedSectorsOption);
            writeCommand.Add(startOption);
            writeCommand.SetAction((ParseResult ctx) =>
            {
                var source = ctx.GetValue(sourceArgument);
                var destination = ctx.GetValue(destinationArgument);
                var size = ctx.GetValue(sizeOption);
                var retries = ctx.GetValue(retriesOption);
                var verify = ctx.GetValue(verifyOption);
                var force = ctx.GetValue(forceOption);
                var skipUnusedSectors = ctx.GetValue(skipUnusedSectorsOption);
                var start = ctx.GetValue(startOption);
                return CommandHandler.Write(source, destination, size, retries, verify, force, skipUnusedSectors, start);
            });

            return writeCommand;
        }

        private static Command CreateReadCommand()
        {
            var sourceArgument = new Argument<string>("Source")
            {
                Description = "Path to source image file or physical disk."
            };

            var destinationArgument = new Argument<string>("Destination")
            {
                Description = "Path to destination image file."
            };

            var sizeOption = new Option<string>("--size", ["-s"])
            {
                Description = "Size to read."
            };

            var retriesOption = new Option<int?>("--retries", ["-r"])
            {
                Description = "Number of retries to try read or write data."
            };

            var verifyOption = new Option<bool?>("--verify", ["-v"])
            {
                Description = "Verify data read."
            };

            var forceOption = new Option<bool?>("--force", ["-f"])
            {
                Description = "Force read to ignore read errors."
            };

            var startOption = new Option<long?>("--start", ["-st"])
            {
                Description = "Source start offset."
            };

            var readCommand = new Command("read", "Read a physical disk or part of to an image file.");
            readCommand.Add(sourceArgument);
            readCommand.Add(destinationArgument);
            readCommand.Add(sizeOption);
            readCommand.Add(retriesOption);
            readCommand.Add(verifyOption);
            readCommand.Add(forceOption);
            readCommand.Add(startOption);
            readCommand.SetAction((ParseResult ctx) =>
            {
                var source = ctx.GetValue(sourceArgument);
                var destination = ctx.GetValue(destinationArgument);
                var size = ctx.GetValue(sizeOption);
                var retries = ctx.GetValue(retriesOption);
                var verify = ctx.GetValue(verifyOption);
                var force = ctx.GetValue(forceOption);
                var start = ctx.GetValue(startOption);
                return CommandHandler.Read(source, destination, size, retries, verify, force, start);
            });

            return readCommand;
        }

        private static Command CreateConvertCommand()
        {
            var sourceArgument = new Argument<string>("Source")
            {
                Description = "Path to source image file."
            };

            var destinationArgument = new Argument<string>("Destination")
            {
                Description = "Path to destination image file."
            };

            var sizeOption = new Option<string>("--size", ["-s"])
            {
                Description = "Size of image file transfer."
            };

            var verifyOption = new Option<bool>("--verify", ["-v"])
            {
                Description = "Verify data transferred."
            };

            var srcStartOption = new Option<long?>("--src-start", ["-ss"])
            {
                Description = "Source start offset."
            };

            var destStartOption = new Option<long?>("--dest-start", ["-ds"])
            {
                Description = "Destination start offset."
            };

            var convertCommand = new Command("convert", "Convert an image file. Obsolete, works same way af transfer and convert will be removed in a future release!");
            convertCommand.Add(sourceArgument);
            convertCommand.Add(destinationArgument);
            convertCommand.Add(sizeOption);
            convertCommand.Add(verifyOption);
            convertCommand.Add(srcStartOption);
            convertCommand.Add(destStartOption);
            convertCommand.SetAction((ParseResult ctx) =>
            {
                var source = ctx.GetValue(sourceArgument);
                var destination = ctx.GetValue(destinationArgument);
                var size = ctx.GetValue(sizeOption);
                var verify = ctx.GetValue(verifyOption);
                var srcStart = ctx.GetValue(srcStartOption);
                var destStart = ctx.GetValue(destStartOption);
                return CommandHandler.Transfer(source, destination, size, verify, srcStart, destStart);
            });

            return convertCommand;
        }

        private static Command CreateTransferCommand()
        {
            var sourceArgument = new Argument<string>("Source")
            {
                Description = "Path to source image file."
            };

            var destinationArgument = new Argument<string>("Destination")
            {
                Description = "Path to destination image file."
            };

            var sizeOption = new Option<string>("--size", ["-s"])
            {
                Description = "Size of image file transfer."
            };

            var verifyOption = new Option<bool>("--verify", ["-v"])
            {
                Description = "Verify data transferred."
            };

            var srcStartOption = new Option<long?>("--src-start", ["-ss"])
            {
                Description = "Source start offset."
            };

            var destStartOption = new Option<long?>("--dest-start", ["-ds"])
            {
                Description = "Destination start offset."
            };

            var transferCommand = new Command("transfer", "Transfer converts, imports or exports from an image file or part of to another.");
            transferCommand.Add(sourceArgument);
            transferCommand.Add(destinationArgument);
            transferCommand.Add(sizeOption);
            transferCommand.Add(verifyOption);
            transferCommand.Add(srcStartOption);
            transferCommand.Add(destStartOption);
            transferCommand.SetAction((ParseResult ctx) =>
            {
                var source = ctx.GetValue(sourceArgument);
                var destination = ctx.GetValue(destinationArgument);
                var size = ctx.GetValue(sizeOption);
                var verify = ctx.GetValue(verifyOption);
                var srcStart = ctx.GetValue(srcStartOption);
                var destStart = ctx.GetValue(destStartOption);
                return CommandHandler.Transfer(source, destination, size, verify, srcStart, destStart);
            });

            return transferCommand;
        }

        private static Command CreateFormatCommand()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical disk or image file."
            };

            var partitionTableArgument = new Argument<FormatType>("FormatType")
            {
                Description = "Type of disk to format."
            };

            var fileSystemArgument = new Argument<string>("FileSystem")
            {
                Description = "File system to format partition(s) created."
            };

            var fileSystemPathOption = new Option<string>("--file-system-path")
            {
                Description = "Path to file system file used to format (only for RDB and PiStorm)."
            };

            var sizeOption = new Option<string>("--size", ["-s"])
            {
                Description = "Size of disk to format."
            };

            var maxPartitionSizeOption = new Option<string>("--max-partition-size")
            {
                Description = "Max partition size for RDB disks."
            };

            var useExperimentalOption = new Option<bool>("--use-experimental")
            {
                Description = "Use PFS3 experimental partition sizes."
            };

            var kickstart31Option = new Option<bool>("--kickstart31")
            {
                Description = "Create Workbench partition size for Kickstart v3.1 or lower within first 4GB."
            };

            var command = new Command("format", "Format a physical disk or an image file.");
            command.Add(pathArgument);
            command.Add(partitionTableArgument);
            command.Add(fileSystemArgument);
            command.Add(fileSystemPathOption);
            command.Add(sizeOption);
            command.Add(maxPartitionSizeOption);
            command.Add(useExperimentalOption);
            command.Add(kickstart31Option);
            command.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var formatType = ctx.GetValue(partitionTableArgument);
                var fileSystem = ctx.GetValue(fileSystemArgument);
                var fileSystemPath = ctx.GetValue(fileSystemPathOption);
                var size = ctx.GetValue(sizeOption);
                var maxPartitionSize = ctx.GetValue(maxPartitionSizeOption);
                var useExperimental = ctx.GetValue(useExperimentalOption);
                var kickstart31 = ctx.GetValue(kickstart31Option);
                return CommandHandler.Format(path, formatType, fileSystem, fileSystemPath, size, maxPartitionSize, useExperimental, kickstart31);
            });

            return command;
        }

        private static Command CreateBlankCommand()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path image file."
            };

            var sizeArgument = new Argument<string>("Size")
            {
                Description = "Size of image file."
            };

            var compatibleSizeOption = new Option<bool>("--compatible", ["-c"])
            {
                Description = "Make size compatible by reducing it with 5%.",
                DefaultValueFactory = (ArgumentResult _) => false
            };

            var blankCommand = new Command("blank", "Create a blank image file.");
            blankCommand.Add(pathArgument);
            blankCommand.Add(sizeArgument);
            blankCommand.Add(compatibleSizeOption);
            blankCommand.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var size = ctx.GetValue(sizeArgument);
                var compatibleSize = ctx.GetValue(compatibleSizeOption);
                return CommandHandler.Blank(path, size, compatibleSize);
            });

            return blankCommand;
        }

        private static Command CreateOptimizeCommand()
        {
            var pathArgument = new Argument<string>("Source")
            {
                Description = "Path to image file."
            };

            var sizeOption = new Option<string>("--size", ["-s"])
            {
                Description = "Size to optimize to."
            };

            var partitionTableOption = new Option<PartitionTable?>("--partition-table", ["-pt"])
            {
                Description = "Optimize to size of partition table."
            };

            var optimizeCommand = new Command("optimize", "Optimize an image file size.");
            optimizeCommand.Add(pathArgument);
            optimizeCommand.Add(sizeOption);
            optimizeCommand.Add(partitionTableOption);
            optimizeCommand.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var size = ctx.GetValue(sizeOption);
                var partitionTable = ctx.GetValue(partitionTableOption);
                return CommandHandler.Optimize(path, size, partitionTable);
            });

            return optimizeCommand;
        }

        private static Command CreateCompareCommand()
        {
            var sourceArgument = new Argument<string>("Source")
            {
                Description = "Path to source physical disk or image file."
            };

            var destinationArgument = new Argument<string>("Destination")
            {
                Description = "Path to destination physical disk or image file."
            };

            var srcStartOffsetOption = new Option<long?>("--source-start")
            {
                Description = "Source start offset."
            };

            var destStartOffsetOption = new Option<long?>("--destination-start")
            {
                Description = "Destination start offset."
            };

            var sizeOption = new Option<string>("--size", ["-s"])
            {
                Description = "Size to verify."
            };

            var skipUnusedSectorsOption = new Option<bool?>("--skip-unused-sectors")
            {
                Description = "Skip unused sectors. Sectors containing zeroes are skipped to improve compare speed. However, not all operating systems or file systems support this. As an example ChromeOS images will not be able to recover properly if unused sectors are skipped!"
            };

            var retriesOption = new Option<int?>("--retries", ["-r"])
            {
                Description = "Number of retries to try read or write data."
            };

            var forceOption = new Option<bool?>("--force", ["-f"])
            {
                Description = "Force compare to ignore read errors."
            };

            var compareCommand = new Command("compare", "Compare image files and physical disks byte by byte.");
            compareCommand.Add(sourceArgument);
            compareCommand.Add(destinationArgument);
            compareCommand.Add(srcStartOffsetOption);
            compareCommand.Add(destStartOffsetOption);
            compareCommand.Add(skipUnusedSectorsOption);
            compareCommand.Add(sizeOption);
            compareCommand.Add(retriesOption);
            compareCommand.Add(forceOption);
            compareCommand.SetAction((ParseResult ctx) =>
            {
                var source = ctx.GetValue(sourceArgument);
                var destination = ctx.GetValue(destinationArgument);
                var srcStart = ctx.GetValue(srcStartOffsetOption);
                var destStart = ctx.GetValue(destStartOffsetOption);
                var size = ctx.GetValue(sizeOption);
                var skipUnusedSectors = ctx.GetValue(skipUnusedSectorsOption);
                var retries = ctx.GetValue(retriesOption);
                var force = ctx.GetValue(forceOption);
                return CommandHandler.Compare(source, destination, srcStart, destStart, size, skipUnusedSectors, retries, force);
            });

            return compareCommand;
        }

        private static Command CreateBlockCommand()
        {
            var command = new Command("block", "Block.");
            command.Add(CreateBlockReadCommand());
            command.Add(CreateBlockViewCommand());
            return command;
        }

        private static Command CreateBlockReadCommand()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical disk or image file."
            };

            var outputPathArgument = new Argument<string>("OutputPath")
            {
                Description = "Output path to write sectors."
            };

            var blockSizeOption = new Option<int>("--block-size", ["-bs"])
            {
                Description = "Block size.",
                DefaultValueFactory = (ArgumentResult _) => 512
            };

            var usedOption = new Option<bool>("--used", ["-u"])
            {
                Description = "Only used blocks."
            };

            var startOption = new Option<long?>("--start", ["-s"])
            {
                Description = "Start offset."
            };

            var endOption = new Option<long?>("--end", ["-e"])
            {
                Description = "End offset."
            };

            var blankCommand = new Command("read", "Read blocks from a physical disk or an image file to file per block.");
            blankCommand.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var outputPath = ctx.GetValue(outputPathArgument);
                var blockSize = ctx.GetValue(blockSizeOption);
                var used = ctx.GetValue(usedOption);
                var start = ctx.GetValue(startOption);
                var end = ctx.GetValue(endOption);
                return CommandHandler.BlockRead(path, outputPath, blockSize, used, start, end);
            });
            blankCommand.Add(pathArgument);
            blankCommand.Add(outputPathArgument);
            blankCommand.Add(blockSizeOption);
            blankCommand.Add(usedOption);
            blankCommand.Add(startOption);
            blankCommand.Add(endOption);

            return blankCommand;
        }

        private static Command CreateBlockViewCommand()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical disk or image file."
            };

            var blockSizeOption = new Option<int>("--block-size", ["-bs"])
            {
                Description = "Block size.",
                DefaultValueFactory = (ArgumentResult _) => 512
            };

            var startOption = new Option<long?>("--start", ["-s"])
            {
                Description = "Start offset."
            };

            var blankCommand = new Command("view", "View blocks from a physical disk or an image file as hex.");
            blankCommand.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var blockSize = ctx.GetValue(blockSizeOption);
                var start = ctx.GetValue(startOption);
                return CommandHandler.BlockView(path, blockSize, start);
            });
            blankCommand.Add(pathArgument);
            blankCommand.Add(blockSizeOption);
            blankCommand.Add(startOption);

            return blankCommand;
        }
    }
}