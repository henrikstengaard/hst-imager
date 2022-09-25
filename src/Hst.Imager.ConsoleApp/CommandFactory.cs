﻿namespace Hst.Imager.ConsoleApp
{
    using System.CommandLine;
    using System.IO;

    public static class CommandFactory
    {
        public static readonly Option<FileInfo> LogFileOption = new(
            new[] { "--log-file" },
            description: "Write log file.");

        public static readonly Option<bool> VerboseOption = new(
            new[] { "--verbose" },
            description: "Verbose output.");

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
            rootCommand.AddCommand(CreateInfoCommand());
            rootCommand.AddCommand(CreateListCommand());
            rootCommand.AddCommand(CreateOptimizeCommand());
            rootCommand.AddCommand(CreateReadCommand());
            rootCommand.AddCommand(CreateScriptCommand());
            rootCommand.AddCommand(CreateSectorCommand());
            rootCommand.AddCommand(CreateVerifyCommand());
            rootCommand.AddCommand(CreateWriteCommand());
            rootCommand.AddCommand(MbrCommandFactory.CreateMbrCommand());
            rootCommand.AddCommand(RdbCommandFactory.CreateRdbCommand());

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

            var command = new Command("info", "Show info about physical drive or image file.");
            command.AddArgument(pathArgument);
            command.SetHandler(CommandHandler.Info, pathArgument);

            return command;
        }

        public static Command CreateListCommand()
        {
            var listCommand = new Command("list", "List physical drives.");
            listCommand.SetHandler(CommandHandler.List);

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

            var writeCommand = new Command("write", "Write image file to physical drive.");
            writeCommand.AddArgument(sourceArgument);
            writeCommand.AddArgument(destinationArgument);
            writeCommand.AddOption(sizeOption);
            writeCommand.SetHandler(CommandHandler.Write, sourceArgument, destinationArgument, sizeOption);

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

            var readCommand = new Command("read", "Write physical drive to image file.");
            readCommand.AddArgument(sourceArgument);
            readCommand.AddArgument(destinationArgument);
            readCommand.AddOption(sizeOption);
            readCommand.SetHandler(CommandHandler.Read, sourceArgument, destinationArgument, sizeOption);

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

            var convertCommand = new Command("convert", "Convert image file.");
            convertCommand.AddArgument(sourceArgument);
            convertCommand.AddArgument(destinationArgument);
            convertCommand.AddOption(sizeOption);
            convertCommand.SetHandler(CommandHandler.Convert, sourceArgument, destinationArgument, sizeOption);

            return convertCommand;
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
                getDefaultValue: () => true);

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

            var convertCommand = new Command("optimize", "Optimize image file size.");
            convertCommand.AddArgument(pathArgument);
            convertCommand.AddOption(sizeOption);
            convertCommand.SetHandler(CommandHandler.Optimize, pathArgument, sizeOption);

            return convertCommand;
        }

        public static Command CreateVerifyCommand()
        {
            var sourceArgument = new Argument<string>(
                name: "Source",
                description: "Path to source image file.");

            var destinationArgument = new Argument<string>(
                name: "Destination",
                description: "Path to destination image file.");

            var sizeOption = new Option<string>(
                new[] { "--size", "-s" },
                description: "Size to verify.");

            var convertCommand = new Command("verify", "Verify source and destination.");
            convertCommand.AddArgument(sourceArgument);
            convertCommand.AddArgument(destinationArgument);
            convertCommand.AddOption(sizeOption);
            convertCommand.SetHandler(CommandHandler.Verify, sourceArgument, destinationArgument, sizeOption);

            return convertCommand;
        }

        public static Command CreateSectorCommand()
        {
            var command = new Command("sector", "Sector.");
            command.AddCommand(CreateSectorExtractCommand());
            return command;
        }

        public static Command CreateSectorExtractCommand()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var outputPathArgument = new Argument<string>(
                name: "OutputPath",
                description: "Output path to write sectors.");

            var sectorSizeOption = new Option<int>(
                new[] { "--sector-size", "-ss" },
                description: "Sector size", getDefaultValue: () => 512);

            var startOption = new Option<long?>(
                new[] { "--start", "-s" },
                description: "Start offset");

            var endOption = new Option<long?>(
                new[] { "--end", "-e" },
                description: "End offset");

            var blankCommand = new Command("extract", "Extract sectors.");
            blankCommand.SetHandler(CommandHandler.SectorExtract, pathArgument, outputPathArgument, sectorSizeOption,
                startOption, endOption);
            blankCommand.AddArgument(pathArgument);
            blankCommand.AddArgument(outputPathArgument);
            blankCommand.AddOption(sectorSizeOption);

            return blankCommand;
        }
    }
}