namespace Hst.Imager.ConsoleApp
{
    using System.CommandLine;
    using System.CommandLine.Parsing;

    public static class RdbCommandFactory
    {
        public static Command CreateRdbCommand()
        {
            var rdbCommand = new Command("rdb", "Rigid Disk Block.");

            rdbCommand.Add(CreateRdbInfo());
            rdbCommand.Add(CreateRdbInit());
            rdbCommand.Add(CreateRdbResize());
            rdbCommand.Add(CreateRdbFs());
            rdbCommand.Add(CreateRdbPart());
            rdbCommand.Add(CreateRdbUpdate());
            rdbCommand.Add(CreateRdbBackup());
            rdbCommand.Add(CreateRdbRestore());

            return rdbCommand;
        }

        private static Command CreateRdbInfo()
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

            var rdbInfoCommand = new Command("info", "Display info about Rigid Disk Block.");
            rdbInfoCommand.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var showUnallocated = ctx.GetValue(showUnallocatedOption);
                return CommandHandler.RdbInfo(path, showUnallocated);
            });
            rdbInfoCommand.Add(pathArgument);
            rdbInfoCommand.Add(showUnallocatedOption);

            return rdbInfoCommand;
        }

        private static Command CreateRdbInit()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var sizeOption = new Option<string>("--size", ["-s"])
            {
                Description = "Size of disk."
            };

            var nameOption = new Option<string>("--name", ["-n"])
            {
                Description = "Name of disk."
            };

            var rdbBlockLoOption = new Option<int>("--rdb-block-lo")
            {
                Description = "Low block reserved for Rigid Disk Block (0-15)."
            };

            var chsOption = new Option<string>("-chs")
            {
                Description = "Initialize from cylinders, heads and sectors."
            };

            var rdbInitCommand = new Command("initialize", "Initialize disk with empty Rigid Disk Block.");
            rdbInitCommand.Aliases.Add("init");
            rdbInitCommand.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var size = ctx.GetValue(sizeOption);
                var name = ctx.GetValue(nameOption);
                var chs = ctx.GetValue(chsOption);
                var rdbBlockLo = ctx.GetValue(rdbBlockLoOption);
                return CommandHandler.RdbInit(path, size, name, chs, rdbBlockLo);
            });
            rdbInitCommand.Add(pathArgument);
            rdbInitCommand.Add(sizeOption);
            rdbInitCommand.Add(nameOption);
            rdbInitCommand.Add(chsOption);
            rdbInitCommand.Add(rdbBlockLoOption);

            return rdbInitCommand;
        }

        private static Command CreateRdbResize()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var sizeOption = new Option<string>("--size", ["-s"])
            {
                Description = "Size of Rigid Disk Block."
            };

            var command = new Command("resize", "Resize Rigid Disk Block.");
            command.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var size = ctx.GetValue(sizeOption);
                return CommandHandler.RdbResize(path, size);
            });
            command.Add(pathArgument);
            command.Add(sizeOption);

            return command;
        }

        private static Command CreateRdbFs()
        {
            var rdbFsCommand = new Command("filesystem", "File system.");
            rdbFsCommand.Aliases.Add("fs");
            rdbFsCommand.Add(CreateRdbFsAdd());
            rdbFsCommand.Add(CreateRdbFsDel());
            rdbFsCommand.Add(CreateRdbFsExport());
            rdbFsCommand.Add(CreateRdbFsImport());
            rdbFsCommand.Add(CreateRdbFsUpdate());

            return rdbFsCommand;
        }

        private static Command CreateRdbFsAdd()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var fileSystemPathArgument = new Argument<string>("FileSystemPath")
            {
                Description = "Path to file system to add."
            };

            var dosTypeArgument = new Argument<string>("DosType")
            {
                Description = "Dos Type for file system (e.g. DOS3, PDS3)."
            };

            var fileSystemNameOption = new Option<string>("--name", ["-n"])
            {
                Description = "Name of file system."
            };

            var versionOption = new Option<int?>("--version", ["-v"])
            {
                Description = "Version of file system (number before . in version)."
            };

            var revisionOption = new Option<int?>("--revision", ["-r"])
            {
                Description = "Revision of file system (number after . in version)."
            };

            var rdbFsAddCommand = new Command("add", "Add file system.");
            rdbFsAddCommand.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var fileSystemPath = ctx.GetValue(fileSystemPathArgument);
                var dosType = ctx.GetValue(dosTypeArgument);
                var name = ctx.GetValue(fileSystemNameOption);
                var version = ctx.GetValue(versionOption);
                var revision = ctx.GetValue(revisionOption);
                return CommandHandler.RdbFsAdd(path, fileSystemPath, dosType, name, version, revision);
            });
            rdbFsAddCommand.Add(pathArgument);
            rdbFsAddCommand.Add(fileSystemPathArgument);
            rdbFsAddCommand.Add(dosTypeArgument);
            rdbFsAddCommand.Add(fileSystemNameOption);
            rdbFsAddCommand.Add(versionOption);
            rdbFsAddCommand.Add(revisionOption);

            return rdbFsAddCommand;
        }

        private static Command CreateRdbFsDel()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var fileSystemNumber = new Argument<int>("FileSystemNumber")
            {
                Description = "File system number to delete."
            };

            var rdbFsDelCommand = new Command("delete", "Delete file system.");
            rdbFsDelCommand.Aliases.Add("del");
            rdbFsDelCommand.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var num = ctx.GetValue(fileSystemNumber);
                return CommandHandler.RdbFsDel(path, num);
            });
            rdbFsDelCommand.Add(pathArgument);
            rdbFsDelCommand.Add(fileSystemNumber);

            return rdbFsDelCommand;
        }

        private static Command CreateRdbFsImport()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var fileSystemPathArgument = new Argument<string>("FileSystemPath")
            {
                Description = "Path to file system to add."
            };

            var dosTypeOption = new Option<string>("--dos-type", ["-dt"])
            {
                Description = "Dos Type for file system (e.g. DOS3, PDS3)."
            };

            var fileSystemNameOption = new Option<string>("--name", ["-n"])
            {
                Description = "Name of file system."
            };

            var command = new Command("import", "Import file systems from physical drive, image file (supports .adf).");
            command.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var fileSystemPath = ctx.GetValue(fileSystemPathArgument);
                var dosType = ctx.GetValue(dosTypeOption);
                var name = ctx.GetValue(fileSystemNameOption);
                return CommandHandler.RdbFsImport(path, fileSystemPath, dosType, name);
            });
            command.Add(pathArgument);
            command.Add(fileSystemPathArgument);
            command.Add(dosTypeOption);
            command.Add(fileSystemNameOption);

            return command;
        }

        private static Command CreateRdbFsExport()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var fileSystemNumber = new Argument<int>("FileSystemNumber")
            {
                Description = "File system number to delete."
            };

            var fileSystemPathArgument = new Argument<string>("FileSystemPath")
            {
                Description = "Path to file system."
            };

            var command = new Command("export", "Export file system to a file.");
            command.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var num = ctx.GetValue(fileSystemNumber);
                var fsPath = ctx.GetValue(fileSystemPathArgument);
                return CommandHandler.RdbFsExport(path, num, fsPath);
            });
            command.Add(pathArgument);
            command.Add(fileSystemNumber);
            command.Add(fileSystemPathArgument);

            return command;
        }

        private static Command CreateRdbFsUpdate()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var fileSystemNumber = new Argument<int>("FileSystemNumber")
            {
                Description = "File system number to delete."
            };

            var dosTypeArgument = new Option<string>("--dos-type", ["-dt"])
            {
                Description = "Dos type for file system (e.g. DOS3, PDS3)."
            };

            var fileSystemNameOption = new Option<string>("--name", ["-n"])
            {
                Description = "Name of file system."
            };

            var fileSystemPathOption = new Option<string>("--path", ["-p"])
            {
                Description = "Path to file system."
            };

            var command = new Command("update", "Update file system.");
            command.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var num = ctx.GetValue(fileSystemNumber);
                var dosType = ctx.GetValue(dosTypeArgument);
                var name = ctx.GetValue(fileSystemNameOption);
                var fsPath = ctx.GetValue(fileSystemPathOption);
                return CommandHandler.RdbFsUpdate(path, num, dosType, name, fsPath);
            });
            command.Add(pathArgument);
            command.Add(fileSystemNumber);
            command.Add(dosTypeArgument);
            command.Add(fileSystemNameOption);
            command.Add(fileSystemPathOption);
            command.Validators.Add((CommandResult validate) =>
            {
                if (validate.GetResult(dosTypeArgument) is null &&
                    validate.GetResult(fileSystemNameOption) is null &&
                    validate.GetResult(fileSystemPathOption) is null)
                {
                    validate.AddError("At least one option must be specified");
                }
            });
            return command;
        }

        private static Command CreateRdbPart()
        {
            var rdbPartCommand = new Command("part", "Partition.");

            rdbPartCommand.Add(CreateRdbPartAdd());
            rdbPartCommand.Add(CreateRdbPartUpdate());
            rdbPartCommand.Add(CreateRdbPartDel());
            rdbPartCommand.Add(CreateRdbPartCopy());
            rdbPartCommand.Add(CreateRdbPartExport());
            rdbPartCommand.Add(CreateRdbPartImport());
            rdbPartCommand.Add(CreateRdbPartKill());
            rdbPartCommand.Add(CreateRdbPartMove());
            rdbPartCommand.Add(CreateRdbPartFormat());

            return rdbPartCommand;
        }

        private static Command CreateRdbPartAdd()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var dosTypeArgument = new Argument<string>("DosType")
            {
                Description = "DOS type for the partition to use (e.g. DOS3, PFS3)."
            };

            var nameArgument = new Argument<string>("Name")
            {
                Description = "Name of the partition (e.g. DH0)."
            };

            var sizeArgument = new Argument<string>("Size")
            {
                Description = "Size of the partition."
            };

            var reservedOption = new Option<uint?>("--reserved", ["-r"])
            {
                Description = "Set reserved blocks at start of partition."
            };

            var preAllocOption = new Option<uint?>("--pre-alloc", ["-pa"])
            {
                Description = "Set reserved blocks at end of partition"
            };

            var buffersOption = new Option<uint?>("--buffers", ["-bu"])
            {
                Description = "Set buffers"
            };

            var maxTransferOption = new Option<string>("--max-transfer", ["-mt"])
            {
                Description = "Max transfer (integer or hex value e.g. 0x1fe00)"
            };

            var maskOption = new Option<string>("--mask", ["-ma"])
            {
                Description = "Mask (integer or hex value e.g. 0x7ffffffe)"
            };

            var noMountOption = new Option<bool>("--no-mount", ["-nm"])
            {
                Description = "Set partition to no mount, partition is not mounted on boot.",
                DefaultValueFactory = (ArgumentResult _) => false
            };

            var bootableOption = new Option<bool>("--bootable", ["-b"])
            {
                Description = "Set bootable.",
                DefaultValueFactory = (ArgumentResult _) => false
            };

            var bootPriorityOption = new Option<int?>("--boot-priority", ["-bp"])
            {
                Description = "Set boot priority."
            };

            var blockSizeOption = new Option<int?>("--block-size", ["-bs"])
            {
                Description = "Block size for the partition.",
                DefaultValueFactory = (ArgumentResult _) => 512
            };

            var useExperimentalOption = new Option<bool>("--use-experimental")
            {
                Description = "Use experimental partition sizes."
            };

            var startCylinderOption = new Option<uint>("StartCylinder")
            {
                Description = "Start cylinder to add partition."
            };

            var rdbPartAddCommand = new Command("add", "Add partition.");
            rdbPartAddCommand.SetAction(async (ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var name = ctx.GetValue(nameArgument);
                var dosType = ctx.GetValue(dosTypeArgument);
                var size = ctx.GetValue(sizeArgument);
                var reserved = ctx.GetValue(reservedOption);
                var preAlloc = ctx.GetValue(preAllocOption);
                var buffers = ctx.GetValue(buffersOption);
                var maxTransfer = ctx.GetValue(maxTransferOption);
                var mask = ctx.GetValue(maskOption);
                var noMount = ctx.GetValue(noMountOption);
                var bootable = ctx.GetValue(bootableOption);
                var bootPriority = ctx.GetValue(bootPriorityOption);
                var blockSize = ctx.GetValue(blockSizeOption);
                var useExperimental = ctx.GetValue(useExperimentalOption);
                var startCylinder = ctx.GetValue(startCylinderOption);

                await CommandHandler.RdbPartAdd(path, name, dosType, size, reserved, preAlloc, buffers, maxTransfer, mask,
                    noMount, bootable, bootPriority, blockSize, useExperimental, startCylinder);
            });

            rdbPartAddCommand.Add(pathArgument);
            rdbPartAddCommand.Add(nameArgument);
            rdbPartAddCommand.Add(dosTypeArgument);
            rdbPartAddCommand.Add(sizeArgument);
            rdbPartAddCommand.Add(reservedOption);
            rdbPartAddCommand.Add(preAllocOption);
            rdbPartAddCommand.Add(buffersOption);
            rdbPartAddCommand.Add(maxTransferOption);
            rdbPartAddCommand.Add(maskOption);
            rdbPartAddCommand.Add(noMountOption);
            rdbPartAddCommand.Add(bootableOption);
            rdbPartAddCommand.Add(bootPriorityOption);
            rdbPartAddCommand.Add(blockSizeOption);
            rdbPartAddCommand.Add(useExperimentalOption);
            rdbPartAddCommand.Add(startCylinderOption);

            return rdbPartAddCommand;
        }

        private static Command CreateRdbPartUpdate()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var partitionNumberArgument = new Argument<int>("PartitionNumber")
            {
                Description = "Partition number to update."
            };

            var dosTypeOption = new Option<string>("--dos-type", ["-dt"])
            {
                Description = "DOS type for the partition to use (e.g. DOS3, PFS3)."
            };

            var nameOption = new Option<string>("--name", ["-n"])
            {
                Description = "Name of the partition (e.g. DH0)."
            };

            var reservedOption = new Option<int?>("--reserved", ["-r"])
            {
                Description = "Reserved blocks at start of partition."
            };

            var preAllocOption = new Option<int?>("--pre-alloc", ["-pa"])
            {
                Description = "Reserved blocks at end of partition"
            };

            var buffersOption = new Option<int?>("--buffers", ["-bu"])
            {
                Description = "Buffers"
            };

            var maxTransferOption = new Option<string>("--max-transfer", ["-mt"])
            {
                Description = "Max transfer (integer or hex value e.g. 0x1fe00)"
            };

            var maskOption = new Option<string>("--mask", ["-ma"])
            {
                Description = "Mask (integer or hex value e.g. 0x7ffffffe)"
            };

            var noMountOption = new Option<BoolType?>("--no-mount", ["-nm"])
            {
                Description = "Set no mount for partition (partition is not mounted on boot)."
            };

            var bootableOption = new Option<BoolType?>("--bootable", ["-b"])
            {
                Description = "Set bootable for partition."
            };

            var bootPriorityOption = new Option<int?>("--boot-priority", ["-bp"])
            {
                Description = "Set boot priority (controls order of partitions to boot, lowest is booted first)."
            };

            var fileSystemBlockSizeOption = new Option<int?>("--block-size", ["-bs"])
            {
                Description = "File system block size for the partition."
            };

            var command = new Command("update", "Update partition.");
            command.SetAction(async (ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var partitionNumber = ctx.GetValue(partitionNumberArgument);
                var name = ctx.GetValue(nameOption);
                var dosType = ctx.GetValue(dosTypeOption);
                var reserved = ctx.GetValue(reservedOption);
                var preAlloc = ctx.GetValue(preAllocOption);
                var buffers = ctx.GetValue(buffersOption);
                var maxTransfer = ctx.GetValue(maxTransferOption);
                var mask = ctx.GetValue(maskOption);
                var noMount = ctx.GetValue(noMountOption);
                var bootable = ctx.GetValue(bootableOption);
                var bootPriority = ctx.GetValue(bootPriorityOption);
                var fileSystemBlockSize = ctx.GetValue(fileSystemBlockSizeOption);

                await CommandHandler.RdbPartUpdate(path, partitionNumber, name, dosType, reserved, preAlloc, buffers,
                    maxTransfer,
                    mask, noMount.HasValue ? noMount.Value == BoolType.True : null,
                    bootable.HasValue ? bootable.Value == BoolType.True : null, bootPriority, fileSystemBlockSize);
            });

            command.Add(pathArgument);
            command.Add(partitionNumberArgument);
            command.Add(nameOption);
            command.Add(dosTypeOption);
            command.Add(reservedOption);
            command.Add(preAllocOption);
            command.Add(buffersOption);
            command.Add(maxTransferOption);
            command.Add(maskOption);
            command.Add(noMountOption);
            command.Add(bootableOption);
            command.Add(bootPriorityOption);
            command.Add(fileSystemBlockSizeOption);
            command.Validators.Add((CommandResult validate) =>
            {
                if (validate.GetResult(nameOption) is null &&
                    validate.GetResult(dosTypeOption) is null &&
                    validate.GetResult(reservedOption) is null &&
                    validate.GetResult(preAllocOption) is null &&
                    validate.GetResult(buffersOption) is null &&
                    validate.GetResult(maxTransferOption) is null &&
                    validate.GetResult(maskOption) is null &&
                    validate.GetResult(noMountOption) is null &&
                    validate.GetResult(bootableOption) is null &&
                    validate.GetResult(bootPriorityOption) is null &&
                    validate.GetResult(fileSystemBlockSizeOption) is null)
                {
                    validate.AddError("At least one option must be specified");
                }
            });
            return command;
        }

        private static Command CreateRdbPartDel()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var partitionNumber = new Argument<int>("PartitionNumber")
            {
                Description = "Partition number to delete."
            };

            var rdbPartDelCommand = new Command("delete", "Delete partition.");
            rdbPartDelCommand.Aliases.Add("del");
            rdbPartDelCommand.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var num = ctx.GetValue(partitionNumber);
                return CommandHandler.RdbPartDel(path, num);
            });
            rdbPartDelCommand.Add(pathArgument);
            rdbPartDelCommand.Add(partitionNumber);

            return rdbPartDelCommand;
        }

        private static Command CreateRdbPartCopy()
        {
            var sourcePathArgument = new Argument<string>("SourcePath")
            {
                Description = "Path to source physical drive or image file."
            };

            var partitionNumber = new Argument<int>("PartitionNumber")
            {
                Description = "Partition number to copy."
            };

            var destinationPathArgument = new Argument<string>("DestinationPath")
            {
                Description = "Path to destination physical drive or image file."
            };

            var nameOption = new Option<string>("--name", ["-n"])
            {
                Description = "Name of the partition (e.g. DH0)."
            };

            var dosTypeOption = new Option<string>("--dos-type", ["-dt"])
            {
                Description = "DOS type for the partition to use (e.g. DOS3, PFS3)."
            };

            var rdbPartDelCommand = new Command("copy", "Copy partition from a physical drive or image file.");
            rdbPartDelCommand.SetAction((ParseResult ctx) =>
            {
                var src = ctx.GetValue(sourcePathArgument);
                var num = ctx.GetValue(partitionNumber);
                var dest = ctx.GetValue(destinationPathArgument);
                var name = ctx.GetValue(nameOption);
                var dosType = ctx.GetValue(dosTypeOption);
                return CommandHandler.RdbPartCopy(src, num, dest, name, dosType);
            });
            rdbPartDelCommand.Add(sourcePathArgument);
            rdbPartDelCommand.Add(partitionNumber);
            rdbPartDelCommand.Add(destinationPathArgument);
            rdbPartDelCommand.Add(nameOption);
            rdbPartDelCommand.Add(dosTypeOption);

            return rdbPartDelCommand;
        }

        private static Command CreateRdbPartExport()
        {
            var sourcePathArgument = new Argument<string>("SourcePath")
            {
                Description = "Path to source physical drive or image file."
            };

            var partitionNumber = new Argument<int>("PartitionNumber")
            {
                Description = "Partition number to export."
            };

            var destinationPathArgument = new Argument<string>("DestinationPath")
            {
                Description = "Path to destination file (e.g. DH0.hdf)."
            };

            var command = new Command("export", "Export partition to a hard file (e.g. DH0.hdf).");
            command.SetAction((ParseResult ctx) =>
            {
                var src = ctx.GetValue(sourcePathArgument);
                var num = ctx.GetValue(partitionNumber);
                var dest = ctx.GetValue(destinationPathArgument);
                return CommandHandler.RdbPartExport(src, num, dest);
            });
            command.Add(sourcePathArgument);
            command.Add(partitionNumber);
            command.Add(destinationPathArgument);

            return command;
        }

        private static Command CreateRdbPartImport()
        {
            var sourcePathArgument = new Argument<string>("SourcePath")
            {
                Description = "Path to source hard file (e.g. DH0.hdf)."
            };

            var destinationPathArgument = new Argument<string>("DestinationPath")
            {
                Description = "Path to destination physical drive or image file."
            };

            var dosTypeArgument = new Argument<string>("DosType")
            {
                Description = "DOS type for the partition to use (e.g. DOS3, PFS3)."
            };

            var nameArgument = new Argument<string>("Name")
            {
                Description = "Name of the partition (e.g. DH0)."
            };

            var fileSystemBlockSizeOption = new Option<int>("--block-size", ["-bs"])
            {
                Description = "File system block size for the partition.",
                DefaultValueFactory = (ArgumentResult _) => 512
            };

            var bootableOption = new Option<bool>("--bootable", ["-b"])
            {
                Description = "Set bootable.",
                DefaultValueFactory = (ArgumentResult _) => false
            };

            var command = new Command("import", "Import partition from a hard file (e.g. DH0.hdf).");
            command.SetAction((ParseResult ctx) =>
            {
                var src = ctx.GetValue(sourcePathArgument);
                var dest = ctx.GetValue(destinationPathArgument);
                var name = ctx.GetValue(nameArgument);
                var dosType = ctx.GetValue(dosTypeArgument);
                var blockSize = ctx.GetValue(fileSystemBlockSizeOption);
                var bootable = ctx.GetValue(bootableOption);
                return CommandHandler.RdbPartImport(src, dest, name, dosType, blockSize, bootable);
            });
            command.Add(sourcePathArgument);
            command.Add(destinationPathArgument);
            command.Add(nameArgument);
            command.Add(dosTypeArgument);
            command.Add(fileSystemBlockSizeOption);
            command.Add(bootableOption);

            return command;
        }

        private static Command CreateRdbPartKill()
        {
            var sourcePathArgument = new Argument<string>("SourcePath")
            {
                Description = "Path to source physical drive or image file."
            };

            var partitionNumber = new Argument<int>("PartitionNumber")
            {
                Description = "Partition number to export."
            };

            var hexBootBytesArgument = new Argument<string>("HexBootBytes")
            {
                Description = "Boot bytes in hex to write (e.g. 00000000)."
            };

            var command = new Command("kill", "Kill partition.");
            command.SetAction((ParseResult ctx) =>
            {
                var src = ctx.GetValue(sourcePathArgument);
                var num = ctx.GetValue(partitionNumber);
                var hexBytes = ctx.GetValue(hexBootBytesArgument);
                return CommandHandler.RdbPartKill(src, num, hexBytes);
            });
            command.Add(sourcePathArgument);
            command.Add(partitionNumber);
            command.Add(hexBootBytesArgument);

            return command;
        }

        private static Command CreateRdbPartMove()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var partitionNumber = new Argument<int>("PartitionNumber")
            {
                Description = "Partition number to move."
            };

            var startCylinder = new Argument<uint>("StartCylinder")
            {
                Description = "Start cylinder to move partition to."
            };

            var command = new Command("move", "Move partition.");
            command.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var num = ctx.GetValue(partitionNumber);
                var startCyl = ctx.GetValue(startCylinder);
                return CommandHandler.RdbPartMove(path, num, startCyl);
            });
            command.Add(pathArgument);
            command.Add(partitionNumber);
            command.Add(startCylinder);

            return command;
        }

        private static Command CreateRdbPartFormat()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var partitionNumber = new Argument<int>("PartitionNumber")
            {
                Description = "Partition number to format."
            };

            var volumeNameArgument = new Argument<string>("VolumeName")
            {
                Description = "Name of the volume (e.g. Workbench)."
            };

            var nonRdbOption = new Option<bool>("--non-rdb")
            {
                Description = "Set non-RDB.",
                DefaultValueFactory = (ArgumentResult _) => false
            };

            var chsOption = new Option<string>("-chs")
            {
                Description = "Format from cylinders, heads and sectors. Optional for non-RDB partition."
            };

            var dosTypeOption = new Option<string>("--dos-type", ["-dt"])
            {
                Description = "DOS type for the partition to use (e.g. DOS3, PFS3). Required for non-RDB partition."
            };

            var rdbPartFormatCommand = new Command("format", "Format partition.");
            rdbPartFormatCommand.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var num = ctx.GetValue(partitionNumber);
                var volName = ctx.GetValue(volumeNameArgument);
                var nonRdb = ctx.GetValue(nonRdbOption);
                var chs = ctx.GetValue(chsOption);
                var dosType = ctx.GetValue(dosTypeOption);
                return CommandHandler.RdbPartFormat(path, num, volName, nonRdb, chs, dosType);
            });
            rdbPartFormatCommand.Add(pathArgument);
            rdbPartFormatCommand.Add(partitionNumber);
            rdbPartFormatCommand.Add(volumeNameArgument);
            rdbPartFormatCommand.Add(nonRdbOption);
            rdbPartFormatCommand.Add(chsOption);
            rdbPartFormatCommand.Add(dosTypeOption);

            return rdbPartFormatCommand;
        }

        private static Command CreateRdbUpdate()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var flagsOption = new Option<uint?>("--flags", ["-f"])
            {
                Description = "Flags."
            };

            var hostIdOption = new Option<uint?>("--host-id", ["-h"])
            {
                Description = "Host id."
            };

            var diskProductOption = new Option<string>("--disk-product", ["-dp"])
            {
                Description = "Disk product."
            };

            var diskRevisionOption = new Option<string>("--disk-revision", ["-dr"])
            {
                Description = "Disk revision."
            };

            var diskVendorOption = new Option<string>("--disk-vendor", ["-dv"])
            {
                Description = "Disk vendor."
            };

            var command = new Command("update", "Update Rigid Disk Block.");
            command.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var flags = ctx.GetValue(flagsOption);
                var hostId = ctx.GetValue(hostIdOption);
                var diskProduct = ctx.GetValue(diskProductOption);
                var diskRevision = ctx.GetValue(diskRevisionOption);
                var diskVendor = ctx.GetValue(diskVendorOption);
                return CommandHandler.RdbUpdate(path, flags, hostId, diskProduct, diskRevision, diskVendor);
            });
            command.Add(pathArgument);
            command.Add(flagsOption);
            command.Add(hostIdOption);
            command.Add(diskProductOption);
            command.Add(diskRevisionOption);
            command.Add(diskVendorOption);
            command.Validators.Add((CommandResult validate) =>
            {
                if (validate.GetResult(flagsOption) is null &&
                    validate.GetResult(hostIdOption) is null &&
                    validate.GetResult(diskProductOption) is null &&
                    validate.GetResult(diskRevisionOption) is null &&
                    validate.GetResult(diskVendorOption) is null)
                {
                    validate.AddError("At least one option must be specified");
                }
            });

            return command;
        }

        private static Command CreateRdbBackup()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var backupPathArgument = new Argument<string>("BackupPath")
            {
                Description = "Path to Rigid disk block backup file."
            };

            var command = new Command("backup", "Backup Rigid Disk Block to file.");
            command.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var backupPath = ctx.GetValue(backupPathArgument);
                return CommandHandler.RdbBackup(path, backupPath);
            });
            command.Add(pathArgument);
            command.Add(backupPathArgument);

            return command;
        }

        private static Command CreateRdbRestore()
        {
            var backupPathArgument = new Argument<string>("BackupPath")
            {
                Description = "Path to Rigid disk block backup file."
            };

            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var command = new Command("restore", "Restore Rigid Disk Block from backup file.");
            command.SetAction((ParseResult ctx) =>
            {
                var backupPath = ctx.GetValue(backupPathArgument);
                var path = ctx.GetValue(pathArgument);
                return CommandHandler.RdbRestore(backupPath, path);
            });
            command.Add(backupPathArgument);
            command.Add(pathArgument);

            return command;
        }
    }
}