namespace Hst.Imager.ConsoleApp
{
    using System.CommandLine;

    public static class RdbCommandFactory
    {
        public static Command CreateRdbCommand()
        {
            var rdbCommand = new Command("rdb", "Rigid Disk Block.");

            rdbCommand.AddCommand(CreateRdbInfo());
            rdbCommand.AddCommand(CreateRdbInit());
            rdbCommand.AddCommand(CreateRdbResize());
            rdbCommand.AddCommand(CreateRdbFs());
            rdbCommand.AddCommand(CreateRdbPart());
            rdbCommand.AddCommand(CreateRdbUpdate());
            rdbCommand.AddCommand(CreateRdbBackup());
            rdbCommand.AddCommand(CreateRdbRestore());

            return rdbCommand;
        }

        private static Command CreateRdbInfo()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var showUnallocatedOption = new Option<bool>(
                new[] { "--unallocated", "-u" },
                description: "Show unallocated.",
                getDefaultValue: () => true);
            
            var rdbInfoCommand = new Command("info", "Display info about Rigid Disk Block.");
            rdbInfoCommand.SetHandler(CommandHandler.RdbInfo, pathArgument, showUnallocatedOption);
            rdbInfoCommand.AddArgument(pathArgument);
            rdbInfoCommand.AddOption(showUnallocatedOption);

            return rdbInfoCommand;
        }

        private static Command CreateRdbInit()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var sizeOption = new Option<string>(
                new[] { "--size", "-s" },
                description: "Size of disk.");

            var nameOption = new Option<string>(
                new[] { "--name", "-n" },
                "Name of disk.");

            var rdbBlockLoOption = new Option<int>(
                new[] { "--rdb-block-lo" },
                "Low block reserved for Rigid Disk Block (0-15).");

            var chsOption = new Option<string>(
                new[] { "-chs" },
                description: "Initialize from cylinders, heads and sectors.");

            var rdbInitCommand = new Command("initialize", "Initialize disk with empty Rigid Disk Block.");
            rdbInitCommand.AddAlias("init");
            rdbInitCommand.SetHandler(CommandHandler.RdbInit, pathArgument, sizeOption, nameOption,
                chsOption, rdbBlockLoOption);
            rdbInitCommand.AddArgument(pathArgument);
            rdbInitCommand.AddOption(sizeOption);
            rdbInitCommand.AddOption(nameOption);
            rdbInitCommand.AddOption(chsOption);
            rdbInitCommand.AddOption(rdbBlockLoOption);

