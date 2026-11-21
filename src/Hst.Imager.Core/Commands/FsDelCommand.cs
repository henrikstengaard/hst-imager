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

namespace Hst.Imager.Core.Commands;

public class FsDelCommand(
    ICommandHelper commandHelper,
    IEnumerable<IPhysicalDrive> physicalDrives,
    string path,
    UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb)
    : FsCommandBase(commandHelper, physicalDrives)
{
    public override async Task<Result> Execute(CancellationToken token)
    {
        using var appCache = new MemoryAppCache();
        var uaeMetadataHelper = new UaeMetadataHelper(appCache);

        using var volumeFactory = CreateVolumeFactory(uaeMetadataHelper, uaeMetadata);

        var walkerResult = await GetWalker(volumeFactory);
        if (walkerResult.IsFaulted)
        {
            return new Result(walkerResult.Error);
        }

        var walker = walkerResult.Value;
        var volume = walker.Volume;

        var stopwatch = new Stopwatch();

        stopwatch.Start();

        OnInformationMessage($"Deleting path '{path}'");

        var entries = new List<Entry>();

        while (await walker.Next())
        {
            var entry = walker.Current;
            
            // skip directory entries when there are no entry path components or when it's a single file.
            if (entry.Type == EntryType.Dir &&
                (walker.IsSingleFileEntryNext || entry.RelativePathComponents.Length == 0))
            {
                continue;
            }
            
            entries.Add(entry);
        }

        var filesCount = 0;
        var dirsCount = 0;
        var totalBytes = 0L;

        var dirPathComponents = walker.DirPathComponents.Length > 0
            ? new[] { walker.DirPathComponents[^1] }
            : [];
        
        // delete entries starting with the longest path to ensure files are deleted before their parent directories
        foreach (var entry in entries.OrderByDescending(e => e.RelativePathComponents.Length))
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
            
            var pathComponents = walker.IsSingleFileEntryNext
                ? entry.RelativePathComponents
                : dirPathComponents.Concat(entry.RelativePathComponents).ToArray();
            
            OnInformationMessage($"{volume.MediaPath.Join(pathComponents)}");

            var deleteResult = await volume.Delete(entry.FullPathComponents);
            if (deleteResult.IsFaulted)
            {
                return deleteResult;
            }
        }

        // delete root directory, if present and is not a single file
        if (!walker.IsSingleFileEntryNext &&
            walker.PathComponents.Length == walker.DirPathComponents.Length)
        {
            OnInformationMessage($"{walker.DirPathComponents[^1]}");
            
            var deleteResult = await volume.Delete(walker.DirPathComponents);
            if (deleteResult.IsFaulted)
            {
                return deleteResult;
            }
        }

        await volume.Flush();

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
        stats.Add($"{totalBytes.FormatBytes()} deleted in {stopwatch.Elapsed.FormatElapsed()}");

        OnInformationMessage(string.Join(", ", stats));

        return new Result();
    }
    
    private async Task<Result<VolumeEntryWalker>> GetWalker(VolumeFactory volumeFactory)
    {
        var volumePathResult = await volumeFactory.Open(path, VolumeAccess.Write, openArchives: false);
        if (volumePathResult.IsFaulted)
        {
            return new Result<VolumeEntryWalker>(volumePathResult.Error);
        }

        var (volume, pathComponents) = volumePathResult.Value;

        // root directory of a volume can't be deleted
        if (pathComponents.Length == 0)
        {
            return new Result<VolumeEntryWalker>(new Error(
                $"Path '{path}' doesn't contain partition table, partition number and entry to delete"));
        }

        return await VolumeEntryWalker.Create(volume, pathComponents, true);
    }
}
