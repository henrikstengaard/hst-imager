﻿namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        var zipEntryIterator = await GetZipEntryIterator(pathResult.Value, recursive);
        if (zipEntryIterator != null && zipEntryIterator.IsSuccess)
        {
            await ListEntries(zipEntryIterator.Value, pathResult.Value.FileSystemPath);
            return new Result();
        }
        
        // lha
        var lhaEntryIterator = await GetLhaEntryIterator(pathResult.Value, recursive);
        if (lhaEntryIterator != null && lhaEntryIterator.IsSuccess)
        {
            await ListEntries(lhaEntryIterator.Value, pathResult.Value.FileSystemPath);
            return new Result();
        }

        // adf
        var adfEntryIterator = await GetAdfEntryIterator(pathResult.Value, recursive);
        if (adfEntryIterator != null && adfEntryIterator.IsSuccess)
        {
            await ListEntries(adfEntryIterator.Value, pathResult.Value.FileSystemPath);
            return new Result();
        }
        
        // iso
        var iso9660EntryIterator = await GetIso9660EntryIterator(pathResult.Value, recursive);
        if (iso9660EntryIterator != null && iso9660EntryIterator.IsSuccess)
        {
            await ListEntries(iso9660EntryIterator.Value, pathResult.Value.FileSystemPath);
            return new Result();
        }
        
        // disk
        var readableMediaResult = commandHelper.GetReadableMedia(physicalDrives, pathResult.Value.MediaPath);
        if (readableMediaResult.IsFaulted)
        {
            return new Result(readableMediaResult.Error);
        }

        using var media = readableMediaResult.Value;
        var stream = media.Stream;

        var parts = (pathResult.Value.FileSystemPath ?? string.Empty).Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);

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
                case "rdb":
                    if (parts.Length == 1)
                    {
                        var listPartitionsResult = await ListRdbPartitions(stream);
                        return listPartitionsResult.IsFaulted ? new Result(listPartitionsResult.Error) : new Result();
                    }

                    var listRdbEntriesResult = await ListRdbPartitionEntries(stream, parts.Skip(1).ToArray());
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

    private async Task ListPartitionTables(Media media, Stream stream)
    {
        OnDebugMessage($"Listing partition tables");

        var entries = new List<Entry>();
        var diskInfo = await commandHelper.ReadDiskInfo(media, stream);

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

        using var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
            
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
        using var disk = media is DiskMedia diskMedia ? diskMedia.Disk : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);
            
        var mbrFileSystemResult = await MountMbrFileSystem(disk, parts[0]);
        if (mbrFileSystemResult.IsFaulted)
        {
            return new Result(mbrFileSystemResult.Error);
        }

        var fileSystemPath = string.Join("/", parts.Skip(1));
        var entryIterator = new FatEntryIterator(media, mbrFileSystemResult.Value, fileSystemPath, recursive);

        await ListEntries(entryIterator, fileSystemPath);

        return new Result();
    }
    
    private async Task<Result> ListRdbPartitions(Stream stream)
    {
        OnDebugMessage("Reading Rigid Disk Block");

        var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);

        if (rigidDiskBlock == null)
        {
            return new Result<IEnumerable<Entry>>(new List<Entry>());
        }

        OnEntriesRead(new EntriesInfo
        {
            Path = path,
            Entries = rigidDiskBlock.PartitionBlocks.Select(x => new Entry
            {
                Name = x.DriveName,
                FormattedName = x.DriveName,
                Type = EntryType.Dir,
                Size = x.PartitionSize,
                Properties = new Dictionary<string, string>
                {
                    { "DosType", string.Join(", ", x.PartitionSize.FormatBytes(), $"{x.DosTypeHex} ({x.DosTypeFormatted})") }
                }
            }).ToList()
        });

        return new Result();
    }

    private async Task<Result> ListRdbPartitionEntries(Stream stream, string[] parts)
    {
        var volumeResult = await MountRdbFileSystemVolume(stream, parts[0]);
        if (volumeResult.IsFaulted)
        {
            return new Result(volumeResult.Error);
        }

        var fileSystemPath = string.Join("/", parts.Skip(1));
        var entryIterator = new AmigaVolumeEntryIterator(stream, fileSystemPath, volumeResult.Value, recursive);

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