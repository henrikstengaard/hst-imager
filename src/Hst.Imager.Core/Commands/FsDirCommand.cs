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

        using var media = readableMediaResult.Value;
        var stream = media.Stream;

        var parts = (pathResult.Value.FileSystemPath ?? string.Empty).Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);

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

        OnEntriesRead(new EntriesInfo
        {
            Path = path,
            Entries = entries
        });
    }

    private Task<Result> ListMbrPartitions(Media media)
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
            return Task.FromResult<Result>(new Result<IEntryIterator>(new Error("Master Boot Record not found")));
        }

        var partitionNumber = 0;

        var entries = new List<Entry>();
        foreach (var partition in biosPartitionTable.Partitions)
        {
            var partitionNumberFormatted = (++partitionNumber).ToString();
            entries.Add(new Entry
            {
                Name = partitionNumberFormatted,
                FormattedName = partitionNumberFormatted,
                Type = EntryType.Dir,
                Size = 0,
                Properties = new Dictionary<string, string>
                {
                    { "Info", string.Join(", ", (partition.SectorCount * 512).FormatBytes(), partition.TypeAsString) }
                }
            });
        }
        
        OnEntriesRead(new EntriesInfo
        {
            Path = path,
            Entries = entries
        });

        return Task.FromResult(new Result());
    }
    
    private async Task<Result> ListMbrPartitionEntries(Media media, string[] parts)
    {
        var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
            
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

    private Task<Result> ListGptPartitions(Media media)
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
            return Task.FromResult<Result>(new Result<IEntryIterator>(new Error("Guid Partition Table not found")));
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
                Properties = new Dictionary<string, string>
                {
                    { "Info", string.Join(", ", (partition.SectorCount * 512).FormatBytes(), partition.TypeAsString) }
                }
            });
        }
        
        OnEntriesRead(new EntriesInfo
        {
            Path = path,
            Entries = entries
        });

        return Task.FromResult(new Result());
    }
    
    private async Task<Result> ListRdbPartitions(Media media)
    {
        OnDebugMessage("Reading Rigid Disk Block");

        var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);

        var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(disk.Content);

        if (rigidDiskBlock == null)
        {
            return new Result<IEnumerable<Entry>>(new List<Entry>());
        }

        var entries = new List<Entry>();
        foreach (var partitionBlock in rigidDiskBlock.PartitionBlocks)
        {
            entries.Add(new Entry
            {
                Name = partitionBlock.DriveName,
                FormattedName = partitionBlock.DriveName,
                Type = EntryType.Dir,
                Size = partitionBlock.PartitionSize,
                Properties = await GetRdbPartitionProperties(disk, partitionBlock)
            });
        }
        
        OnEntriesRead(new EntriesInfo
        {
            Path = path,
            Entries = entries
        });

        return new Result();
    }

    private async Task<Dictionary<string, string>> GetRdbPartitionProperties(VirtualDisk disk,
        PartitionBlock partitionBlock)
    {
        IFileSystemVolume fileSystemVolume = null;
        try
        {
            var fileSystemVolumeResult = await MountPartitionFileSystemVolume(disk.Content, partitionBlock);
            fileSystemVolume = fileSystemVolumeResult.IsSuccess ? fileSystemVolumeResult.Value : null;
        }
        catch (Exception)
        {
            // ignored
        }

        var properties = new Dictionary<string, string>
        {
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
        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
        
        var volumeResult = await MountRdbFileSystemVolume(disk, parts[0]);
        if (volumeResult.IsFaulted)
        {
            return new Result(volumeResult.Error);
        }

        var fileSystemPath = string.Join("/", parts.Skip(1));
        var entryIterator = new AmigaVolumeEntryIterator(media.Stream, fileSystemPath, volumeResult.Value, recursive);

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