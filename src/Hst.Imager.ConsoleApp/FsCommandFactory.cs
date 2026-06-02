using Hst.Imager.Core.Models;

namespace Hst.Imager.ConsoleApp;

using System.CommandLine;
using System.CommandLine.Parsing;
using Core.UaeMetadatas;

public static class FsCommandFactory
{
    public static Command CreateFsCommand()
    {
        var command = new Command("fs", "File system.");

        command.Add(CreateFsDir());
        command.Add(CreateFsCopy());
        command.Add(CreateFsExtract());
        command.Add(CreateFsMkDir());
        command.Add(CreateFsDelete());

        return command;
    }

    private static Command CreateFsDir()
    {
        var pathArgument = new Argument<string>("DiskPath")
        {
            Description = "Path to physical drive or image file."
        };

        var recursiveOption = new Option<bool>("--recursive", ["-r"])
        {
            Description = "Recursively list sub-directories.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var uaeMetadataOption = new Option<UaeMetadata>("--uaemetadata", ["-uae"])
        {
            Description = "Type of UAE metadata to read.",
            DefaultValueFactory = (ArgumentResult _) => UaeMetadata.UaeFsDb
        };

        var attributesOption = new Option<AttributesMode>("--attributes", ["-a"])
        {
            Description = "Type of attributes to display.",
            DefaultValueFactory = (ArgumentResult _) => AttributesMode.Auto
        };

        var command = new Command("dir", "List files and subdirectories in a directory.");
        command.SetAction((ParseResult ctx) =>
        {
            var path = ctx.GetValue(pathArgument);
            var recursive = ctx.GetValue(recursiveOption);
            var uaeMetadata = ctx.GetValue(uaeMetadataOption);
            var attributes = ctx.GetValue(attributesOption);
            var format = ctx.GetValue(CommandFactory.FormatOption);
            return CommandHandler.FsDir(path, recursive, uaeMetadata, attributes, format);
        });
        command.Add(pathArgument);
        command.Add(recursiveOption);
        command.Add(uaeMetadataOption);
        command.Add(attributesOption);
        command.Add(CommandFactory.FormatOption);

        return command;
    }

    private static Command CreateFsCopy()
    {
        var sourcePathArgument = new Argument<string>("SourcePath")
        {
            Description = "Path to source physical drive or image file."
        };

        var destinationPathArgument = new Argument<string>("DestinationPath")
        {
            Description = "Path to destination physical drive or image file."
        };

        var recursiveOption = new Option<bool>("--recursive", ["-r"])
        {
            Description = "Recursively copy sub-directories.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var skipAttributesOption = new Option<bool>("--skip-attributes", ["-sa"])
        {
            Description = "Attributes of directories and files are not set when copied.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var quietOption = new Option<bool>("--quiet", ["-q"])
        {
            Description = "Quiet mode.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var uaeMetadataOption = new Option<UaeMetadata>("--uaemetadata", ["-uae"])
        {
            Description = "Type of UAE metadata to read and write.",
            DefaultValueFactory = (ArgumentResult _) => UaeMetadata.UaeFsDb
        };

        var makeDirectoryOption = new Option<bool>("--makedir", ["-md"])
        {
            Description = "Make destination directory, if it does not exist.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var forceOption = new Option<bool>("--force", ["-f"])
        {
            Description = "Force overwriting any existing files.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var command = new Command("copy", "Copy files or subdirectories from source to destination.");
        command.Aliases.Add("c");
        command.SetAction((ParseResult ctx) =>
        {
            var src = ctx.GetValue(sourcePathArgument);
            var dest = ctx.GetValue(destinationPathArgument);
            var recursive = ctx.GetValue(recursiveOption);
            var skipAttributes = ctx.GetValue(skipAttributesOption);
            var quiet = ctx.GetValue(quietOption);
            var uaeMetadata = ctx.GetValue(uaeMetadataOption);
            var makeDir = ctx.GetValue(makeDirectoryOption);
            var force = ctx.GetValue(forceOption);
            return CommandHandler.FsCopy(src, dest, recursive, skipAttributes, quiet, uaeMetadata, makeDir, force);
        });
        command.Add(sourcePathArgument);
        command.Add(destinationPathArgument);
        command.Add(recursiveOption);
        command.Add(skipAttributesOption);
        command.Add(quietOption);
        command.Add(uaeMetadataOption);
        command.Add(makeDirectoryOption);
        command.Add(forceOption);

        return command;
    }

    private static Command CreateFsExtract()
    {
        var sourcePathArgument = new Argument<string>("SourcePath")
        {
            Description = "Source path to extract from (lha, iso, adf file)."
        };

        var destinationPathArgument = new Argument<string>("DestinationPath")
        {
            Description = "Destination path to extract to (physical drive, image file or directory)."
        };

        var recursiveOption = new Option<bool>("--recursive", ["-r"])
        {
            Description = "Recursively extract sub-directories.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var skipAttributesOption = new Option<bool>("--skip-attributes", ["-sa"])
        {
            Description = "Attributes of directories and files are not set when copied.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var quietOption = new Option<bool>("--quiet", ["-q"])
        {
            Description = "Quiet mode.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var uaeMetadataOption = new Option<UaeMetadata>("--uaemetadata", ["-uae"])
        {
            Description = "Type of UAE metadata to read and write.",
            DefaultValueFactory = (ArgumentResult _) => UaeMetadata.UaeFsDb
        };

        var makeDirectoryOption = new Option<bool>("--makedir", ["-md"])
        {
            Description = "Make destination directory, if it does not exist.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var forceOption = new Option<bool>("--force", ["-f"])
        {
            Description = "Force overwriting any existing files.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var command = new Command("extract", "Extract files or subdirectories from source to destination.");
        command.Aliases.Add("x");
        command.SetAction((ParseResult ctx) =>
        {
            var src = ctx.GetValue(sourcePathArgument);
            var dest = ctx.GetValue(destinationPathArgument);
            var recursive = ctx.GetValue(recursiveOption);
            var skipAttributes = ctx.GetValue(skipAttributesOption);
            var quiet = ctx.GetValue(quietOption);
            var uaeMetadata = ctx.GetValue(uaeMetadataOption);
            var makeDir = ctx.GetValue(makeDirectoryOption);
            var force = ctx.GetValue(forceOption);
            return CommandHandler.FsExtract(src, dest, recursive, skipAttributes, quiet, uaeMetadata, makeDir, force);
        });
        command.Add(sourcePathArgument);
        command.Add(destinationPathArgument);
        command.Add(recursiveOption);
        command.Add(skipAttributesOption);
        command.Add(quietOption);
        command.Add(uaeMetadataOption);
        command.Add(makeDirectoryOption);
        command.Add(forceOption);

        return command;
    }

    private static Command CreateFsMkDir()
    {
        var pathArgument = new Argument<string>("Path")
        {
            Description = "Path to directory to create locally, in physical drive or image file."
        };

        var command = new Command("mkdir", "Create a directory.");
        command.SetAction((ParseResult ctx) =>
        {
            var path = ctx.GetValue(pathArgument);
            return CommandHandler.FsMkDir(path);
        });
        command.Add(pathArgument);

        return command;
    }

    private static Command CreateFsDelete()
    {
        var pathArgument = new Argument<string>("Path")
        {
            Description = "Path to directory or file to delete locally, in physical drive or image file."
        };

        var command = new Command("delete", "Create a directory.");
        command.Aliases.Add("del");
        command.SetAction((ParseResult ctx) =>
        {
            var path = ctx.GetValue(pathArgument);
            return CommandHandler.FsDelete(path);
        });
        command.Add(pathArgument);

        return command;
    }
}