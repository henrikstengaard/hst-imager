using Hst.Imager.Core.Models;

namespace Hst.Imager.ConsoleApp;

using System.CommandLine;
using System.Linq;
using System.CommandLine.Parsing;
using Core.UaeMetadatas;

public static class FsCommandFactory
{
    public static Command CreateFsCommand()
    {
        var command = new Command("fs", "File system.");

        command.Add(CreateFsDir());
        command.Add(CreateFsCopy());
        command.Add(CreateFsMove());
        command.Add(CreateFsExtract());
        command.Add(CreateFsMkDir());
        command.Add(CreateFsDelete());
        command.Add(CreateFsAttrib());

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
            Description = "Type of UAE metadata to read for local directories. When specified, Amiga protection bits are shown as attributes unless attributes type is specified.",
            DefaultValueFactory = (ArgumentResult _) => UaeMetadata.None
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

    private static Command CreateFsMove()
    {
        var sourcePathArgument = new Argument<string>("SourcePath")
        {
            Description = "Path to source physical drive, image file or directory."
        };

        var destinationPathArgument = new Argument<string>("DestinationPath")
        {
            Description = "Path to destination physical drive, image file or directory."
        };

        var uaeMetadataOption = new Option<UaeMetadata>("--uaemetadata", "-uae")
        {
            Description = "Type of UAE metadata to read and write.",
            DefaultValueFactory = (ArgumentResult _) => UaeMetadata.UaeFsDb
        };

        var makeDirectoryOption = new Option<bool>("--makedir", ["-md"])
        {
            Description = "Make destination directory, if it does not exist.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Force overwriting any existing files."
        };

        var command = new Command("move", "Move a file or subdirectory from source to destination.");
        command.Aliases.Add("mv");
        command.SetAction((ParseResult ctx) =>
        {
            var srcPath = ctx.GetValue(sourcePathArgument);
            var destPath = ctx.GetValue(destinationPathArgument);
            var uaeMetadata = ctx.GetValue(uaeMetadataOption);
            var makeDir = ctx.GetValue(makeDirectoryOption);
            var force = ctx.GetValue(forceOption);
            return CommandHandler.FsMove(srcPath, destPath, uaeMetadata, makeDir, force);
        });
        command.Add(sourcePathArgument);
        command.Add(destinationPathArgument);
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
            Description = "Attributes of directories and files are not set when extracted.",
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

        var command = new Command("delete", "Delete a directory or file.");
        command.Aliases.Add("del");
        command.SetAction(ctx =>
        {
            var path = ctx.GetValue(pathArgument);
            return CommandHandler.FsDelete(path);
        });
        command.Add(pathArgument);

        return command;
    }

    private static Command CreateFsAttrib()
    {
        var pathArgument = new Argument<string>("Path")
        {
            Description = "Path to directory or file to set attributes for locally, in physical drive or image file."
        };

        var attributesArgument = new Argument<string[]>("Attributes")
        {
            Description = "Attributes to set: '+' adds, '-' removes and without '+' or '-' sets exactly, e.g. +s, -wd or rwed. Amiga protection bits 'hsparwed', Windows attributes 'rash' or Unix permissions 'rwx' or octal mode, e.g. 755.",
            Arity = ArgumentArity.ZeroOrMore
        };

        var recursiveOption = new Option<bool>("--recursive")
        {
            Description = "Recursively set attributes of files and sub-directories.",
            DefaultValueFactory = (ArgumentResult _) => false
        };

        var attributesOption = new Option<AttributesMode>("--attributes")
        {
            Description = "Type of attributes to set. Auto uses Amiga protection bits for Amiga file systems, Windows attributes for FAT and NTFS and Windows or Unix attributes depending on operating system for local directories or Amiga protection bits, if UAE metadata is specified.",
            DefaultValueFactory = (ArgumentResult _) => AttributesMode.Auto
        };

        var uaeMetadataOption = new Option<UaeMetadata>("--uaemetadata", ["-uae"])
        {
            Description = "Type of UAE metadata to read and write Amiga protection bits for local directories and files. When specified, Amiga protection bits are set unless attributes type is specified.",
            DefaultValueFactory = (ArgumentResult _) => UaeMetadata.None
        };

        // hidden option to parse "-h" as an attribute to remove hidden or held resident instead of showing help
        var removeHiddenOption = new Option<bool>("-h")
        {
            Hidden = true
        };

        var command = new Command("attrib", "Set attributes or protection bits of a directory or file.");
        command.Aliases.Add("attr");
        command.SetAction(ctx =>
        {
            var path = ctx.GetValue(pathArgument);
            var attributes = ctx.GetValue(removeHiddenOption)
                ? (ctx.GetValue(attributesArgument) ?? []).Concat(["-h"]).ToArray()
                : ctx.GetValue(attributesArgument);
            var recursive = ctx.GetValue(recursiveOption);
            var attributesMode = ctx.GetValue(attributesOption);
            var uaeMetadata = ctx.GetValue(uaeMetadataOption);
            return CommandHandler.FsAttrib(path, attributes, recursive, attributesMode, uaeMetadata);
        });
        command.Add(pathArgument);
        command.Add(attributesArgument);
        command.Add(recursiveOption);
        command.Add(attributesOption);
        command.Add(uaeMetadataOption);
        command.Add(removeHiddenOption);

        return command;
    }
}
