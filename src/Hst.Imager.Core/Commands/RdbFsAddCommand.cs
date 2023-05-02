namespace Hst.Imager.Core.Commands
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Amiga.Extensions;
    using Amiga.RigidDiskBlocks;
    using Amiga.VersionStrings;
    using Hst.Core;
    using Hst.Core.Extensions;
    using Microsoft.Extensions.Logging;
    using BlockHelper = Amiga.RigidDiskBlocks.BlockHelper;

    public class RdbFsAddCommand : CommandBase
    {
        private readonly ILogger<RdbFsAddCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly string dosType;
        private readonly string fileSystemPath;
        private readonly string fileSystemName;

        public RdbFsAddCommand(ILogger<RdbFsAddCommand> logger, ICommandHelper commandHelper,
            IEnumerable<IPhysicalDrive> physicalDrives, string path, string fileSystemPath, string dosType, string fileSystemName)
        {
            this.logger = logger;
            this.commandHelper = commandHelper;
            this.physicalDrives = physicalDrives;
            this.path = path;
            this.fileSystemPath = fileSystemPath;
            this.dosType = string.IsNullOrWhiteSpace(dosType) ? "DOS3" : dosType.ToUpper();
            this.fileSystemName = string.IsNullOrWhiteSpace(fileSystemName) ? "FastFileSystem" : fileSystemName;
        }

        public override async Task<Result> Execute(CancellationToken token)
        {
            if (!string.IsNullOrWhiteSpace(dosType) && dosType.Length != 4)
            {
                return new Result(new Error("DOS type must be 4 characters"));
            }
            
            OnInformationMessage($"Adding file system to Rigid Disk Block at '{path}'");

            OnDebugMessage($"Opening '{path}' as writable");

            var mediaResult = commandHelper.GetWritableMedia(physicalDrives, path);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            using var media = mediaResult.Value;
            var stream = media.Stream;

            OnDebugMessage("Reading Rigid Disk Block");

            var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);

            if (rigidDiskBlock == null)
            {
                return new Result(new Error("Rigid Disk Block not found"));
            }

            OnDebugMessage($"Opening path '{fileSystemPath}' for reading file system");

            await using var fileSystemStream = File.OpenRead(fileSystemPath);
            
            OnDebugMessage("Read file system from file");

            var maxFileSystemSize = 512 * 1024;
            if (fileSystemStream.Length > maxFileSystemSize)
            {
                return new Result(new Error($"Invalid file system size '{fileSystemStream.Length}' larger than max size '{maxFileSystemSize}'"));
            }

            var fileSystemBytes = await fileSystemStream.ReadBytes((int)fileSystemStream.Length);

            var version = await VersionStringReader.Read(new MemoryStream(fileSystemBytes));
            if (string.IsNullOrWhiteSpace(version))
            {
                return new Result(new Error("Version string is empty"));
            }

            var fileVersion = VersionStringReader.Parse(version);
            if (fileVersion == null)
            {
                return new Result(new Error($"Parse version failed"));
            }
            
            var fileSystemHeaderBlock = BlockHelper.CreateFileSystemHeaderBlock(DosTypeHelper.FormatDosType(dosType),
                fileVersion.Version, fileVersion.Revision, fileSystemName, fileSystemBytes);
            fileSystemHeaderBlock.FileSystemName = Path.GetFileName(fileSystemPath);

            long size = fileSystemHeaderBlock.FileSystemSize; 
            
            OnInformationMessage("Adding file system:");
            OnInformationMessage($"- DOS type '0x{fileSystemHeaderBlock.DosType.FormatHex()}' ({fileSystemHeaderBlock.DosType.FormatDosType()})");
            OnInformationMessage($"- Version '{fileSystemHeaderBlock.VersionFormatted}'");
            OnInformationMessage($"- Size '{size.FormatBytes()}' ({size} bytes)");
            OnInformationMessage($"- File system name '{fileSystemHeaderBlock.FileSystemName}'");
            
            rigidDiskBlock.FileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks
                .Where(x => !x.DosType.SequenceEqual(fileSystemHeaderBlock.DosType)).Concat(new[]
                    { fileSystemHeaderBlock });
            
            OnDebugMessage("Writing Rigid Disk Block");
            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

            return new Result();
        }
    }
}