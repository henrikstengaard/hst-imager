namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Microsoft.Extensions.Logging;
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

        var pathResult = ResolveMedia(path);
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
        var readableMediaResult =
            commandHelper.GetReadableMedia(physicalDrives, pathResult.Value.MediaPath, allowPhysicalDrive: true);
        if (readableMediaResult.IsFaulted)
        {
            return new Result(readableMediaResult.Error);
        }

        using var media = readableMediaResult.Value;
        var stream = media.Stream;

        var parts = (pathResult.Value.FileSystemPath ?? string.Empty).Split(new []{'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0 || string.IsNullOrEmpty(parts[0]))
        {
            await ListPartitionTables(stream);
        }
        else
        {
            switch (parts[0])
            {
                case "rdb":
                    if (parts.Length == 1)
                    {
                        var listPartitionsResult = await ListRdbPartitions(stream, pathResult.Value.FileSystemPath);
                        if (listPartitionsResult.IsFaulted)
                        {
                            return new Result(listPartitionsResult.Error);
                        }
                        return new Result();
                    }

                    var listEntriesResult = await ListRdbPartitionEntries(stream, parts.Skip(1).ToArray());
                    if (listEntriesResult.IsFaulted)
                    {
                        return new Result(listEntriesResult.Error);
                    }

                    break;
                default:
                    return new Result(new Error($"Unsupported partition table '{parts[0]}'"));
            }
        }

        return new Result();
    }

    private async Task<Result> ListPartitionTables(Stream stream)
    {
        OnDebugMessage($"Listing partition tables");

        var entries = new List<Entry>();
        var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);

        if (rigidDiskBlock != null)
        {
            entries.Add(new Entry
            {
                Name = "RDB",
                FormattedName = "RDB", 
                Type = EntryType.Dir,
                Size = 0
            });
        }

        OnEntriesRead(new EntriesInfo
        {
            Path = path,
            Entries = entries
        });

        return new Result();
    }

    private async Task<Result> ListRdbPartitions(Stream stream, string fileSystemPath)
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
                Size = 0
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