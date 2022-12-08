namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Amiga.FileSystems.FastFileSystem;
using Amiga.FileSystems.Pfs3;
using Amiga.RigidDiskBlocks;
using Hst.Core;
using Microsoft.Extensions.Logging;
using Entry = Models.FileSystems.Entry;
using EntryType = Models.FileSystems.EntryType;

public class FsDirCommand : CommandBase
{
    private readonly ILogger<FsDirCommand> logger;
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string path;
    private readonly string fsPath;

    public FsDirCommand(ILogger<FsDirCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, string fsPath)
    {
        this.logger = logger;
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
        this.fsPath = fsPath;
    }

    public event EventHandler<EntriesInfoReadEventArgs> EntriesRead;

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnDebugMessage($"Opening '{path}' as readable");

        var mediaResult = commandHelper.GetReadableMedia(physicalDrives, path, allowPhysicalDrive: true);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }

        using var media = mediaResult.Value;
        await using var stream = media.Stream;

        var parts = (fsPath ?? string.Empty).Split('/');

        var entriesResult = await ReadEntries(stream, parts);
        if (entriesResult.IsFaulted)
        {
            return new Result(entriesResult.Error);
        }

        OnEntriesRead(new EntriesInfo
        {
            DiskPath = path,
            FileSystemPath = fsPath,
            Entries = entriesResult.Value
        });

        return new Result();
    }

    private async Task<Result<IEnumerable<Entry>>> ReadEntries(Stream stream, string[] parts)
    {
        if (parts.Length == 0 || string.IsNullOrEmpty(parts[0]))
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

            return new Result<IEnumerable<Entry>>(entries);
        }

        return parts[0] switch
        {
            "rdb" => await ReadRdbEntries(stream, parts.Skip(1).ToArray()),
            _ => new Result<IEnumerable<Entry>>(new Error($"Unsupported partition table '{parts[0]}'"))
        };
    }

    private async Task<Result<IEnumerable<Entry>>> ReadRdbEntries(Stream stream, string[] parts)
    {
        OnDebugMessage("Reading Rigid Disk Block");

        var rigidDiskBlock = await commandHelper.GetRigidDiskBlock(stream);

        if (rigidDiskBlock == null)
        {
            return new Result<IEnumerable<Entry>>(new List<Entry>());
        }

        if (!parts.Any())
        {
            return new Result<IEnumerable<Entry>>(rigidDiskBlock.PartitionBlocks.Select(x => new Entry
            {
                Name = x.DriveName,
                Type = EntryType.Dir,
                Size = 0,
                Attributes = string.Empty
            }).ToList());
        }

        var partitionName = parts[0];

        OnDebugMessage($"Partition '{partitionName}'");

        var partitionBlock = rigidDiskBlock.PartitionBlocks.FirstOrDefault(x =>
            x.DriveName.Equals(partitionName, StringComparison.OrdinalIgnoreCase));

        if (partitionBlock == null)
        {
            return new Result<IEnumerable<Entry>>(new Error($"File system path '{fsPath}' not found at path '{path}'"));
        }

        var fileSystemVolumeResult = await MountVolume(stream, partitionBlock);
        if (fileSystemVolumeResult.IsFaulted)
        {
            return new Result<IEnumerable<Entry>>(fileSystemVolumeResult.Error);
        }

        OnDebugMessage($"Volume '{fileSystemVolumeResult.Value.Name}'");

        var fileSystemVolume = fileSystemVolumeResult.Value;
        if (fileSystemVolume == null)
        {
            return new Result<IEnumerable<Entry>>(new Error("No file system volume mounted"));
        }

        var fileSystemPath = string.Join("/", parts.Skip(1));

        if (!string.IsNullOrWhiteSpace(fileSystemPath))
        {
            OnDebugMessage($"Change directory to path '{fileSystemPath}'");

            await fileSystemVolume.ChangeDirectory(fileSystemPath);
        }

        var entries = await fileSystemVolume.ListEntries();

        return new Result<IEnumerable<Entry>>(entries.Select(x => new Entry
        {
            Name = x.Name,
            Type = GetEntryType(x.Type),
            Size = x.Size,
            Date = x.Date,
            Attributes = EntryFormatter.FormatProtectionBits(x.ProtectionBits)
        }).ToList());
    }

    private EntryType GetEntryType(Hst.Amiga.FileSystems.EntryType type)
    {
        return type switch
        {
            Amiga.FileSystems.EntryType.Dir => EntryType.Dir,
            Amiga.FileSystems.EntryType.DirLink => EntryType.Dir,
            Amiga.FileSystems.EntryType.File => EntryType.File,
            Amiga.FileSystems.EntryType.FileLink => EntryType.File,
            Amiga.FileSystems.EntryType.SoftLink => EntryType.File,
            _ => throw new IOException($"Invalid entry type '{type}'")
        };
    }

    private async Task<Result<IFileSystemVolume>> MountVolume(Stream stream, PartitionBlock partitionBlock)
    {
        switch (partitionBlock.DosTypeFormatted)
        {
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
        }

        return new Result<IFileSystemVolume>(new Error($"Unsupported file system '{partitionBlock.DosTypeFormatted}'"));
    }

    private void OnEntriesRead(EntriesInfo entriesInfo)
    {
        EntriesRead?.Invoke(this, new EntriesInfoReadEventArgs(entriesInfo));
    }
}