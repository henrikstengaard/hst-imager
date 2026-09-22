namespace Hst.Imager.ConsoleApp
{
    using System.CommandLine;
    using System.CommandLine.Parsing;

    public static class MbrCommandFactory
    {
        public static Command CreateMbrCommand()
        {
            var command = new Command("mbr", "Master Boot Record.");

            command.Add(CreateMbrInfo());
            command.Add(CreateMbrInit());
            command.Add(CreateMbrPart());

            return command;
        }

        private static Command CreateMbrInfo()
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

            var command = new Command("info", "Display info about Master Boot Record.");
            command.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var showUnallocated = ctx.GetValue(showUnallocatedOption);
                return CommandHandler.MbrInfo(path, showUnallocated);
            });
            command.Add(pathArgument);
            command.Add(showUnallocatedOption);

            return command;
        }

        private static Command CreateMbrInit()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var mbrInitCommand = new Command("initialize", "Initialize disk with empty Master Boot Record.");
            mbrInitCommand.Aliases.Add("init");
            mbrInitCommand.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                return CommandHandler.MbrInit(path);
            });
            mbrInitCommand.Add(pathArgument);

            return mbrInitCommand;
        }

        private static Command CreateMbrPart()
        {
            var partCommand = new Command("part", "Partition.");

            partCommand.Add(CreateMbrPartAdd());
            partCommand.Add(CreateMbrPartDel());
            partCommand.Add(CreateMbrPartFormat());
            partCommand.Add(CreateMbrPartExport());
            partCommand.Add(CreateMbrPartImport());
            partCommand.Add(CreateMbrPartClone());

            return partCommand;
        }

        private static Command CreateMbrPartAdd()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var typeArgument = new Argument<string>("Type")
            {
                Description = "Type of the partition as name or number (e.g. name FAT32 or value 0xb for FAT32)."
            };

            var sizeArgument = new Argument<string>("Size")
            {
                Description = "Size of the partition."
            };

            var startSectorOption = new Option<long?>("--start-sector", ["-s"])
            {
                Description = "Start sector."
            };

            var activeOption = new Option<bool>("--active", ["-a"])
            {
                Description = "Set partition active (bootable).",
                DefaultValueFactory = (ArgumentResult _) => false
            };

            var mbrPartAddCommand = new Command("add", "Add partition.");
            mbrPartAddCommand.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var type = ctx.GetValue(typeArgument);
                var size = ctx.GetValue(sizeArgument);
                var startSector = ctx.GetValue(startSectorOption);
                var active = ctx.GetValue(activeOption);
                return CommandHandler.MbrPartAdd(path, type, size, startSector, active);
            });
            mbrPartAddCommand.Add(pathArgument);
            mbrPartAddCommand.Add(typeArgument);
            mbrPartAddCommand.Add(sizeArgument);
            mbrPartAddCommand.Add(startSectorOption);
            mbrPartAddCommand.Add(activeOption);

            return mbrPartAddCommand;
        }

        private static Command CreateMbrPartDel()
        {
            var path = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var partitionNumber = new Argument<int>("PartitionNumber")
            {
                Description = "Partition number to delete."
            };

            var command = new Command("delete", "Delete partition.");
            command.Aliases.Add("del");
            command.SetAction((ParseResult ctx) =>
            {
                var pathVal = ctx.GetValue(path);
                var partitionNum = ctx.GetValue(partitionNumber);
                return CommandHandler.MbrPartDel(pathVal, partitionNum);
            });
            command.Add(path);
            command.Add(partitionNumber);

            return command;
        }

        private static Command CreateMbrPartFormat()
        {
            var pathArgument = new Argument<string>("Path")
            {
                Description = "Path to physical drive or image file."
            };

            var partitionNumberArgument = new Argument<int>("PartitionNumber")
            {
                Description = "Partition number to format."
            };

            var nameArgument = new Argument<string>("Name")
            {
                Description = "Name of the partition."
            };

            var fileSystemOption = new Option<string>("--file-system", ["-fs"])
            {
                Description = "File system format partition with."
            };

            var formatCommand = new Command("format", "Format partition.");
            formatCommand.SetAction((ParseResult ctx) =>
            {
                var path = ctx.GetValue(pathArgument);
                var partitionNumber = ctx.GetValue(partitionNumberArgument);
                var name = ctx.GetValue(nameArgument);
                var fileSystem = ctx.GetValue(fileSystemOption);
                return CommandHandler.MbrPartFormat(path, partitionNumber, name, fileSystem);
            });
            formatCommand.Add(pathArgument);
            formatCommand.Add(partitionNumberArgument);
            formatCommand.Add(nameArgument);

            return formatCommand;
        }

        private static Command CreateMbrPartExport()
        {
            var sourcePathArgument = new Argument<string>("SourcePath")
            {
                Description = "Path to source physical drive or image file."
            };

            var partition = new Argument<string>("Partition")
            {
                Description = "Partition to export from (\"2\" for partition number 2 or \"fat32\" for first fat32 partition)."
            };

            var destinationPathArgument = new Argument<string>("DestinationPath")
            {
                Description = "Path to destination physical drive or image file."
            };

            var command = new Command("export", "Export partition to a file.");
            command.SetAction((ParseResult ctx) =>
            {
                var srcPath = ctx.GetValue(sourcePathArgument);
                var partVal = ctx.GetValue(partition);
                var destPath = ctx.GetValue(destinationPathArgument);
                return CommandHandler.MbrPartExport(srcPath, partVal, destPath);
            });
            command.Add(sourcePathArgument);
            command.Add(partition);
            command.Add(destinationPathArgument);

            return command;
        }

        private static Command CreateMbrPartImport()
        {
            var sourcePathArgument = new Argument<string>("SourcePath")
            {
                Description = "Path to source file."
            };

            var destinationPathArgument = new Argument<string>("DestinationPath")
            {
                Description = "Path to destination physical drive or image file."
            };

            var partition = new Argument<string>("Partition")
            {
                Description = "Partition to import to (\"2\" for partition number 2 or \"fat32\" for first fat32 partition)."
            };

            var command = new Command("import", "Import partition from a file.");
            command.SetAction((ParseResult ctx) =>
            {
                var srcPath = ctx.GetValue(sourcePathArgument);
                var destPath = ctx.GetValue(destinationPathArgument);
                var partVal = ctx.GetValue(partition);
                return CommandHandler.MbrPartImport(srcPath, destPath, partVal);
            });
            command.Add(sourcePathArgument);
            command.Add(destinationPathArgument);
            command.Add(partition);

            return command;
        }

        private static Command CreateMbrPartClone()
        {
            var srcPathArgument = new Argument<string>("SourcePath")
            {
                Description = "Path to source physical drive or image file."
            };

            var srcPartitionNumber = new Argument<int>("Partition")
            {
                Description = "Source partition to clone from."
            };

            var destPathArgument = new Argument<string>("DestinationPath")
            {
                Description = "Path to destination physical drive or image file."
            };

            var destPartitionNumber = new Argument<int>("DestinationPartitionNumber")
            {
                Description = "Destination partition number to clone to."
            };

            var command = new Command("clone", "Clone partition from a physical drive or image file.");
            command.SetAction((ParseResult ctx) =>
            {
                var srcPath = ctx.GetValue(srcPathArgument);
                var srcPartNum = ctx.GetValue(srcPartitionNumber);
                var destPath = ctx.GetValue(destPathArgument);
                var destPartNum = ctx.GetValue(destPartitionNumber);
                return CommandHandler.MbrPartClone(srcPath, srcPartNum, destPath, destPartNum);
            });
            command.Add(srcPathArgument);
            command.Add(srcPartitionNumber);
            command.Add(destPathArgument);
            command.Add(destPartitionNumber);

            return command;
        }
    }
}