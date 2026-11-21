namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Microsoft.Extensions.Logging;
using Models.FileSystems;
using Caching;
using MagicBytes;
using UaeMetadatas;
using Volumes;

public class ArcListCommand : FsCommandBase
{
    private readonly ILogger<ArcListCommand> logger;
    private readonly string path;
    private readonly bool recursive;

    public ArcListCommand(ILogger<ArcListCommand> logger, ICommandHelper commandHelper,
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
        OnDebugMessage($"Resolving path '{path}'");

        var pathResult = commandHelper.ResolveMedia(path);
        if (pathResult.IsFaulted)
        {
            return new Result(pathResult.Error);
        }

        OnDebugMessage($"Media Path: '{pathResult.Value.MediaPath}'");
        OnDebugMessage($"File System Path: '{pathResult.Value.FileSystemPath}'");

        if (string.IsNullOrWhiteSpace(pathResult.Value.MediaPath))
        {
            return new Result(
                new PathNotFoundError($"Media path not defined",
                    pathResult.Value.MediaPath));
        }
        
        var dataType = await commandHelper.DetectDataType(pathResult.Value.MediaPath);
        if (dataType is not (DataType.Zip or DataType.Lha or DataType.Lzx or DataType.Lzw or DataType.Adf
            or DataType.Iso9660))
        {
            return new Result(new Error($"Archive '{path}' not supported"));
        }

        using var appCache = new MemoryAppCache();
        using var volumeFactory = CreateVolumeFactory(new UaeMetadataHelper(appCache), UaeMetadata.None);

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

    private async Task ListEntries(VolumeEntryWalker walker)
    {
        var dirs = new List<Entry>();
        var files = new List<Entry>();

        while (await walker.Next())
        {
            var entry = walker.Current;
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
            Path = path,
            Entries = dirs.OrderBy(x => x.Name).Concat(files.OrderBy(x => x.Name)).ToList()
        });
    }

    private void OnEntriesRead(EntriesInfo entriesInfo)
    {
        EntriesRead?.Invoke(this, new EntriesInfoReadEventArgs(entriesInfo));
    }
}