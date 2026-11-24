using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Ext;
using DiscUtils.ExFat;
using DiscUtils.Fat;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using Hst.Amiga.FileSystems;
using Hst.Amiga.FileSystems.FastFileSystem;
using Hst.Amiga.FileSystems.Pfs3;
using Hst.Amiga.FileSystems.SmartFileSystem;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Core;
using Hst.Core.Extensions;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Volumes;

/// <summary>
/// Mounts file systems from media, partition tables and partitions.
/// </summary>
/// <param name="debugMessage">Action to report debug messages.</param>
public partial class VolumeMounter(Action<string> debugMessage = null)
{
    private void OnDebugMessage(string message) => debugMessage?.Invoke(message);

    /// <summary>
    /// Mount ADF file system volume using Fast File System.
    /// </summary>
    public async Task<Result<IFileSystemVolume>> MountAdfFileSystemVolume(Stream stream)
    {
        OnDebugMessage("Mounting ADF file system volume using Fast File System");

        var fileSystemVolume = await FastFileSystemVolume.MountAdf(stream);

        OnDebugMessage($"Volume '{fileSystemVolume.Name}'");

        return new Result<IFileSystemVolume>(fileSystemVolume);
    }

    /// <summary>
    /// Mount file system in Master Boot Record partition.
    /// </summary>
    public async Task<Result<(int, IFileSystem)>> MountMbrFileSystem(VirtualDisk disk, string partitionNumber)
    {
        if (!int.TryParse(partitionNumber, out var partitionNumberIntValue))
        {
            return new Result<(int, IFileSystem)>(new Error($"Invalid partition number '{partitionNumber}'"));
        }

        OnDebugMessage("Reading Master Boot Record");

        BiosPartitionTable biosPartitionTable;
        try
        {
            biosPartitionTable = new BiosPartitionTable(disk);
        }
        catch (Exception)
        {
            return new Result<(int, IFileSystem)>(new Error("Master Boot Record not found"));
        }

        OnDebugMessage($"Partition number '{partitionNumber}'");

        if (partitionNumberIntValue < 0 || partitionNumberIntValue - 1 > biosPartitionTable.Partitions.Count)
        {
            return new Result<(int, IFileSystem)>(new Error(
                $"Invalid partition number {partitionNumber}, expected to between 1-{biosPartitionTable.Partitions.Count}"));
        }

        var partitionInfo = biosPartitionTable.Partitions[partitionNumberIntValue - 1];

        OnDebugMessage("Mounting file system");

        var stream = partitionInfo.Open();

        var fileSystemResult = await MountFileSystem(stream);
        return fileSystemResult.IsFaulted
            ? new Result<(int, IFileSystem)>(fileSystemResult.Error)
            : new Result<(int, IFileSystem)>((partitionNumberIntValue, fileSystemResult.Value));
    }

    /// <summary>
    /// Mount file system in Guid Partition Table partition.
    /// </summary>
    public async Task<Result<(int, IFileSystem)>> MountGptFileSystem(VirtualDisk disk, string partitionNumber)
    {
        if (!int.TryParse(partitionNumber, out var partitionNumberIntValue))
        {
            return new Result<(int, IFileSystem)>(new Error($"Invalid partition number '{partitionNumber}'"));
        }

        OnDebugMessage("Reading Guid Partition Table");

        GuidPartitionTable guidPartitionTable;
        try
        {
            guidPartitionTable = new GuidPartitionTable(disk);
        }
        catch (Exception)
        {
            return new Result<(int, IFileSystem)>(new Error("Guid Partition Table not found"));
        }

        OnDebugMessage($"Partition number '{partitionNumber}'");

        if (partitionNumberIntValue < 0 || partitionNumberIntValue - 1 > guidPartitionTable.Partitions.Count)
        {
            return new Result<(int, IFileSystem)>(new Error(
                $"Invalid partition number {partitionNumber}, expected to between 1-{guidPartitionTable.Partitions.Count}"));
        }

        var partitionInfo = guidPartitionTable.Partitions[partitionNumberIntValue - 1];

        OnDebugMessage("Mounting file system");

        var stream = partitionInfo.Open();

        var fileSystemResult = await MountFileSystem(stream);
        return fileSystemResult.IsFaulted
            ? new Result<(int, IFileSystem)>(fileSystemResult.Error)
            : new Result<(int, IFileSystem)>((partitionNumberIntValue, fileSystemResult.Value));
    }

