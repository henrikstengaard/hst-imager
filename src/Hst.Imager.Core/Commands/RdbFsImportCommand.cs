namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amiga.Extensions;
using Amiga.FileSystems.FastFileSystem;
using Amiga.RigidDiskBlocks;
using Amiga.VersionStrings;
using Extensions;
using Hst.Core;
using Hst.Core.Extensions;
using Microsoft.Extensions.Logging;
using BlockHelper = Amiga.RigidDiskBlocks.BlockHelper;
using Constants = Amiga.FileSystems.FastFileSystem.Constants;

public class RdbFsImportCommand : CommandBase
{
    private readonly ILogger<RdbFsImportCommand> logger;
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string path;
    private readonly string dosType;
    private readonly string fileSystemName;
    private readonly string fileSystemPath;

    public RdbFsImportCommand(ILogger<RdbFsImportCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, string fileSystemPath, string dosType,
        string fileSystemName)
    {
        this.logger = logger;
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
        this.fileSystemPath = fileSystemPath;
        this.dosType = dosType;
        this.fileSystemName = fileSystemName;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(dosType) && dosType.Length != 4)
        {
            return new Result(new Error("DOS type must be 4 characters"));
        }

        OnDebugMessage($"Opening '{path}' as writable");

        var mediaResult = commandHelper.GetWritableMedia(physicalDrives, path, allowPhysicalDrive: true);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }

        using var media = mediaResult.Value;
        await using var stream = media.Stream;

        OnDebugMessage("Reading Rigid Disk Block");

        var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);

        if (rigidDiskBlock == null)
        {
            return new Result(new Error("Rigid Disk Block not found"));
        }

        OnDebugMessage($"Opening path '{fileSystemPath}' for reading file system");

        var fileSystemMediaResult =
            commandHelper.GetReadableMedia(physicalDrives, fileSystemPath, allowPhysicalDrive: true);
        if (fileSystemMediaResult.IsFaulted)
        {
            return new Result(fileSystemMediaResult.Error);
        }

        using var fileSystemMedia = fileSystemMediaResult.Value;
        await using var fileSystemStream = fileSystemMedia.Stream;

        var firstBytes = await fileSystemStream.ReadBytes(512 * 2048);

        var fileSystemHeaderBlocks = (await ImportFileSystems(new MemoryStream(firstBytes))).ToList();

        if (!fileSystemHeaderBlocks.Any())
        {
            return new Result(new Error($"No file systems read from file systems path '{fileSystemPath}'"));
        }

        foreach (var fileSystemHeaderBlock in fileSystemHeaderBlocks)
        {
            long size = fileSystemHeaderBlock.FileSystemSize;
            OnInformationMessage("Adding file system:");
            OnInformationMessage($"- DOS type '{fileSystemHeaderBlock.DosType.FormatDosType()}'");
            OnInformationMessage($"- Version '{fileSystemHeaderBlock.VersionFormatted}'");
            OnInformationMessage($"- Size '{size.FormatBytes()}' ({size} bytes)");
            OnInformationMessage($"- File system name '{fileSystemHeaderBlock.FileSystemName}'");

            AddFileSystem(rigidDiskBlock, fileSystemHeaderBlock);
        }

        OnDebugMessage("Writing Rigid Disk Block");
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

        return new Result();
    }

    private void AddFileSystem(RigidDiskBlock rigidDiskBlock, FileSystemHeaderBlock fileSystemHeaderBlock)
    {
        rigidDiskBlock.FileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks
            .Where(x => !x.DosType.SequenceEqual(fileSystemHeaderBlock.DosType)).Concat(new[]
                { fileSystemHeaderBlock });
    }

    private async Task<IEnumerable<FileSystemHeaderBlock>> ImportFileSystems(MemoryStream stream)
    {
        var identifier = BitConverter.ToUInt32(await stream.ReadBytes(4), 0);
        if (identifier.Equals(BlockIdentifiers.RigidDiskBlock))
        {
            OnDebugMessage("Importing file systems from Rigid Disk Block");
            return await ReadFileSystemsFromRigidDiskBlock(stream);
        }

        var dos1Identifier = BitConverter.ToUInt32(new byte[] { 0x44, 0x4f, 0x53, 0x1 });
        if (identifier.Equals(dos1Identifier))
        {
            OnDebugMessage("Importing file systems from ADF");
            return await ReadFileSystemsFromAdf(stream);
        }

        return new List<FileSystemHeaderBlock>();
    }

    private async Task<IEnumerable<FileSystemHeaderBlock>> ReadFileSystemsFromRigidDiskBlock(Stream stream)
    {
        var dosTypeBytes = !string.IsNullOrWhiteSpace(dosType)
            ? DosTypeHelper.FormatDosType(dosType)
            : Array.Empty<byte>();

        var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);

        var fileSystemHeaderBlocks = (await FileSystemHeaderBlockReader.Read(rigidDiskBlock, stream)).ToList();

        // skip file systems not matching dos type, if dos type is defined
        if (dosTypeBytes.Length == 4)
        {
            fileSystemHeaderBlocks = fileSystemHeaderBlocks.Where(x => x.DosType.SequenceEqual(dosTypeBytes)).ToList();
        }

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

        OnDebugMessage($"Mounting ADF file");

        var volume = await FastFileSystemHelper.MountAdf(stream);

        OnDebugMessage($"Disk name = '{volume.RootBlock.DiskName}'");

        // read all entries from adf
        var entries =
            (await Amiga.FileSystems.FastFileSystem.Directory.ReadEntries(volume, volume.RootBlock, true))
            .OrderBy(x => x.Name).ToList();

        var dosTypeBytes =
            DosTypeHelper.FormatDosType(string.IsNullOrWhiteSpace(this.dosType) ? "DOS3" : this.dosType.ToUpper());

        var findFileSystemName = string.IsNullOrWhiteSpace(fileSystemName) ? "FastFileSystem" : fileSystemName;
        OnDebugMessage($"Finding file system files with name '{findFileSystemName}'");

        var fileSystemEntries = FindEntries(entries, findFileSystemName).ToList();

        if (!fileSystemEntries.Any())
        {
            return new List<FileSystemHeaderBlock>();
        }

        foreach (var fastFileSystemEntry in fileSystemEntries)
        {
            var entryStream = await Amiga.FileSystems.FastFileSystem.File.Open(volume, fastFileSystemEntry);
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

            var fileSystemHeaderBlock = BlockHelper.CreateFileSystemHeaderBlock(dosTypeBytes, fileVersion.Version,
                fileVersion.Revision, fastFileSystemEntry.Name, entryBytes);

            OnDebugMessage(
                $"- Found '{fileSystemHeaderBlock.FileSystemName}' version '{version.Trim()}' {((long)fileSystemHeaderBlock.FileSystemSize).FormatBytes()} ({fileSystemHeaderBlock.FileSystemSize} bytes)");

            fileSystemHeaderBlocks.Add(fileSystemHeaderBlock);
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