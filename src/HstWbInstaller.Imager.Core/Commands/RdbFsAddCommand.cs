namespace HstWbInstaller.Imager.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Amiga.Extensions;
    using Hst.Amiga.FileSystems.FastFileSystem;
    using Hst.Amiga.RigidDiskBlocks;
    using Hst.Amiga.VersionStrings;
    using Hst.Core.Extensions;
    using HstWbInstaller.Core;
    using Microsoft.Extensions.Logging;
    using BlockHelper = Hst.Amiga.RigidDiskBlocks.BlockHelper;

    public class RdbFsAddCommand : CommandBase
    {
        private readonly ILogger<RdbInitCommand> logger;
        private readonly ICommandHelper commandHelper;
        private readonly IEnumerable<IPhysicalDrive> physicalDrives;
        private readonly string path;
        private readonly string dosType;
        private readonly string fileSystemPath;
        private readonly string fileSystemName;

        public RdbFsAddCommand(ILogger<RdbInitCommand> logger, ICommandHelper commandHelper,
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
            OnProgressMessage($"Opening '{path}' for reading/writing Rigid Disk Block");

            var mediaResult = commandHelper.GetWritableMedia(physicalDrives, path, allowPhysicalDrive: true);
            if (mediaResult.IsFaulted)
            {
                return new Result(mediaResult.Error);
            }

            using var media = mediaResult.Value;
            await using var stream = media.Stream;

            OnProgressMessage($"Reading Rigid Disk Block from path '{path}'");
            
            var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

            if (rigidDiskBlock == null)
            {
                return new Result(new Error("RDB not found"));
            }

            OnProgressMessage($"Opening path '{fileSystemPath}' for reading file system");
            
            var fileSystemMediaResult =
                commandHelper.GetReadableMedia(physicalDrives, fileSystemPath, allowPhysicalDrive: true);
            if (fileSystemMediaResult.IsFaulted)
            {
                return new Result(fileSystemMediaResult.Error);
            }

            using var fileSystemMedia = fileSystemMediaResult.Value;
            await using var fileSystemStream = fileSystemMedia.Stream;

            var firstBytes = await fileSystemStream.ReadBytes(512 * 2048);

            var fileSystemHeaderBlocks = (await ReadFileSystems(new MemoryStream(firstBytes))).ToList();

            if (!fileSystemHeaderBlocks.Any())
            {
                return new Result(new Error($"No file systems read from file systems path '{fileSystemPath}'"));
            }

            foreach (var fileSystemHeaderBlock in fileSystemHeaderBlocks)
            {
                long size = fileSystemHeaderBlock.LoadSegBlocks.Sum(x => x.Data.Length); 
                OnProgressMessage($"Adding file system with DOS type '{fileSystemHeaderBlock.DosType.FormatDosType()}' {size.FormatBytes()} ({size} bytes)");

                AddFileSystem(rigidDiskBlock, fileSystemHeaderBlock);
            }
            
            OnProgressMessage($"Writing Rigid Disk Block to path '{path}'");
            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

            return new Result();
        }

        private void AddFileSystem(RigidDiskBlock rigidDiskBlock, FileSystemHeaderBlock fileSystemHeaderBlock)
        {
            rigidDiskBlock.FileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks
                .Where(x => !x.DosType.SequenceEqual(fileSystemHeaderBlock.DosType)).Concat(new[]
                    { fileSystemHeaderBlock });
        }

        private async Task<IEnumerable<FileSystemHeaderBlock>> ReadFileSystems(MemoryStream stream)
        {
            var identifier = BitConverter.ToUInt32(await stream.ReadBytes(4), 0);
            if (identifier.Equals(BlockIdentifiers.RigidDiskBlock))
            {
                OnProgressMessage($"Read file systems from Rigid Disk Block");
                return await ReadFileSystemsFromRigidDiskBlock(stream);
            }

            var dos1Identifier = BitConverter.ToUInt32(new byte[] { 0x44, 0x4f, 0x53, 0x1 });
            if (identifier.Equals(dos1Identifier))
            {
                OnProgressMessage($"Read file systems from ADF");
                return await ReadFileSystemsFromAdf(stream);
            }

            OnProgressMessage($"Read file systems from file");
            
            var version = await VersionStringReader.Read(stream);
            if (string.IsNullOrWhiteSpace(version))
            {
                OnProgressMessage($"Version string is empty");
                return new List<FileSystemHeaderBlock>();
            }

            var fileVersion = VersionStringReader.Parse(version);
            if (fileVersion == null)
            {
                OnProgressMessage($"File version is null");
                return new List<FileSystemHeaderBlock>();
            }

            return new[]
            {
                BlockHelper.CreateFileSystemHeaderBlock(DosTypeHelper.FormatDosType(dosType),
                    fileVersion.Version, fileVersion.Revision, fileSystemName, stream.ToArray())
            };
        }

        private async Task<IEnumerable<FileSystemHeaderBlock>> ReadFileSystemsFromRigidDiskBlock(Stream stream)
        {
            var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
            
            var fileSystemHeaderBlocks = (await FileSystemHeaderBlockReader.Read(rigidDiskBlock, stream)).ToList();
            foreach (var fileSystemHeaderBlock in fileSystemHeaderBlocks)
            {
                fileSystemHeaderBlock.NextFileSysHeaderBlock = 0;

                foreach (var loadSegBlock in fileSystemHeaderBlock.LoadSegBlocks)
                {
                    loadSegBlock.NextLoadSegBlock = 0;
                }
            }

            return fileSystemHeaderBlocks;
        }

        private async Task<IEnumerable<FileSystemHeaderBlock>> ReadFileSystemsFromAdf(Stream stream)
        {
            var fileSystemHeaderBlocks = new List<FileSystemHeaderBlock>();

            OnProgressMessage($"Mounting ADF file");
            
            var volume = await FastFileSystemHelper.MountAdf(stream);

            OnProgressMessage($"Disk name = '{volume.RootBlock.DiskName}'");
            
            // read all entries from adf
            var entries =
                (await Hst.Amiga.FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlock, true))
                .OrderBy(x => x.Name).ToList();

            OnProgressMessage($"Finding file system files with name '{fileSystemName}'");
            
            var fileSystemEntries = FindEntries(entries, fileSystemName).ToList();

            if (!fileSystemEntries.Any())
            {
                return new List<FileSystemHeaderBlock>();
            }
            
            foreach (var fastFileSystemEntry in fileSystemEntries)
            {
                var entryStream = await Hst.Amiga.FileSystems.FastFileSystem.File.Open(volume, fastFileSystemEntry);
                var entryBytes = await entryStream.ReadBytes(fastFileSystemEntry.Size);

                var version = VersionStringReader.Read(entryBytes);
                if (string.IsNullOrWhiteSpace(version))
                {
                    continue;
                }

                var fileVersion = VersionStringReader.Parse(version);
                if (fileVersion == null)
                {
                    continue;
                }

                long size = fastFileSystemEntry.Size;
                OnProgressMessage($"- Found '{fileSystemName}' version '{version.Trim()}' {size.FormatBytes()} ({size} bytes)");

                fileSystemHeaderBlocks.Add(BlockHelper.CreateFileSystemHeaderBlock(DosTypeHelper.FormatDosType(dosType),
                    fileVersion.Version, fileVersion.Revision, fileSystemName, entryBytes));
            }

            return fileSystemHeaderBlocks;
        }
        
        private IEnumerable<Entry> FindEntries(IEnumerable<Entry> entries, string name)
        {
            var matchingEntries = new List<Entry>();

            foreach (var entry in entries)
            {
                if (entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    matchingEntries.Add(entry);
                }

                if (entry.Type == Constants.ST_DIR)
                {
                    matchingEntries.AddRange(FindEntries(entry.SubDir, name));
                }
            }

            return matchingEntries;
        }
    }
}