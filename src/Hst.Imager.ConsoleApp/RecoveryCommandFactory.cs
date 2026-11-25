namespace Hst.Imager.ConsoleApp;

using System.CommandLine;
using System.CommandLine.Parsing;
using Core.Commands;
using Core.UaeMetadatas;
using Hst.Amiga.RigidDiskBlocks.Recovery;

public static class RecoveryCommandFactory
{
    /// <summary>
    /// Partition table types, which can be scanned and repaired.
    /// </summary>
    public enum RecoveryPartitionTableType
    {
        Auto,
        Rdb,
        Mbr,
        Gpt
    }

    public static Command CreateRecoveryCommand()
    {
        var command = new Command("recovery", "Disk and file system recovery.");

        command.Add(CreatePartitionTable());
        command.Add(CreateFs());

        return command;
    }

    private static Option<string> CreateRecoveryLogOption() => new("--recovery-log", ["-rl"])
    {
        Description =
            "Path to write detailed recovery log file to. Default is next to image file or in home directory for physical drives."
    };

    private static Option<bool> CreateYesOption() => new("--yes", ["-y"])
    {
        Description = "Confirm repair without asking. Repair can potentially corrupt data further and should be done on a backup.",
        DefaultValueFactory = (ArgumentResult _) => false
    };

    private static Option<RecoveryPartitionTableType> CreatePartitionTableTypeOption() => new("--type", ["-t"])
    {
        Description = "Type of partition table. Auto detects partition tables present.",
        DefaultValueFactory = (ArgumentResult _) => RecoveryPartitionTableType.Auto
    };

    public static PartitionTableType? ToPartitionTableType(RecoveryPartitionTableType type) => type switch
    {
        RecoveryPartitionTableType.Rdb => PartitionTableType.RigidDiskBlock,
        RecoveryPartitionTableType.Mbr => PartitionTableType.MasterBootRecord,
        RecoveryPartitionTableType.Gpt => PartitionTableType.GuidPartitionTable,
        _ => null
    };

    private static Command CreatePartitionTable()
    {
        var command = new Command("partitiontable", "Scan and repair partition tables (Rigid Disk Block, Master Boot Record and Guid Partition Table).");
        command.Aliases.Add("pt");

        command.Add(CreatePartitionTableScan());
        command.Add(CreatePartitionTableRepair());

        return command;
    }

    private static Command CreatePartitionTableScan()
    {
        var pathArgument = new Argument<string>("Path")
        {
            Description = "Path to physical drive or image file, optionally with partition table, e.g. disk.img\\rdb."
        };
        var typeOption = CreatePartitionTableTypeOption();
        var recoveryLogOption = CreateRecoveryLogOption();

        var command = new Command("scan", "Scan partition table for errors without writing any changes.");
        command.SetAction(async (ParseResult ctx) =>
        {
            await CommandHandler.RecoveryPartitionTableScan(ctx.GetValue(pathArgument),
                ToPartitionTableType(ctx.GetValue(typeOption)), ctx.GetValue(recoveryLogOption));
        });
        command.Add(pathArgument);
        command.Add(typeOption);
        command.Add(recoveryLogOption);

        return command;
    }

    private static Command CreatePartitionTableRepair()
    {
        var pathArgument = new Argument<string>("Path")
        {
            Description = "Path to physical drive or image file, optionally with partition table, e.g. disk.img\\rdb."
        };
        var typeOption = CreatePartitionTableTypeOption();
        var modeOption = new Option<RecoveryMode>("--mode", ["-m"])
        {
            Description = "Rigid disk block repair mode. Fix repairs existing rigid disk block, Rebuild creates new rigid disk block from file systems found on disk and Auto fixes, if rigid disk block is found, otherwise rebuilds.",
            DefaultValueFactory = (ArgumentResult _) => RecoveryMode.Auto
        };
        var yesOption = CreateYesOption();
        var recoveryLogOption = CreateRecoveryLogOption();

        var command = new Command("repair", "Repair partition table. Repair writes to disk and should be done on a backup.");
        command.SetAction(async (ParseResult ctx) =>
        {
            await CommandHandler.RecoveryPartitionTableRepair(ctx.GetValue(pathArgument),
                ToPartitionTableType(ctx.GetValue(typeOption)), ctx.GetValue(modeOption), ctx.GetValue(yesOption),
                ctx.GetValue(recoveryLogOption));
        });
        command.Add(pathArgument);
        command.Add(typeOption);
        command.Add(modeOption);
        command.Add(yesOption);
        command.Add(recoveryLogOption);

        return command;
    }

