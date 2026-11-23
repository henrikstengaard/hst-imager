using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.Caching;
using Hst.Imager.Core.Extensions;
using Hst.Imager.Core.Models.FileSystems;
using Hst.Imager.Core.UaeMetadatas;
using Hst.Imager.Core.Volumes;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.Core.Commands;

public class FsMoveCommand(
    ILogger<FsMoveCommand> logger,
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string fromPath,
    string toPath,
    bool forceOverwrite = false,
    UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb,
    bool makeDirectory = false)
    : FsCommandBase(commandHelper, physicalDrives)
{
    private readonly ILogger<FsMoveCommand> logger = logger;

    public override async Task<Result> Execute(CancellationToken token)
    {
        using var appCache = new MemoryAppCache();
        var uaeMetadataHelper = new UaeMetadataHelper(appCache);
        using var volumeFactory = CreateVolumeFactory(uaeMetadataHelper, uaeMetadata);

        // get destination target
        var destTargetResult = await OpenVolumeTarget(volumeFactory, toPath, false, makeDirectory,
            forceOverwrite);
        if (destTargetResult.IsFaulted)
        {
            return new Result(destTargetResult.Error);
        }

        var destTarget = destTargetResult.Value;
        var destMediaIsDir = destTarget.PathComponents.Length == destTarget.DirPathComponents.Length &&
                             destTarget.PathComponents.SequenceEqual(destTarget.DirPathComponents);

        // get source walker
        var srcWalkerResult = await GetWalker(volumeFactory, fromPath);
        if (srcWalkerResult.IsFaulted)
        {
            return new Result(srcWalkerResult.Error);
        }

        var srcWalker = srcWalkerResult.Value;
        var srcVolume = srcWalker.Volume;
        var srcMediaIsDir = srcWalker.PathComponents.Length == srcWalker.DirPathComponents.Length &&
                            srcWalker.PathComponents.SequenceEqual(srcWalker.DirPathComponents);

        if (destTarget.IsSelfCopy(srcWalker))
        {
            return new Result(new SelfCopyError(
                $"Unable to move from source path '{fromPath}' to destination path '{toPath}' onto itself"));
        }

        if (destTarget.IsCyclic(srcWalker))
        {
            return new Result(new CyclicPathError(
                $"Unable to move cyclic path from source path '{fromPath}' to destination path '{toPath}'"));
        }

        var stopwatch = new Stopwatch();

        stopwatch.Start();

        OnInformationMessage($"Moving '{fromPath}' to '{toPath}'");

        var sameMedia = srcVolume.IsSameVolume(destTarget.Volume);

        var filesCount = 0;
        var dirsCount = 0;
        var totalBytes = 0L;

        var deleteEntries = new List<Entry>();
        string[] destRootPathComponents = [];
        var srcDirsMovedIndex = new HashSet<string>();
        var iterateSrcEntries = true;
        
        var entries = new List<Entry>();

        if (srcMediaIsDir && destMediaIsDir)
        {
            var destRootEntryName = srcWalker.PathComponents.Last();
            var rootEntry = new Entry
            {
                Name = destRootEntryName,
                Type = EntryType.Dir,
                RelativePathComponents = [],
                FullPathComponents = srcWalker.PathComponents
            };
            destRootPathComponents = [destRootEntryName];
            
            if (sameMedia && !srcWalker.UsesPattern)
            {
                var moveResult = await MoveEntry(destTarget, destRootPathComponents, rootEntry,
                    srcWalker.IsSingleFileEntryNext);
                if (moveResult.IsFaulted)
                {
                    return new Result(moveResult.Error);
                }
                srcDirsMovedIndex.Add(string.Join("|", rootEntry.FullPathComponents));
                iterateSrcEntries = false;
                dirsCount++;
            }
            else
            {
                await destTarget.CreateDirectory(rootEntry, destRootPathComponents,
                    false, srcWalker.IsSingleFileEntryNext);
                deleteEntries.Add(rootEntry);
            }
        }
        
        var count = 0;
        
        while (iterateSrcEntries && await srcWalker.Next())
        {
            count++;
            if (count >= 200)
            {
                count = 0;
                await srcVolume.Flush();
            }

            var entry = srcWalker.Current;

            // skip directory entries when there are no entry path components or when it's a single file.
            if (entry.Type == EntryType.Dir &&
                (srcWalker.IsSingleFileEntryNext || entry.RelativePathComponents.Length == 0))
            {
                continue;
            }
            
            entries.Add(entry);
        }

        count = 0;
        await srcVolume.Flush();

        var dirPathComponents = srcWalker.DirPathComponents.Length > 0
            ? new[] { srcWalker.DirPathComponents[^1] }
            : [];
        
        foreach (var entry in entries.OrderBy(e => e.RelativePathComponents.Length))
        {
            switch (entry.Type)
            {
                case EntryType.Dir:
                case EntryType.LinkDir:
                    dirsCount++;
                    break;
                case EntryType.File:
                    filesCount++;
                    totalBytes += entry.Size;
                    break;
                case EntryType.LinkFile:
                    filesCount++;
                    break;
            }
            
            var entryPathComponents = destRootPathComponents.Concat(entry.RelativePathComponents).ToArray();
            var pathComponents = srcWalker.IsSingleFileEntryNext
                ? entryPathComponents
                : dirPathComponents.Concat(entryPathComponents).ToArray();
            
            OnInformationMessage($"{srcVolume.MediaPath.Join(pathComponents)}");

            if (sameMedia)
            {
                var isSrcEntryPathMoved = false;
                
                for(var i = 1; i < entry.FullPathComponents.Length; i++)
                {
                    var srcEntryPathComponents = entry.FullPathComponents.Take(i).ToArray();
                    var srcEntryPathKey = string.Join("|", srcEntryPathComponents);
                    if (srcDirsMovedIndex.Contains(srcEntryPathKey))
                    {
                        isSrcEntryPathMoved = true;
                        break;
                    }
                }
                
                if (!isSrcEntryPathMoved)
                {
                    var moveResult = await MoveEntry(destTarget, destRootPathComponents, entry,
                        srcWalker.IsSingleFileEntryNext);
                    if (moveResult.IsFaulted)
                    {
                        return new Result(moveResult.Error);
                    }
                    var srcEntryPathKey = string.Join("|", entry.FullPathComponents);
                    srcDirsMovedIndex.Add(srcEntryPathKey);
                }
            }
            else
            {
                var copyResult = await CopyEntry(srcWalker, entry, destRootPathComponents, destTarget,
                    token);
                if (copyResult.IsFaulted)
                {
                    return new Result(copyResult.Error);
                }

                switch (entry.Type)
                {
                    case EntryType.File:
                    case EntryType.LinkFile:
                        filesCount++;
                        totalBytes += entry.Size;

                        var result = await srcVolume.Delete(entry.FullPathComponents);
                        if (result.IsFaulted)
                        {
                            return result;
                        }

                        break;
                    case EntryType.Dir:
                    case EntryType.LinkDir:
                        dirsCount++;

                        deleteEntries.Add(entry);
                        break;
                }
            }
            
            count++;

            if (count <= 200)
            {
                continue;
            }

            count = 0;
            await srcVolume.Flush();
            await destTarget.Volume.Flush();
        }

        foreach (var entry in deleteEntries.OrderByDescending(x => x.FullPathComponents.Length))
        {
            token.ThrowIfCancellationRequested();
            var result = await srcVolume.Delete(entry.FullPathComponents);
            if (result.IsFaulted)
            {
                return result;
            }
        }

        await srcVolume.Flush();
        await destTarget.Volume.Flush();

        stopwatch.Stop();

        var stats = new List<string>();
        if (dirsCount > 0 || filesCount == 0)
        {
            stats.Add($"{dirsCount} {(dirsCount > 1 ? "directories" : "directory")}");
        }
        if (filesCount > 0 || dirsCount == 0)
        {
            stats.Add($"{filesCount} {(filesCount == 1 ? "file" : "files")}");
        }
        stats.Add($"{totalBytes.FormatBytes()} moved in {stopwatch.Elapsed.FormatElapsed()}");

        OnInformationMessage(string.Join(", ", stats));
        
        return new Result();
    }

    private static async Task<Result> MoveEntry(VolumeTarget destWriter, string[] destRootPathComponents, Entry entry,
        bool singleFile)
    {
        var destEntryPathComponents = destRootPathComponents.Concat(entry.RelativePathComponents).ToArray();

        var result = await destWriter.MoveEntry(entry, destEntryPathComponents, singleFile);

        return result.IsFaulted ? result : new Result();
    }

    private static async Task<Result> CopyEntry(VolumeEntryWalker source, Entry entry,
        string[] destPathComponents, VolumeTarget destination, CancellationToken token)
    {
        if (entry.Type == EntryType.Dir && (source.IsSingleFileEntryNext || entry.RelativePathComponents.Length == 0))
        {
            return new Result();
        }

        var path = destPathComponents.Concat(entry.RelativePathComponents).ToArray();

        Result result;
        if (entry.Type is EntryType.Dir or EntryType.LinkDir)
        {
            result = await destination.CreateDirectory(entry, path, false, source.IsSingleFileEntryNext);
        }
        else
        {
            var openResult = await source.Volume.OpenFile(entry.FullPathComponents);
            if (openResult.IsFaulted)
            {
                return new Result(openResult.Error);
            }

            await using var stream = openResult.Value;
            result = await destination.CreateFile(entry, path, stream, false, source.IsSingleFileEntryNext);
        }

        return result.IsFaulted ? result : new Result();
    }

    private async Task<Result<VolumeEntryWalker>> GetWalker(VolumeFactory volumeFactory, string path)
    {
        var volumePathResult = await volumeFactory.Open(path, VolumeAccess.Write, openArchives: false);
        if (volumePathResult.IsFaulted)
        {
            return new Result<VolumeEntryWalker>(volumePathResult.Error);
        }

        var (volume, pathComponents) = volumePathResult.Value;

        // root directory of a volume can't be moved
        if (pathComponents.Length == 0)
        {
            return new Result<VolumeEntryWalker>(new Error(
                $"Path '{path}' doesn't contain partition table, partition number and entry to move"));
        }

        return await VolumeEntryWalker.Create(volume, pathComponents, true);
    }
}
