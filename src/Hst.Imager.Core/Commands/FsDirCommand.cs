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
        OnDebugMessage($"Virtual Path: '{pathResult.Value.VirtualPath}'");

        // adf
        if (System.IO.File.Exists(pathResult.Value.MediaPath) &&
            (Path.GetExtension(pathResult.Value.MediaPath) ?? string.Empty).Equals(".adf",
                StringComparison.OrdinalIgnoreCase))
        {
            var adfStream = System.IO.File.OpenRead(pathResult.Value.MediaPath);
            var fileSystemVolumeResult = await MountAdfFileSystemVolume(adfStream);
            if (fileSystemVolumeResult.IsFaulted)
            {
                return new Result<IEntryIterator>(fileSystemVolumeResult.Error);
            }

            var entryIterator = new AmigaVolumeEntryIterator(adfStream,
                pathResult.Value.VirtualPath, fileSystemVolumeResult.Value, recursive);
            await ListEntries(entryIterator, pathResult.Value.VirtualPath);
            return new Result();
        }

        var mediaResult =
            commandHelper.GetReadableMedia(physicalDrives, pathResult.Value.MediaPath, allowPhysicalDrive: true);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }

        using var media = mediaResult.Value;
        await using var stream = media.Stream;

        var parts = (pathResult.Value.VirtualPath ?? string.Empty).Split(new[] { '\\', '/' });

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
                        var listPartitionsResult = await ListRdbPartitions(stream, pathResult.Value.VirtualPath);
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
                Type = EntryType.Dir,
                Size = 0
            });
        }

        OnEntriesRead(new EntriesInfo
        {
            DiskPath = path,
            FileSystemPath = string.Empty,
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
            DiskPath = path,
            FileSystemPath = fileSystemPath,
            Entries = rigidDiskBlock.PartitionBlocks.Select(x => new Entry
            {
                Name = x.DriveName,
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
        var dirs = new List<Entry>();
        var files = new List<Entry>();

        while (await entryIterator.Next())
        {
            var entry = entryIterator.Current;
            switch (entry.Type)
            {
                case EntryType.Dir:
                    dirs.Add(entry);
                    break;
                case EntryType.File:
                    files.Add(entry);
                    break;
            }
        }

        OnEntriesRead(new EntriesInfo
        {
            DiskPath = path,
            FileSystemPath = fileSystemPath,
            Entries = dirs.OrderBy(x => x.Name).Concat(files.OrderBy(x => x.Name)).ToList()
        });
    }

    private void OnEntriesRead(EntriesInfo entriesInfo)
    {
        EntriesRead?.Invoke(this, new EntriesInfoReadEventArgs(entriesInfo));
    }
}