    private static Command CreateFs()
    {
        var command = new Command("fs", "Scan, repair and salvage Amiga file systems (FFS, PFS3 and SFS).");

        command.Add(CreateFsScan());
        command.Add(CreateFsRepair());
        command.Add(CreateFsSalvage());

        return command;
    }

    private static Argument<string> CreatePartitionPathArgument() => new("Path")
    {
        Description = "Path to partition in physical drive or image file, e.g. disk.img\\rdb\\dh0 or disk.img\\rdb\\1, or path to adf file."
    };

    private static Command CreateFsScan()
    {
        var pathArgument = CreatePartitionPathArgument();
        var recoveryLogOption = CreateRecoveryLogOption();

        var command = new Command("scan", "Scan file system for errors without writing any changes.");
        command.SetAction(async (ParseResult ctx) =>
        {
            await CommandHandler.RecoveryFsScan(ctx.GetValue(pathArgument), ctx.GetValue(recoveryLogOption));
        });
        command.Add(pathArgument);
        command.Add(recoveryLogOption);

        return command;
    }

    private static Command CreateFsRepair()
    {
        var pathArgument = CreatePartitionPathArgument();
        var yesOption = CreateYesOption();
        var recoveryLogOption = CreateRecoveryLogOption();

        var command = new Command("repair", "Repair file system. Repair writes to disk and should be done on a backup.");
        command.SetAction(async (ParseResult ctx) =>
        {
            await CommandHandler.RecoveryFsRepair(ctx.GetValue(pathArgument), ctx.GetValue(yesOption),
                ctx.GetValue(recoveryLogOption));
        });
        command.Add(pathArgument);
        command.Add(yesOption);
        command.Add(recoveryLogOption);

        return command;
    }

    private static Command CreateFsSalvage()
    {
        var pathArgument = CreatePartitionPathArgument();
        var outputPathArgument = new Argument<string>("OutputPath")
        {
            Description = "Path to directory to write salvaged directories and files to."
        };
        var uaeMetadataOption = new Option<UaeMetadata>("--uaemetadata", ["-uae"])
        {
            Description = "Type of UAE metadata to write for Amiga names, protection bits and comments.",
            DefaultValueFactory = (ArgumentResult _) => UaeMetadata.UaeFsDb
        };
        var noOrphansOption = new Option<bool>("--no-orphans", ["-no"])
        {
            Description = "Don't salvage orphaned directories and files, which can't be reached from root directory.",
            DefaultValueFactory = (ArgumentResult _) => false
        };
        var recoveryLogOption = CreateRecoveryLogOption();

        var command = new Command("salvage", "Salvage directories and files from file system to a directory. File system is never written to.");
        command.SetAction(async (ParseResult ctx) =>
        {
            await CommandHandler.RecoveryFsSalvage(ctx.GetValue(pathArgument), ctx.GetValue(outputPathArgument),
                ctx.GetValue(uaeMetadataOption), !ctx.GetValue(noOrphansOption), ctx.GetValue(recoveryLogOption));
        });
        command.Add(pathArgument);
        command.Add(outputPathArgument);
        command.Add(uaeMetadataOption);
        command.Add(noOrphansOption);
        command.Add(recoveryLogOption);

        return command;
    }
}
