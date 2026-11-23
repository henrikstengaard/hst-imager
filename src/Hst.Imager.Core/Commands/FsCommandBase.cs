using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Core;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.UaeMetadatas;

namespace Hst.Imager.Core.Commands;

public abstract class FsCommandBase : CommandBase
{
    protected readonly ICommandHelper commandHelper;
    protected readonly IEnumerable<IPhysicalDrive> physicalDrives;

    protected FsCommandBase(ICommandHelper commandHelper, IEnumerable<IPhysicalDrive> physicalDrives)
    {
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
    }
    
    /// <summary>
    /// Is single file or uses pattern examines if the operation involves 1 file or if the operation uses pattern.
    /// </summary>
    /// <param name="entry">Entry to examine.</param>
    /// <param name="walker">Volume entry walker to examine.</param>
    /// <returns>True, if entry is a file and there are no more entries or if there is only a single file entry next or if the walker uses a pattern. Otherwise, false.</returns>
    protected static bool IsSingleFileOrUsesPattern(Models.FileSystems.Entry entry,
        Volumes.VolumeEntryWalker walker) =>
        (entry.Type == Models.FileSystems.EntryType.File && !walker.HasMoreEntries) ||
        walker.IsSingleFileEntryNext ||
        walker.UsesPattern;

    /// <summary>
    /// Create volume factory for command.
    /// </summary>
    protected Volumes.VolumeFactory CreateVolumeFactory(UaeMetadataHelper uaeMetadataHelper,
        UaeMetadata uaeMetadata) =>
        new(commandHelper, physicalDrives, uaeMetadataHelper, uaeMetadata, OnDebugMessage);

    /// <summary>
    /// Open initialized volume target for destination path to write entries to.
    /// An existing local file is only allowed as destination, when force overwrite is used.
    /// </summary>
    protected async Task<Result<Volumes.VolumeTarget>> OpenVolumeTarget(Volumes.VolumeFactory volumeFactory,
        string destPath, bool recursive, bool createDestDirectory, bool forceOverwrite)
    {
        var mediaResult = commandHelper.ResolveMedia(destPath);
        if (mediaResult.IsSuccess &&
            string.IsNullOrWhiteSpace(mediaResult.Value.FileSystemPath) &&
            !Directory.Exists(destPath) &&
            File.Exists(destPath) &&
            !forceOverwrite)
        {
            return new Result<Volumes.VolumeTarget>(new PathExistsError($"Path already exists '{destPath}'"));
        }

        var volumePathResult = await volumeFactory.Open(destPath, Volumes.VolumeAccess.Write, openArchives: false);
        if (volumePathResult.IsFaulted)
        {
            return new Result<Volumes.VolumeTarget>(volumePathResult.Error);
        }

        var (volume, pathComponents) = volumePathResult.Value;
        return await Volumes.VolumeTarget.Create(volume, pathComponents, recursive, createDestDirectory,
            forceOverwrite);
    }

    protected Task<Result<IFileSystemVolume>> MountPartitionFileSystemVolume(Media media,
        PartitionBlock partitionBlock) =>
        new Volumes.VolumeMounter(OnDebugMessage).MountPartitionFileSystemVolume(media, partitionBlock);
}