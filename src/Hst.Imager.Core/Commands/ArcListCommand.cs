namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Microsoft.Extensions.Logging;
using Models.FileSystems;

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
            return new Result<IEntryIterator>(
                new PathNotFoundError($"Media path not defined",
                    pathResult.Value.MediaPath));
        }
        
        // lha
        var lhaEntryIterator = await GetLhaEntryIterator(pathResult.Value, recursive);
        if (lhaEntryIterator != null && lhaEntryIterator.IsSuccess)
        {
            await ListEntries(lhaEntryIterator.Value, pathResult.Value.FileSystemPath);
            return new Result();
        }

        // adf
        var adfEntryIterator = await GetLhaEntryIterator(pathResult.Value, recursive);
        if (adfEntryIterator != null && adfEntryIterator.IsSuccess)
        {
            await ListEntries(adfEntryIterator.Value, pathResult.Value.FileSystemPath);
            return new Result();
        }
        
        // iso
        var isoEntryIterator = await GetIso9660EntryIterator(pathResult.Value, recursive);
        if (isoEntryIterator != null && isoEntryIterator.IsSuccess)
        {
            await ListEntries(isoEntryIterator.Value, pathResult.Value.FileSystemPath);
            return new Result();
        }
        
        return new Result<IEntryIterator>(new Error($"Archive '{path}' not supported"));
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
            Path = path,
            Entries = dirs.OrderBy(x => x.Name).Concat(files.OrderBy(x => x.Name)).ToList()
        });
    }

    private void OnEntriesRead(EntriesInfo entriesInfo)
    {
        EntriesRead?.Invoke(this, new EntriesInfoReadEventArgs(entriesInfo));
    }
}