using DiscUtils;
using Hst.Imager.Core.Caching;
using Hst.Imager.Core.MagicBytes;
using Hst.Imager.Core.UaeMetadatas;
using Hst.Imager.Core.Volumes;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Amiga.RigidDiskBlocks;
using DiscUtils.Partitions;
using Extensions;
using Hst.Core;
using Helpers;
using PartitionTables;
using Microsoft.Extensions.Logging;
using Models;
using Entry = Models.FileSystems.Entry;
using EntryType = Models.FileSystems.EntryType;

public class FsDirCommand(
    ILogger<FsDirCommand> logger,
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string path,
    bool recursive,
    UaeMetadata uaeMetadata = UaeMetadata.None)
    : FsCommandBase(commandHelper, physicalDrives)
{
    private readonly ILogger<FsDirCommand> logger = logger;

    public event EventHandler<EntriesInfoReadEventArgs> EntriesRead;

    public override async Task<Result> Execute(CancellationToken token)
    {
        using var appCache = new MemoryAppCache();
        var uaeMetadataHelper = new UaeMetadataHelper(appCache);
        using var volumeFactory = new VolumeFactory(commandHelper, physicalDrives, uaeMetadataHelper, uaeMetadata,
            OnDebugMessage);

        OnDebugMessage($"Opening '{path}' as readable");

        // list partition tables or partitions, if path is a disk without a file system path or only a partition table
        var listPartitionsResult = await ListPartitionTablesOrPartitions();
        if (listPartitionsResult != null)
        {
            return listPartitionsResult;
        }

        var volumePathResult = await volumeFactory.Open(path, VolumeAccess.Read);
        if (volumePathResult.IsFaulted)
        {
            return new Result(volumePathResult.Error);
        }

        var (volume, pathComponents) = volumePathResult.Value;

        var walkerResult = await VolumeEntryWalker.Create(volume, pathComponents, recursive);
        if (walkerResult.IsFaulted)
        {
            return new Result(walkerResult.Error);
        }

        await ListEntries(walkerResult.Value);

        return new Result();
    }

    /// <summary>
    /// List partition tables or partitions, if path is a disk without a file system path or only a partition table.
    /// </summary>
    /// <returns>Result of listing partition tables or partitions. Null, if path is not partition tables or partitions.</returns>
    private async Task<Result> ListPartitionTablesOrPartitions()
    {
        var pathResult = commandHelper.ResolveMedia(path);
        if (pathResult.IsFaulted ||
            (string.IsNullOrWhiteSpace(pathResult.Value.FileSystemPath) && Directory.Exists(path)))
        {
            return null;
        }

        var parts = (pathResult.Value.FileSystemPath ?? string.Empty).Split(pathResult.Value.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);

        // partition tables are listed for no parts, partitions are listed for a partition table part or
        // a pistorm rdb partition table in a mbr partition, e.g. "mbr/2/rdb"
        if (parts.Length > 3 ||
            (parts.Length > 0 && parts[0].ToLowerInvariant() is not ("mbr" or "gpt" or "rdb")))
        {
            return null;
        }

        var dataType = await commandHelper.DetectDataType(pathResult.Value.MediaPath);
        if (dataType is DataType.Zip or DataType.Lha or DataType.Lzx or DataType.Lzw or DataType.Iso9660 or
            DataType.Adf)
        {
            return null;
        }

        OnDebugMessage($"Media Path: '{pathResult.Value.MediaPath}'");
        OnDebugMessage($"File System Path: '{pathResult.Value.FileSystemPath}'");

        var readableMediaResult = await commandHelper.GetReadableMedia(physicalDrives, pathResult.Value.MediaPath);
        if (readableMediaResult.IsFaulted)
        {
            return new Result(readableMediaResult.Error);
        }

        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(readableMediaResult.Value,
            pathResult.Value.FileSystemPath ?? string.Empty, pathResult.Value.DirectorySeparatorChar);

        using var media = piStormRdbMediaResult.Media;
        parts = (piStormRdbMediaResult.FileSystemPath ?? string.Empty).Split(
            pathResult.Value.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        // floppy entries are listed using volume
        if (media.Type == Media.MediaType.Floppy || parts.Length > 1)
        {
            return null;
        }

        if (parts.Length == 0)
        {
            await ListPartitionTables(media);
            return new Result();
        }

        return parts[0].ToLowerInvariant() switch
        {
            "mbr" => await ListMbrPartitions(media),
            "gpt" => await ListGptPartitions(media),
            _ => await ListRdbPartitions(media)
        };
    }

    private async Task ListPartitionTables(Media media)
    {
        OnDebugMessage($"Listing partition tables");

        var entries = new List<Entry>();

        switch(media)
        {
            case PiStormRdbMedia piStormRdbMedia:
                entries.AddRange(await GetPartitionTablesFromPiStormRdb(piStormRdbMedia));
                break;
            default:
                entries.AddRange(await GetPartitionTablesFromMedia(media));
                break;
        }

        OnEntriesRead(new EntriesInfo
        {
            Path = path,
            Entries = entries
        });
    }

    private async Task<IEnumerable<Entry>> GetPartitionTablesFromPiStormRdb(PiStormRdbMedia piStormRdbMedia)
    {
        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(piStormRdbMedia);

        return GetPartitionTablesFromRdb(rigidDiskBlock);
    }

    private IEnumerable<Entry> GetPartitionTablesFromRdb(RigidDiskBlock rigidDiskBlock)
    {
        if (rigidDiskBlock == null)
        {
            yield break;
        }

        yield return new Entry
        {
            Name = "RDB",
            FormattedName = "RDB",
            Type = EntryType.Dir,
            Size = rigidDiskBlock.DiskSize
        };
    }

    private async Task<IEnumerable<Entry>> GetPartitionTablesFromMedia(Media media)
    {
        var entries = new List<Entry>();

        var diskInfo = await commandHelper.ReadDiskInfo(media);

        if (diskInfo.MbrPartitionTablePart != null)
        {
            entries.Add(new Entry
            {
                Name = "MBR",
                FormattedName = "MBR",
                Type = EntryType.Dir,
                Size = diskInfo.MbrPartitionTablePart.Size
            });
        }

        if (diskInfo.GptPartitionTablePart != null)
        {
            entries.Add(new Entry
            {
                Name = "GPT",
                FormattedName = "GPT",
                Type = EntryType.Dir,
                Size = diskInfo.GptPartitionTablePart.Size
            });
        }

        if (diskInfo.RdbPartitionTablePart != null)
        {
            entries.Add(new Entry
            {
                Name = "RDB",
                FormattedName = "RDB",
                Type = EntryType.Dir,
                Size = diskInfo.RdbPartitionTablePart.Size
            });
        }

        return entries;
    }

    private async Task<Result> ListMbrPartitions(Media media)
    {
        OnDebugMessage("Reading Master Boot Record");

        var disk = await MediaHelper.ResolveVirtualDisk(media);
            
        BiosPartitionTable biosPartitionTable;
        try
        {
            biosPartitionTable = new BiosPartitionTable(disk);
        }
        catch (Exception)
        {
            return new Result(new Error("Master Boot Record not found"));
        }

        var partitionNumber = 0;

        var entries = new List<Entry>();
        foreach (var partition in biosPartitionTable.Partitions)
        {
            var partitionNumberFormatted = (++partitionNumber).ToString();

            var partitionType = MbrPartitionTableReader.GetPartitionType(partition);

            entries.Add(new Entry
            {
                Name = partitionNumberFormatted,
                FormattedName = partitionNumberFormatted,
                Type = EntryType.Dir,
                Size = 0,
                Properties = await GetPartitionProperties(disk, partition)
            });
        }
        
        OnEntriesRead(new EntriesInfo
        {
            Path = path,
            Entries = entries
        });

        return new Result();
    }
    
    
    private async Task<Result> ListGptPartitions(Media media)
    {
        OnDebugMessage("Reading Guid Partition Table");

        var disk = await MediaHelper.ResolveVirtualDisk(media);
            
        GuidPartitionTable guidPartitionTable;
        try
        {
            guidPartitionTable = new GuidPartitionTable(disk);
        }
        catch (Exception)
        {
            return new Result(new Error("Guid Partition Table not found"));
        }

        var partitionNumber = 0;

        var entries = new List<Entry>();
        foreach (var partition in guidPartitionTable.Partitions)
        {
            var partitionNumberFormatted = (++partitionNumber).ToString();
            entries.Add(new Entry
            {
                Name = partitionNumberFormatted,
                FormattedName = partitionNumberFormatted,
                Type = EntryType.Dir,
                Size = 0,
                Properties = await GetPartitionProperties(disk, partition)
            });
        }
        
        OnEntriesRead(new EntriesInfo
        {
            Path = path,
            Entries = entries
        });

        return new Result();
    }

    private async Task<Result> ListRdbPartitions(Media media)
    {
        OnDebugMessage("Reading Rigid Disk Block");

        var rigidDiskBlock = await MediaHelper.ReadRigidDiskBlockFromMedia(media);

        if (rigidDiskBlock == null)
        {
            return new Result<IEnumerable<Entry>>(new List<Entry>());
        }

        var entries = new List<Entry>();
        var partitionNumber = 0;
        foreach (var partitionBlock in rigidDiskBlock.PartitionBlocks)
        {
            partitionNumber++;
            entries.Add(new Entry
            {
                Name = partitionNumber.ToString(),
                FormattedName = partitionNumber.ToString(),
                Type = EntryType.Dir,
                Size = partitionBlock.PartitionSize,
                Properties = await GetRdbPartitionProperties(media, partitionBlock)
            });
        }
        
        OnEntriesRead(new EntriesInfo
        {
            Path = path,
            Entries = entries
        });

        return new Result();
    }

    private async Task<Dictionary<string, string>> GetPartitionProperties(VirtualDisk disk,
    DiscUtils.Partitions.PartitionInfo partitionInfo)
    {
        var fileSystemInfo = await FileSystemReader.ReadFileSystem(disk, partitionInfo);

        return new Dictionary<string, string>
        {
            { "File System", $"{fileSystemInfo.FileSystemType}" },
            { "Size", $"{(partitionInfo.SectorCount * disk.SectorSize).FormatBytes()}" }
        };
    }

    private async Task<Dictionary<string, string>> GetRdbPartitionProperties(Media media, PartitionBlock partitionBlock)
    {
        IFileSystemVolume fileSystemVolume = null;
        try
        {
            var fileSystemVolumeResult = await MountPartitionFileSystemVolume(media, partitionBlock);
            fileSystemVolume = fileSystemVolumeResult.IsSuccess ? fileSystemVolumeResult.Value : null;
        }
        catch (Exception)
        {
            // ignored
        }

        var properties = new Dictionary<string, string>
        {
            { "Device Name", partitionBlock.DriveName },
            { "Dos Type", $"{partitionBlock.DosTypeHex} ({partitionBlock.DosTypeFormatted})" }
        };

        if (fileSystemVolume == null)
        {
            return properties;
        }
        properties.Add("Volume Name", fileSystemVolume.Name);
        properties.Add("File system size", fileSystemVolume.Size.FormatBytes());
        properties.Add("File system free", fileSystemVolume.Free.FormatBytes());
        properties.Add("File system used", (fileSystemVolume.Size - fileSystemVolume.Free).FormatBytes());

        return properties;
    }

    private async Task ListEntries(VolumeEntryWalker walker)
    {
        var entries = new List<Entry>();

        while (await walker.Next())
        {
            entries.Add(walker.Current);
        }

        OnEntriesRead(new EntriesInfo
        {
            Path = path,
            Recursive = recursive,
            Entries = entries,
            AttributesMode = walker.Volume.AttributesMode
        });
    }

    private void OnEntriesRead(EntriesInfo entriesInfo)
    {
        EntriesRead?.Invoke(this, new EntriesInfoReadEventArgs(entriesInfo));
    }
}