            return rdbInitCommand;
        }

        private static Command CreateRdbResize()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var sizeOption = new Option<string>(
                new[] { "--size", "-s" },
                description: "Size of Rigid Disk Block.");

            var command = new Command("resize", "Resize Rigid Disk Block.");
            command.SetHandler(CommandHandler.RdbResize, pathArgument, sizeOption);
            command.AddArgument(pathArgument);
            command.AddOption(sizeOption);

            return command;
        }

        private static Command CreateRdbFs()
        {
            var rdbFsCommand = new Command("filesystem", "File system.");
            rdbFsCommand.AddAlias("fs");
            rdbFsCommand.AddCommand(CreateRdbFsAdd());
            rdbFsCommand.AddCommand(CreateRdbFsDel());
            rdbFsCommand.AddCommand(CreateRdbFsExport());
            rdbFsCommand.AddCommand(CreateRdbFsImport());
            rdbFsCommand.AddCommand(CreateRdbFsUpdate());

            return rdbFsCommand;
        }

        private static Command CreateRdbFsAdd()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var fileSystemPathArgument = new Argument<string>(
                name: "FileSystemPath",
                description: "Path to file system to add.");

            var dosTypeArgument = new Argument<string>(
                name: "DosType",
                description: "Dos Type for file system (e.g. DOS3, PDS3).");

            var fileSystemNameOption = new Option<string>(
                new[] { "--name", "-n" },
                description: "Name of file system.");

            var versionOption = new Option<int?>(
                new[] { "--version", "-v" },
                description: "Version of file system (number before . in version).");

            var revisionOption = new Option<int?>(
                new[] { "--revision", "-r" },
                description: "Revision of file system (number after . in version).");
            
            var rdbFsAddCommand = new Command("add", "Add file system.");
            rdbFsAddCommand.SetHandler(CommandHandler.RdbFsAdd, pathArgument, fileSystemPathArgument, 
                dosTypeArgument, fileSystemNameOption, versionOption, revisionOption);
            rdbFsAddCommand.AddArgument(pathArgument);
            rdbFsAddCommand.AddArgument(fileSystemPathArgument);
            rdbFsAddCommand.AddArgument(dosTypeArgument);
            rdbFsAddCommand.AddOption(fileSystemNameOption);
            rdbFsAddCommand.AddOption(versionOption);
            rdbFsAddCommand.AddOption(revisionOption);

            return rdbFsAddCommand;
        }

        private static Command CreateRdbFsDel()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var fileSystemNumber = new Argument<int>(
                name: "FileSystemNumber",
                description: "File system number to delete.");

            var rdbFsDelCommand = new Command("delete", "Delete file system.");
            rdbFsDelCommand.AddAlias("del");
            rdbFsDelCommand.SetHandler(CommandHandler.RdbFsDel, pathArgument, fileSystemNumber);
            rdbFsDelCommand.AddArgument(pathArgument);
            rdbFsDelCommand.AddArgument(fileSystemNumber);

            return rdbFsDelCommand;
        }

        private static Command CreateRdbFsImport()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var fileSystemPathArgument = new Argument<string>(
                name: "FileSystemPath",
                description: "Path to file system to add.");

            var dosTypeOption = new Option<string>(
                new[] { "--dos-type", "-dt" },
                description: "Dos Type for file system (e.g. DOS3, PDS3).");

            var fileSystemNameOption = new Option<string>(
                new[] { "--name", "-n" },
                description: "Name of file system.");

            var command = new Command("import", "Import file systems from physical drive, image file (supports .adf).");
            command.SetHandler(CommandHandler.RdbFsImport, pathArgument, fileSystemPathArgument, dosTypeOption,
                fileSystemNameOption);
            command.AddArgument(pathArgument);
            command.AddArgument(fileSystemPathArgument);
            command.AddOption(dosTypeOption);
            command.AddOption(fileSystemNameOption);

            return command;
        }

        private static Command CreateRdbFsExport()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var fileSystemNumber = new Argument<int>(
                name: "FileSystemNumber",
                description: "File system number to delete.");

            var fileSystemPathArgument = new Argument<string>(
                name: "FileSystemPath",
                description: "Path to file system.");

            var command = new Command("export", "Export file system to a file.");
            command.SetHandler(CommandHandler.RdbFsExport, pathArgument, fileSystemNumber, fileSystemPathArgument);
            command.AddArgument(pathArgument);
            command.AddArgument(fileSystemNumber);
            command.AddArgument(fileSystemPathArgument);

            return command;
        }

        private static Command CreateRdbFsUpdate()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var fileSystemNumber = new Argument<int>(
                name: "FileSystemNumber",
                description: "File system number to delete.");

            var dosTypeArgument = new Option<string>(
                new[] { "--dos-type", "-dt" },
                description: "Dos type for file system (e.g. DOS3, PDS3).");

            var fileSystemNameOption = new Option<string>(
                new[] { "--name", "-n" },
                description: "Name of file system.");

            var fileSystemPathOption = new Option<string>(
                new[] { "--path", "-p" },
                description: "Path to file system.");

            var command = new Command("update", "Update file system.");
            command.SetHandler(CommandHandler.RdbFsUpdate, pathArgument, fileSystemNumber, dosTypeArgument,
                fileSystemNameOption, fileSystemPathOption);
            command.AddArgument(pathArgument);
            command.AddArgument(fileSystemNumber);
            command.AddOption(dosTypeArgument);
            command.AddOption(fileSystemNameOption);
            command.AddOption(fileSystemPathOption);
            command.AddValidator(validate =>
            {
                if (validate.FindResultFor(dosTypeArgument) is null &&
                    validate.FindResultFor(fileSystemNameOption) is null &&
                    validate.FindResultFor(fileSystemPathOption) is null)
                {
                    validate.ErrorMessage = "At least one option must be specified";
                }
            });
            return command;
        }

        private static Command CreateRdbPart()
        {
            var rdbPartCommand = new Command("part", "Partition.");

            rdbPartCommand.AddCommand(CreateRdbPartAdd());
            rdbPartCommand.AddCommand(CreateRdbPartUpdate());
            rdbPartCommand.AddCommand(CreateRdbPartDel());
            rdbPartCommand.AddCommand(CreateRdbPartCopy());
            rdbPartCommand.AddCommand(CreateRdbPartExport());
            rdbPartCommand.AddCommand(CreateRdbPartImport());
            rdbPartCommand.AddCommand(CreateRdbPartKill());
            rdbPartCommand.AddCommand(CreateRdbPartMove());
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
                description: "DOS type for the partition to use (e.g. DOS3, PFS3).");

            var nameArgument = new Argument<string>(
                name: "Name",
                description: "Name of the partition (e.g. DH0).");

            var sizeArgument = new Argument<string>(
                name: "Size",
                description: "Size of the partition.");

            var reservedOption = new Option<uint?>(
                new[] { "--reserved", "-r" },
                description: "Set reserved blocks at start of partition.");

            var preAllocOption = new Option<uint?>(
                new[] { "--pre-alloc", "-pa" },
                description: "Set reserved blocks at end of partition");

            var buffersOption = new Option<uint?>(
                new[] { "--buffers", "-bu" },
                description: "Set buffers");

            var maxTransferOption = new Option<string>(
                new[] { "--max-transfer", "-mt" },
                description: "Max transfer (integer or hex value e.g. 0x1fe00)");

            var maskOption = new Option<string>(
                new[] { "--mask", "-ma" },
                description: "Mask (integer or hex value e.g. 0x7ffffffe)");

            var noMountOption = new Option<bool>(
                new[] { "--no-mount", "-nm" },
                description: "Set partition to no mount, partition is not mounted on boot.",
                getDefaultValue: () => false);

            var bootableOption = new Option<bool>(
                new[] { "--bootable", "-b" },
                description: "Set bootable.",
                getDefaultValue: () => false);

            var bootPriorityOption = new Option<int?>(
                new[] { "--boot-priority", "-bp" },
                description: "Set boot priority.");

            var blockSizeOption = new Option<int?>(
                new[] { "--block-size", "-bs" },
                description: "Block size for the partition.",
                getDefaultValue: () => 512);

            var useExperimentalOption = new Option<bool>(
                ["--use-experimental"],
                description: "Use experimental partition sizes.");

            var rdbPartAddCommand = new Command("add", "Add partition.");
            rdbPartAddCommand.SetHandler(async (context) =>
            {
                var path = context.ParseResult.GetValueForArgument(pathArgument);
                var name = context.ParseResult.GetValueForArgument(nameArgument);
                var dosType = context.ParseResult.GetValueForArgument(dosTypeArgument);
                var size = context.ParseResult.GetValueForArgument(sizeArgument);
                var reserved = context.ParseResult.GetValueForOption(reservedOption);
                var preAlloc = context.ParseResult.GetValueForOption(preAllocOption);
                var buffers = context.ParseResult.GetValueForOption(buffersOption);
                var maxTransfer = context.ParseResult.GetValueForOption(maxTransferOption);
                var mask = context.ParseResult.GetValueForOption(maskOption);
                var noMount = context.ParseResult.GetValueForOption(noMountOption);
                var bootable = context.ParseResult.GetValueForOption(bootableOption);
                var bootPriority = context.ParseResult.GetValueForOption(bootPriorityOption);
                var blockSize = context.ParseResult.GetValueForOption(blockSizeOption);
                var useExperimental = context.ParseResult.GetValueForOption(useExperimentalOption);

                await CommandHandler.RdbPartAdd(path, name, dosType, size, reserved, preAlloc, buffers, maxTransfer, mask,
                    noMount, bootable, bootPriority, blockSize, useExperimental);
            });

            rdbPartAddCommand.AddArgument(pathArgument);
            rdbPartAddCommand.AddArgument(nameArgument);
            rdbPartAddCommand.AddArgument(dosTypeArgument);
            rdbPartAddCommand.AddArgument(sizeArgument);
            rdbPartAddCommand.AddOption(reservedOption);
            rdbPartAddCommand.AddOption(preAllocOption);
            rdbPartAddCommand.AddOption(buffersOption);
            rdbPartAddCommand.AddOption(maxTransferOption);
            rdbPartAddCommand.AddOption(maskOption);
            rdbPartAddCommand.AddOption(noMountOption);
            rdbPartAddCommand.AddOption(bootableOption);
            rdbPartAddCommand.AddOption(bootPriorityOption);
            rdbPartAddCommand.AddOption(blockSizeOption);
            rdbPartAddCommand.AddOption(useExperimentalOption);

            return rdbPartAddCommand;
        }

        private static Command CreateRdbPartUpdate()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var partitionNumberArgument = new Argument<int>(
                name: "PartitionNumber",
                description: "Partition number to update.");

            var dosTypeOption = new Option<string>(
                new[] { "--dos-type", "-dt" },
                description: "DOS type for the partition to use (e.g. DOS3, PFS3).");

            var nameOption = new Option<string>(
                new[] { "--name", "-n" },
                description: "Name of the partition (e.g. DH0).");

            var reservedOption = new Option<int?>(
                new[] { "--reserved", "-r" },
                description: "Reserved blocks at start of partition.");

            var preAllocOption = new Option<int?>(
                new[] { "--pre-alloc", "-pa" },
                description: "Reserved blocks at end of partition");

            var buffersOption = new Option<int?>(
                new[] { "--buffers", "-bu" },
                description: "Buffers");

            var maxTransferOption = new Option<string>(
                new[] { "--max-transfer", "-mt" },
                description: "Max transfer (integer or hex value e.g. 0x1fe00)");

            var maskOption = new Option<string>(
                new[] { "--mask", "-ma" },
                description: "Mask (integer or hex value e.g. 0x7ffffffe)");

            var noMountOption = new Option<BoolType?>(
                new[] { "--no-mount", "-nm" },
                description: "Set no mount for partition (partition is not mounted on boot).");

            var bootableOption = new Option<BoolType?>(
                new[] { "--bootable", "-b" },
                description: "Set bootable for partition.");

            var bootPriorityOption = new Option<int?>(
                new[] { "--boot-priority", "-bp" },
                description: "Set boot priority (controls order of partitions to boot, lowest is booted first).");

            var fileSystemBlockSizeOption = new Option<int?>(
                new[] { "--block-size", "-bs" },
                description: "File system block size for the partition.");

            var command = new Command("update", "Update partition.");
            command.SetHandler(async context =>
            {
                var path = context.ParseResult.GetValueForArgument(pathArgument);
                var partitionNumber = context.ParseResult.GetValueForArgument(partitionNumberArgument);
                var name = context.ParseResult.GetValueForOption(nameOption);
                var dosType = context.ParseResult.GetValueForOption(dosTypeOption);
                var reserved = context.ParseResult.GetValueForOption(reservedOption);
                var preAlloc = context.ParseResult.GetValueForOption(preAllocOption);
                var buffers = context.ParseResult.GetValueForOption(buffersOption);
                var maxTransfer = context.ParseResult.GetValueForOption(maxTransferOption);
                var mask = context.ParseResult.GetValueForOption(maskOption);
                var noMount = context.ParseResult.GetValueForOption(noMountOption);
                var bootable = context.ParseResult.GetValueForOption(bootableOption);
                var bootPriority = context.ParseResult.GetValueForOption(bootPriorityOption);
                var fileSystemBlockSize = context.ParseResult.GetValueForOption(fileSystemBlockSizeOption);

                await CommandHandler.RdbPartUpdate(path, partitionNumber, name, dosType, reserved, preAlloc, buffers,
                    maxTransfer,
                    mask, noMount.HasValue ? noMount.Value == BoolType.True : null,
                    bootable.HasValue ? bootable.Value == BoolType.True : null, bootPriority, fileSystemBlockSize);
            });

            command.AddArgument(pathArgument);
            command.AddArgument(partitionNumberArgument);
            command.AddOption(nameOption);
            command.AddOption(dosTypeOption);
            command.AddOption(reservedOption);
            command.AddOption(preAllocOption);
            command.AddOption(buffersOption);
            command.AddOption(maxTransferOption);
            command.AddOption(maskOption);
            command.AddOption(noMountOption);
            command.AddOption(bootableOption);
            command.AddOption(bootPriorityOption);
            command.AddOption(fileSystemBlockSizeOption);
            command.AddValidator(validate =>
            {
                if (validate.FindResultFor(nameOption) is null &&
                    validate.FindResultFor(dosTypeOption) is null &&
                    validate.FindResultFor(reservedOption) is null &&
                    validate.FindResultFor(preAllocOption) is null &&
                    validate.FindResultFor(buffersOption) is null &&
                    validate.FindResultFor(maxTransferOption) is null &&
                    validate.FindResultFor(maskOption) is null &&
                    validate.FindResultFor(noMountOption) is null &&
                    validate.FindResultFor(bootableOption) is null &&
                    validate.FindResultFor(bootPriorityOption) is null &&
                    validate.FindResultFor(fileSystemBlockSizeOption) is null)
                {
                    validate.ErrorMessage = "At least one option must be specified";
                }
            });
            return command;
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

        private static Command CreateRdbPartCopy()
        {
            var sourcePathArgument = new Argument<string>(
                name: "SourcePath",
                description: "Path to source physical drive or image file.");

            var partitionNumber = new Argument<int>(
                name: "PartitionNumber",
                description: "Partition number to copy.");

            var destinationPathArgument = new Argument<string>(
                name: "DestinationPath",
                description: "Path to destination physical drive or image file.");

            var nameOption = new Option<string>(
                new[] { "--name", "-n" },
                description: "Name of the partition (e.g. DH0).");

            var rdbPartDelCommand = new Command("copy", "Copy partition from a physical drive or image file.");
            rdbPartDelCommand.SetHandler(CommandHandler.RdbPartCopy, sourcePathArgument, partitionNumber,
                destinationPathArgument, nameOption);
            rdbPartDelCommand.AddArgument(sourcePathArgument);
            rdbPartDelCommand.AddArgument(partitionNumber);
            rdbPartDelCommand.AddArgument(destinationPathArgument);
            rdbPartDelCommand.AddOption(nameOption);

            return rdbPartDelCommand;
        }

        private static Command CreateRdbPartExport()
        {
            var sourcePathArgument = new Argument<string>(
                name: "SourcePath",
                description: "Path to source physical drive or image file.");

            var partitionNumber = new Argument<int>(
                name: "PartitionNumber",
                description: "Partition number to export.");

            var destinationPathArgument = new Argument<string>(
                name: "DestinationPath",
                description: "Path to destination file (e.g. DH0.hdf).");

            var command = new Command("export", "Export partition to a hard file (e.g. DH0.hdf).");
            command.SetHandler(CommandHandler.RdbPartExport, sourcePathArgument, partitionNumber,
                destinationPathArgument);
            command.AddArgument(sourcePathArgument);
            command.AddArgument(partitionNumber);
            command.AddArgument(destinationPathArgument);

            return command;
        }

        private static Command CreateRdbPartImport()
        {
            var sourcePathArgument = new Argument<string>(
                name: "SourcePath",
                description: "Path to source hard file (e.g. DH0.hdf).");

            var destinationPathArgument = new Argument<string>(
                name: "DestinationPath",
                description: "Path to destination physical drive or image file.");

            var dosTypeArgument = new Argument<string>(
                name: "DosType",
                description: "DOS type for the partition to use (e.g. DOS3, PFS3).");

            var nameArgument = new Argument<string>(
                name: "Name",
                description: "Name of the partition (e.g. DH0).");

            var fileSystemBlockSizeOption = new Option<int>(
                new[] { "--block-size", "-bs" },
                description: "File system block size for the partition.",
                getDefaultValue: () => 512);

            var bootableOption = new Option<bool>(
                new[] { "--bootable", "-b" },
                description: "Set bootable.",
                getDefaultValue: () => false);

            var command = new Command("import", "Import partition from a hard file (e.g. DH0.hdf).");
            command.SetHandler(CommandHandler.RdbPartImport, sourcePathArgument, destinationPathArgument, nameArgument,
                dosTypeArgument, fileSystemBlockSizeOption, bootableOption);
            command.AddArgument(sourcePathArgument);
            command.AddArgument(destinationPathArgument);
            command.AddArgument(nameArgument);
            command.AddArgument(dosTypeArgument);
            command.AddOption(fileSystemBlockSizeOption);
            command.AddOption(bootableOption);

            return command;
        }

        private static Command CreateRdbPartKill()
        {
            var sourcePathArgument = new Argument<string>(
                name: "SourcePath",
                description: "Path to source physical drive or image file.");

            var partitionNumber = new Argument<int>(
                name: "PartitionNumber",
                description: "Partition number to export.");

            var hexBootBytesArgument = new Argument<string>(
                name: "HexBootBytes",
                description: "Boot bytes in hex to write (e.g. 00000000).");

            var command = new Command("kill", "Kill partition.");
            command.SetHandler(CommandHandler.RdbPartKill, sourcePathArgument, partitionNumber, hexBootBytesArgument);
            command.AddArgument(sourcePathArgument);
            command.AddArgument(partitionNumber);
            command.AddArgument(hexBootBytesArgument);

            return command;
        }

        private static Command CreateRdbPartMove()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var partitionNumber = new Argument<int>(
                name: "PartitionNumber",
                description: "Partition number to move.");

            var startCylinder = new Argument<uint>(
                name: "StartCylinder",
                description: "Start cylinder to move partition to.");

            var command = new Command("move", "Move partition.");
            command.SetHandler(CommandHandler.RdbPartMove, pathArgument, partitionNumber, startCylinder);
            command.AddArgument(pathArgument);
            command.AddArgument(partitionNumber);
            command.AddArgument(startCylinder);

            return command;
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
                description: "Name of the volume (e.g. Workbench).");

            var nonRdbOption = new Option<bool>(
                new[] { "--non-rdb" },
                description: "Set non-RDB.",
                getDefaultValue: () => false);
            
            var chsOption = new Option<string>(
                new[] { "-chs" },
                description: "Format from cylinders, heads and sectors. Optional for non-RDB partition.");

            var dosTypeOption = new Option<string>(
                new[] { "--dos-type", "-dt" },
                description: "DOS type for the partition to use (e.g. DOS3, PFS3). Required for non-RDB partition.");
            
            var rdbPartFormatCommand = new Command("format", "Format partition.");
            rdbPartFormatCommand.SetHandler(CommandHandler.RdbPartFormat, pathArgument, partitionNumber,
                volumeNameArgument, nonRdbOption, chsOption, dosTypeOption);
            rdbPartFormatCommand.AddArgument(pathArgument);
            rdbPartFormatCommand.AddArgument(partitionNumber);
            rdbPartFormatCommand.AddArgument(volumeNameArgument);
            rdbPartFormatCommand.AddOption(nonRdbOption);
            rdbPartFormatCommand.AddOption(chsOption);
            rdbPartFormatCommand.AddOption(dosTypeOption);

            return rdbPartFormatCommand;
        }
        
        private static Command CreateRdbUpdate()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");
            
            var flagsOption = new Option<uint?>(
                new[] { "--flags", "-f" },
                description: "Flags.");

            var hostIdOption = new Option<uint?>(
                new[] { "--host-id", "-h" },
                description: "Host id.");
            
            var diskProductOption = new Option<string>(
                new[] { "--disk-product", "-dp" },
                description: "Disk product.");

            var diskRevisionOption = new Option<string>(
                new[] { "--disk-revision", "-dr" },
                description: "Disk revision.");
            
            var diskVendorOption = new Option<string>(
                new[] { "--disk-vendor", "-dv" },
                description: "Disk vendor.");
            
            var command = new Command("update", "Update Rigid Disk Block.");
            command.SetHandler(CommandHandler.RdbUpdate, pathArgument, flagsOption, hostIdOption,
                diskProductOption, diskRevisionOption, diskVendorOption);
            command.AddArgument(pathArgument);
            command.AddOption(flagsOption);
            command.AddOption(hostIdOption);
            command.AddOption(diskProductOption);
            command.AddOption(diskRevisionOption);
            command.AddOption(diskVendorOption);
            command.AddValidator(validate =>
            {
                if (validate.FindResultFor(flagsOption) is null &&
                    validate.FindResultFor(hostIdOption) is null &&
                    validate.FindResultFor(diskProductOption) is null &&
                    validate.FindResultFor(diskRevisionOption) is null &&
                    validate.FindResultFor(diskVendorOption) is null)
                {
                    validate.ErrorMessage = "At least one option must be specified";
                }
            });
            
            return command;
        }
        
        private static Command CreateRdbBackup()
        {
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var backupPathArgument = new Argument<string>(
                name: "BackupPath",
                description: "Path to Rigid disk block backup file.");
            
            var command = new Command("backup", "Backup Rigid Disk Block to file.");
            command.SetHandler(CommandHandler.RdbBackup, pathArgument, backupPathArgument);
            command.AddArgument(pathArgument);
            command.AddArgument(backupPathArgument);

            return command;
        }
        
        private static Command CreateRdbRestore()
        {
            var backupPathArgument = new Argument<string>(
                name: "BackupPath",
                description: "Path to Rigid disk block backup file.");
            
            var pathArgument = new Argument<string>(
                name: "Path",
                description: "Path to physical drive or image file.");

            var command = new Command("restore", "Restore Rigid Disk Block from backup file.");
            command.SetHandler(CommandHandler.RdbRestore, backupPathArgument, pathArgument);
            command.AddArgument(backupPathArgument);
            command.AddArgument(pathArgument);

            return command;
        }
    }
}