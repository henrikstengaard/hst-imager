namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amiga.Extensions;
using Amiga.RigidDiskBlocks;
using Amiga.VersionStrings;
using Extensions;
using Hst.Core;
using Hst.Core.Extensions;
using Hst.Imager.Core.Helpers;
using Microsoft.Extensions.Logging;
using BlockHelper = Amiga.RigidDiskBlocks.BlockHelper;

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

        OnDebugMessage($"Finding file system from path '{fileSystemPath}'");

        var findFileSystemInMediaResult = await AmigaFileSystemHelper.FindFileSystemInMedia(commandHelper,
            fileSystemPath, fileSystemName);

        if (findFileSystemInMediaResult.IsFaulted)
        {
            return new Result(new Error($"No file systems read from file systems path '{fileSystemPath}'"));
        }

        var fileSystem = findFileSystemInMediaResult.Value;

        var fileSystemHeaderBlock = ImportFileSystem(fileSystem.Item1, fileSystem.Item2);

        OnInformationMessage($"Importing file system to Rigid Disk Block at '{path}' from file system path '{fileSystemPath}'");
        
        OnDebugMessage($"Opening '{path}' as writable");

        var writableMediaResult = await commandHelper.GetWritableMedia(physicalDrives, path);
        if (writableMediaResult.IsFaulted)
        {
            return new Result(writableMediaResult.Error);
        }

        using var media = MediaHelper.GetMediaWithPiStormRdbSupport(commandHelper, writableMediaResult.Value, path);
        var stream = media.Stream;

        OnDebugMessage("Reading Rigid Disk Block");

        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(media);

        if (rigidDiskBlock == null)
        {
            return new Result(new Error("Rigid Disk Block not found"));
        }

        long fileSystemSize = fileSystem.Item2.Length;

        OnInformationMessage("Imported file system:");
        OnInformationMessage($"- DOS type '0x{fileSystemHeaderBlock.DosType.FormatHex()}' ({fileSystemHeaderBlock.DosType.FormatDosType()})");
        OnInformationMessage($"- Version '{fileSystemHeaderBlock.VersionFormatted}'");
        OnInformationMessage($"- Size '{fileSystemSize.FormatBytes()}' ({fileSystemSize} bytes)");
        OnInformationMessage($"- File system name '{fileSystemHeaderBlock.FileSystemName}'");

        AddFileSystem(rigidDiskBlock, fileSystemHeaderBlock);

        OnDebugMessage("Writing Rigid Disk Block");
        await MediaHelper.WriteRigidDiskBlockToMedia(media, rigidDiskBlock);

        return new Result();
    }

    private void AddFileSystem(RigidDiskBlock rigidDiskBlock, FileSystemHeaderBlock fileSystemHeaderBlock)
    {
        rigidDiskBlock.FileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks
            .Where(x => !x.DosType.SequenceEqual(fileSystemHeaderBlock.DosType)).Concat(new[]
                { fileSystemHeaderBlock });
    }

    private FileSystemHeaderBlock ImportFileSystem(string fileSystemName, byte[] fileSystemBytes)
    {
        var version = VersionStringReader.Read(fileSystemBytes);
        var amigaVersion = VersionStringReader.Parse(version) ?? new AmigaVersion { Version = 1, Revision = 0 };

        var dosTypeBytes = !string.IsNullOrWhiteSpace(dosType)
            ? DosTypeHelper.FormatDosType(dosType)
            : Array.Empty<byte>();

        var fileSystemHeaderBlock = BlockHelper.CreateFileSystemHeaderBlock(dosTypeBytes, amigaVersion.Version,
            amigaVersion.Revision, fileSystemName, fileSystemBytes);

            OnDebugMessage(
                $"- Found '{fileSystemHeaderBlock.FileSystemName}' version '{version.Trim()}' {((long)fileSystemHeaderBlock.FileSystemSize).FormatBytes()} ({fileSystemHeaderBlock.FileSystemSize} bytes)");

        return fileSystemHeaderBlock;
    }
}