    /// <summary>
    /// Mount file system (ext, FAT, exFAT, NTFS) in stream.
    /// </summary>
    public static async Task<Result<IFileSystem>> MountFileSystem(Stream stream)
    {
        // read first 64kb block bytes of partition
        var blockBytes = await stream.ReadBytes(512 * 128);

        // ext super block magic signature
        if (blockBytes[1024 + 0x38] == 0x53 && blockBytes[1024 + 0x39] == 0xEF)
        {
            return new Result<IFileSystem>(new ExtFileSystem(stream));
        }

        if (FatFileSystem.Detect(stream))
        {
            return new Result<IFileSystem>(new FatFileSystem(stream));
        }

        if (ExFatFileSystem.Detect(stream))
        {
            return new Result<IFileSystem>(new ExFatFileSystem(stream));
        }

        if (NtfsFileSystem.Detect(stream))
        {
            return new Result<IFileSystem>(new NtfsFileSystem(stream));
        }

        return new Result<IFileSystem>(new Error("Unsupported Master Boot Record file system"));
    }

    /// <summary>
    /// Mount file system volume in Rigid Disk Block partition by partition number or device name.
    /// </summary>
    public async Task<Result<(int, IFileSystemVolume)>> MountRdbFileSystemVolume(Media media, string partition)
    {
        OnDebugMessage("Reading Rigid Disk Block");

        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(media);

        if (rigidDiskBlock == null)
        {
            return new Result<(int, IFileSystemVolume)>(new Error("No rigid disk block"));
        }

        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();

        OnDebugMessage($"Partition '{partition}'");

        var partitionNumberMatch = NumberRegex().Match(partition);

        var partitionBlock = partitionNumberMatch.Success &&
                             int.TryParse(partition, out var partitionNumber) &&
                             partitionNumber >= 1 &&
                             partitionNumber <= partitionBlocks.Count
            ? partitionBlocks[partitionNumber - 1]
            : partitionBlocks.FirstOrDefault(x =>
                x.DriveName.Equals(partition, StringComparison.OrdinalIgnoreCase));

        if (partitionBlock == null)
        {
            return new Result<(int, IFileSystemVolume)>(new Error($"Partition '{partition}' not found"));
        }

        OnDebugMessage("Mounting file system");

        var fileSystemVolumeResult = await MountPartitionFileSystemVolume(media, partitionBlock);
        if (fileSystemVolumeResult.IsFaulted)
        {
            return new Result<(int, IFileSystemVolume)>(fileSystemVolumeResult.Error);
        }

        var fileSystemVolume = fileSystemVolumeResult.Value;
        if (fileSystemVolume == null)
        {
            return new Result<(int, IFileSystemVolume)>(new Error("No file system volume mounted"));
        }

        OnDebugMessage($"Volume '{fileSystemVolume.Name}'");

        return new Result<(int, IFileSystemVolume)>((partitionBlocks.IndexOf(partitionBlock), fileSystemVolume));
    }

    /// <summary>
    /// Mount file system volume in Rigid Disk Block partition block.
    /// </summary>
    public async Task<Result<IFileSystemVolume>> MountPartitionFileSystemVolume(Media media,
        PartitionBlock partitionBlock)
    {
        var stream = MediaHelper.GetStreamFromMedia(media);

        switch (partitionBlock.DosTypeFormatted)
        {
            case "DOS\\0":
            case "DOS\\1":
            case "DOS\\2":
            case "DOS\\3":
            case "DOS\\4":
            case "DOS\\5":
            case "DOS\\6":
            case "DOS\\7":
                return new Result<IFileSystemVolume>(await FastFileSystemVolume.MountPartition(stream, partitionBlock));
            case "PDS\\3":
            case "PFS\\3":
                return new Result<IFileSystemVolume>(await Pfs3Volume.Mount(stream, partitionBlock));
            case "SFS\\0":
                return new Result<IFileSystemVolume>(await SmartFileSystemVolume.Mount(stream, partitionBlock));
        }

        return new Result<IFileSystemVolume>(new Error($"Unsupported file system '{partitionBlock.DosTypeFormatted}'"));
    }

    [GeneratedRegex("^\\d+$", RegexOptions.IgnoreCase, "en-DK")]
    private static partial Regex NumberRegex();
}
