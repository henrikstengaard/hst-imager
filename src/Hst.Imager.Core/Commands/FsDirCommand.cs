using DiscUtils;

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
using DiscUtils.Streams;
using Extensions;
using Hst.Core;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.PartitionTables;
using Microsoft.Extensions.Logging;
using Models;
using Entry = Models.FileSystems.Entry;
using EntryType = Models.FileSystems.EntryType;

public class FsDirCommand : FsCommandBase
{
    private readonly ILogger<FsDirCommand> logger;
    private readonly string path;
    private readonly bool recursive;

    public FsDirCommand(ILogger<FsDirCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, bool recursive)
        : base(commandHelper, physicalDrives)
    {
        this.logger = logger;
        this.path = path;
        this.recursive = recursive;
    }

    public event EventHandler<EntriesInfoReadEventArgs> EntriesRead;

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnDebugMessage($"Opening '{path}' as readable");

        var pathResult = commandHelper.ResolveMedia(path);
        if (pathResult.IsFaulted)
        {
            return new Result(pathResult.Error);
        }

        OnDebugMessage($"Media Path: '{pathResult.Value.MediaPath}'");
        OnDebugMessage($"File System Path: '{pathResult.Value.FileSystemPath}'");

        // zip
        var zipEntryIteratorResult = await GetZipEntryIterator(pathResult.Value, recursive);
        if (zipEntryIteratorResult != null && zipEntryIteratorResult.IsSuccess)
        {
            using var zipEntryIterator = zipEntryIteratorResult.Value;
            await ListEntries(zipEntryIterator, pathResult.Value.FileSystemPath);
            return new Result();
        }
        
        // lha
        var lhaEntryIteratorResult = await GetLhaEntryIterator(pathResult.Value, recursive);
        if (lhaEntryIteratorResult != null && lhaEntryIteratorResult.IsSuccess)
        {
            using var lhaEntryIterator = lhaEntryIteratorResult.Value;
            await ListEntries(lhaEntryIterator, pathResult.Value.FileSystemPath);
            return new Result();
        }

        // lzx
        var lzxEntryIteratorResult = await GetLzxEntryIterator(pathResult.Value, recursive);
        if (lzxEntryIteratorResult != null && lzxEntryIteratorResult.IsSuccess)
        {
            using var lzxEntryIterator = lzxEntryIteratorResult.Value;
            await ListEntries(lzxEntryIterator, pathResult.Value.FileSystemPath);
            return new Result();
        }

        // adf
        var adfEntryIteratorResult = await GetAdfEntryIterator(pathResult.Value, recursive);
        if (adfEntryIteratorResult != null && adfEntryIteratorResult.IsSuccess)
        {
            using var adfEntryIterator = adfEntryIteratorResult.Value;
            await ListEntries(adfEntryIterator, pathResult.Value.FileSystemPath);
            return new Result();
        }
        
        // iso
        var iso9660EntryIteratorResult = await GetIso9660EntryIterator(pathResult.Value, recursive);
        if (iso9660EntryIteratorResult != null && iso9660EntryIteratorResult.IsSuccess)
        {
            using var iso9660EntryIterator = iso9660EntryIteratorResult.Value;
            await ListEntries(iso9660EntryIterator, pathResult.Value.FileSystemPath);
            return new Result();
        }
        
        // floppy or disk
        var readableMediaResult = await commandHelper.GetReadableMedia(physicalDrives, pathResult.Value.MediaPath);
        if (readableMediaResult.IsFaulted)
        {
            return new Result(readableMediaResult.Error);
        }

        var fileSystemPath = pathResult.Value.FileSystemPath ?? string.Empty;
        var directorySeparatorChar = pathResult.Value.DirectorySeparatorChar;

        var piStormRdbMediaResult = MediaHelper.GetPiStormRdbMedia(
            readableMediaResult.Value, fileSystemPath, directorySeparatorChar);

        var media = piStormRdbMediaResult.Media;
        fileSystemPath = piStormRdbMediaResult.FileSystemPath;

        var stream = media.Stream;

        var parts = fileSystemPath.Split(new []{ directorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        if (media.Type == Media.MediaType.Floppy)
        {
            var listFloppyEntriesResult = await ListFloppyEntries(media, parts);
            return listFloppyEntriesResult.IsFaulted ? new Result(listFloppyEntriesResult.Error) : new Result();
        }

        if (parts.Length == 0 || string.IsNullOrEmpty(parts[0]))
        {
            await ListPartitionTables(media, stream);
        }
        else
        {
            switch (parts[0].ToLowerInvariant())
            {
                case "mbr":
                    if (parts.Length == 1)
                    {
                        var listPartitionsResult = await ListMbrPartitions(media);
                        return listPartitionsResult.IsFaulted ? new Result(listPartitionsResult.Error) : new Result();
                    }

                    var listMbrEntriesResult = await ListMbrPartitionEntries(media, parts.Skip(1).ToArray());
                    if (listMbrEntriesResult.IsFaulted)
                    {
                        return new Result(listMbrEntriesResult.Error);
                    }

                    break;
                case "gpt":
                    if (parts.Length == 1)
                    {
                        var listPartitionsResult = await ListGptPartitions(media);
                        return listPartitionsResult.IsFaulted ? new Result(listPartitionsResult.Error) : new Result();
                    }

                    var listGptEntriesResult = await ListGptPartitionEntries(media, parts.Skip(1).ToArray());
                    if (listGptEntriesResult.IsFaulted)
                    {
                        return new Result(listGptEntriesResult.Error);
                    }

                    break;
                case "rdb":
                    if (parts.Length == 1)
                    {
                        var listPartitionsResult = await ListRdbPartitions(media);
                        return listPartitionsResult.IsFaulted ? new Result(listPartitionsResult.Error) : new Result();
                    }

                    var listRdbEntriesResult = await ListRdbPartitionEntries(media, parts.Skip(1).ToArray());
                    if (listRdbEntriesResult.IsFaulted)
                    {
                        return new Result(listRdbEntriesResult.Error);
                    }

                    break;
                default:
                    return new Result(new Error($"Unsupported partition table '{parts[0]}'"));
            }
        }

        return new Result();
    }

    private async Task<Result> ListFloppyEntries(Media media, string[] parts)
    {
        var mbrFileSystemResult = await MountFileSystem(media.Stream);
        if (mbrFileSystemResult.IsFaulted)
        {
            return new Result(mbrFileSystemResult.Error);
        }

        var fileSystemPath = string.Join("/", parts);
        var entryIterator = new FileSystemEntryIterator(media, mbrFileSystemResult.Value, fileSystemPath, recursive);

        await ListEntries(entryIterator, fileSystemPath);

        return new Result();
    }

    private async Task ListPartitionTables(Media media, Stream stream)
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

        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
            
        BiosPartitionTable biosPartitionTable;
        try
        {
            biosPartitionTable = new BiosPartitionTable(disk);
        }
        catch (Exception)
        {
            return new Result<IEntryIterator>(new Error("Master Boot Record not found"));
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
    
    private async Task<Result> ListMbrPartitionEntries(Media media, string[] parts)
    {
        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
            
        var mbrFileSystemResult = await MountMbrFileSystem(disk, parts[0]);
        if (mbrFileSystemResult.IsFaulted)
        {
            return new Result(mbrFileSystemResult.Error);
        }

        var fileSystemPath = string.Join("/", parts.Skip(1));
        var entryIterator = new FileSystemEntryIterator(media, mbrFileSystemResult.Value, fileSystemPath, recursive);

        await ListEntries(entryIterator, fileSystemPath);

        return new Result();
    }
    
    private async Task<Result> ListGptPartitionEntries(Media media, string[] parts)
    {
        var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
            
        var gptFileSystemResult = await MountGptFileSystem(disk, parts[0]);
        if (gptFileSystemResult.IsFaulted)
        {
            return new Result(gptFileSystemResult.Error);
        }

        var fileSystemPath = string.Join("/", parts.Skip(1));
        var entryIterator = new FileSystemEntryIterator(media, gptFileSystemResult.Value, fileSystemPath, recursive);

        await ListEntries(entryIterator, fileSystemPath);

        return new Result();
    }

    private async Task<Result> ListGptPartitions(Media media)
    {
        OnDebugMessage("Reading Guid Partition Table");

        var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
            
        GuidPartitionTable guidPartitionTable;
        try
        {
            guidPartitionTable = new GuidPartitionTable(disk);
        }
        catch (Exception)
        {
            return new Result<IEntryIterator>(new Error("Guid Partition Table not found"));
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

    private async Task<Result> ListRdbPartitionEntries(Media media, string[] parts)
    {
        var volumeResult = await MountRdbFileSystemVolume(media, parts[0]);
        if (volumeResult.IsFaulted)
        {
            return new Result(volumeResult.Error);
        }

        var fileSystemPath = string.Join("/", parts.Skip(1));
        var entryIterator = new AmigaVolumeEntryIterator(media, media.Stream, fileSystemPath, volumeResult.Value, recursive);

        await ListEntries(entryIterator, fileSystemPath);

        return new Result();
    }

    private async Task ListEntries(IEntryIterator entryIterator, string fileSystemPath)
    {
        var entries = new List<Entry>();

        while (await entryIterator.Next())
        {
            var entry = entryIterator.Current;
            entries.Add(entry);
        }

        OnEntriesRead(new EntriesInfo
        {
            Path = path,
            Recursive = recursive,
            Entries = entries
        });
    }

    private void OnEntriesRead(EntriesInfo entriesInfo)
    {
        EntriesRead?.Invoke(this, new EntriesInfoReadEventArgs(entriesInfo));
    }
}